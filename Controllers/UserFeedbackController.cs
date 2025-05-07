using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using PageWhispers.Model;
using PageWhispers.Data;

namespace BookHive.Controllers
{
    [Authorize]
    public class UserFeedbackController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserFeedbackController> _logger;
        private readonly UserManager<UserAccount> _userManager;

        public UserFeedbackController(
            ApplicationDbContext context,
            ILogger<UserFeedbackController> logger,
            UserManager<UserAccount> userManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        // GET: Reviews/Create/{bookId}
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> Create(int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            // Check if user has purchased the book
            var hasPurchased = await _context.Orders
                .AnyAsync(o => o.UserId == user.Id && o.BookId == bookId && !o.IsCancelled);
            if (!hasPurchased)
            {
                TempData["ErrorMessage"] = "You can only review books you have purchased.";
                return RedirectToAction("Details", "Books", new { id = bookId });
            }

            // Check if user has already reviewed the book (top-level review)
            var existingReview = await _context.Reviews
                .AnyAsync(r => r.UserId == user.Id && r.BookId == bookId && r.ParentReviewId == null);
            if (existingReview)
            {
                TempData["ErrorMessage"] = "You have already reviewed this book.";
                return RedirectToAction("Details", "Books", new { id = bookId });
            }

            var model = new BookFeedbackRecordModel
            {
                BookId = bookId
            };
            return View(model);
        }

        // POST: Reviews/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> Create([Bind("BookId,Rating,Comment")] BookFeedbackRecordModel review)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Validate input
            if (review.Rating < 1 || review.Rating > 5)
            {
                // For AJAX requests, return JSON instead of a view
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Rating must be between 1 and 5." });
                }
                TempData["ErrorMessage"] = "Rating must be between 1 and 5.";
                return View(review);
            }

            if (string.IsNullOrWhiteSpace(review.Comment))
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Comment is required." });
                }
                TempData["ErrorMessage"] = "Comment is required.";
                return View(review);
            }

            var book = await _context.Books.FindAsync(review.BookId);
            if (book == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "Book not found." });
                }
                return NotFound();
            }

            var hasPurchased = await _context.Orders
                .AnyAsync(o => o.UserId == user.Id && o.BookId == review.BookId && !o.IsCancelled);
            if (!hasPurchased)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "You can only review books you have purchased." });
                }
                TempData["ErrorMessage"] = "You can only review books you have purchased.";
                return RedirectToAction("Details", "Books", new { id = review.BookId });
            }

            // Check if user has already reviewed the book (top-level review)
            var existingReview = await _context.Reviews
                .AnyAsync(r => r.UserId == user.Id && r.BookId == review.BookId && r.ParentReviewId == null);
            if (existingReview)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "You have already reviewed this book." });
                }
                TempData["ErrorMessage"] = "You have already reviewed this book.";
                return RedirectToAction("Details", "Books", new { id = review.BookId });
            }

            review.UserId = user.Id;
            review.ReviewDate = DateTime.UtcNow;
            review.ParentReviewId = null; // Top-level review

            try
            {
                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving review for book {BookId} by user {UserId}", review.BookId, user.Id);
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Json(new { success = false, message = "An error occurred while saving your review: " + ex.Message });
                }
                TempData["ErrorMessage"] = "An error occurred while saving your review: " + ex.Message;
                return RedirectToAction("Details", "Books", new { id = review.BookId });
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    success = true,
                    message = "Review submitted successfully.",
                    review = new
                    {
                        userName = user.UserName,
                        rating = review.Rating,
                        comment = review.Comment,
                        reviewDate = review.ReviewDate.ToString("d MMM yyyy")
                    }
                });
            }

            TempData["SuccessMessage"] = "Review submitted successfully.";
            return RedirectToAction("Details", "Books", new { id = review.BookId });
        }

        // POST: Reviews/CreateReply
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> CreateReply(int bookId, int parentReviewId, string comment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return Json(new { success = false, message = "Book not found." });
            }

            var parentReview = await _context.Reviews.FindAsync(parentReviewId);
            if (parentReview == null || parentReview.BookId != bookId || parentReview.ParentReviewId != null)
            {
                return Json(new { success = false, message = "Parent review not found or invalid." });
            }

            // Check if user has purchased the book
            var hasPurchased = await _context.Orders
                .AnyAsync(o => o.UserId == user.Id && o.BookId == bookId && !o.IsCancelled);
            if (!hasPurchased)
            {
                return Json(new { success = false, message = "You must purchase the book before replying to a review." });
            }

            // Validate comment
            if (string.IsNullOrWhiteSpace(comment))
            {
                return Json(new { success = false, message = "Comment is required." });
            }

            var reply = new BookFeedbackRecordModel
            {
                BookId = bookId,
                UserId = user.Id,
                Rating = 0, // Replies don't have ratings
                Comment = comment,
                ReviewDate = DateTime.UtcNow,
                ParentReviewId = parentReviewId
            };

            try
            {
                _context.Reviews.Add(reply);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving reply for review {ParentReviewId} by user {UserId}", parentReviewId, user.Id);
                return Json(new { success = false, message = "An error occurred while saving your reply: " + ex.Message });
            }

            return Json(new
            {
                success = true,
                message = "Reply submitted successfully!",
                reply = new
                {
                    comment = reply.Comment,
                    userName = user.UserName,
                    reviewDate = reply.ReviewDate.ToString("d MMM yyyy")
                }
            });
        }
    }
}