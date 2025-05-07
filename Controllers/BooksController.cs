using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PageWhispers.Data;
using System.IO;
using System.Threading.Tasks;
using PageWhispers.Hubs;
using Microsoft.Extensions.Logging;
using PageWhispers.Model;


namespace PageWhispers.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<BooksController> _logger;
        private readonly UserManager<UserAccount> _userManager;
        private readonly IHubContext<OrderNotificationsHub> _hubContext;

        public BooksController(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            ILogger<BooksController> logger,
            UserManager<UserAccount> userManager,
            IHubContext<OrderNotificationsHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _webHostEnvironment = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        // GET: Books/Index
        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = 12,
            string search = "",
            string sort = "title",
            string category = "all",
            string author = "",
            string genre = "",
            string availability = "all",
            bool? physicalLibraryAccess = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            double? minRating = null,
            string language = "",
            string format = "",
            string publisher = "",
            string isbn = "")
        {
            var query = _context.Books.AsQueryable();

            // Search by title, ISBN, or description
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(b =>
                    (b.Title != null && b.Title.ToLower().Contains(search)) ||
                    (b.ISBN != null && b.ISBN.ToLower().Contains(search)) ||
                    (b.BookDescription != null && b.BookDescription.ToLower().Contains(search)));
            }

            // Filter by author
            if (!string.IsNullOrEmpty(author))
            {
                query = query.Where(b => b.Author != null && b.Author.ToLower().Contains(author.ToLower()));
            }

            // Filter by genre
            if (!string.IsNullOrEmpty(genre))
            {
                query = query.Where(b => b.Genre != null && b.Genre.ToLower() == genre.ToLower());
            }

            // Filter by availability (stock)
            if (availability == "available")
            {
                query = query.Where(b => b.Quantity > 0);
            }
            else if (availability == "unavailable")
            {
                query = query.Where(b => b.Quantity <= 0);
            }

            // Filter by physical library access
            if (physicalLibraryAccess.HasValue)
            {
                query = query.Where(b => b.IsPhysicalLibraryAccess == physicalLibraryAccess.Value);
            }

            // Filter by price range
            if (minPrice.HasValue)
            {
                query = query.Where(b => b.Price >= minPrice.Value);
            }
            if (maxPrice.HasValue)
            {
                query = query.Where(b => b.Price <= maxPrice.Value);
            }

            // Filter by ratings
            if (minRating.HasValue)
            {
                query = query.Where(b => b.Reviews.Any() ? b.Reviews.Where(r => r.ParentReviewId == null).Average(r => r.Rating) >= minRating.Value : false);
            }

            // Filter by language
            if (!string.IsNullOrEmpty(language))
            {
                query = query.Where(b => b.Language != null && b.Language.ToLower() == language.ToLower());
            }

            // Filter by format
            if (!string.IsNullOrEmpty(format))
            {
                query = query.Where(b => b.Format != null && b.Format.ToLower().Equals(format, StringComparison.CurrentCultureIgnoreCase));
            }

            // Filter by publisher
            if (!string.IsNullOrEmpty(publisher))
            {
                query = query.Where(b => b.Publisher != null && b.Publisher.ToLower().Contains(publisher.ToLower()));
            }

            // Filter by ISBN (already handled in search)

            // Category tabs
            switch (category.ToLower())
            {
                case "bestsellers":
                    query = query.Where(b => b.IsBestseller);
                    break;
                case "awardwinners":
                    query = query.Where(b => b.IsAwardWinner);
                    break;
                case "newreleases":
                    // Books published in the past 3 months
                    var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
                    query = query.Where(b => b.PublicationDate >= threeMonthsAgo);
                    break;
                case "newarrivals":
                    // Books listed in the past month
                    var oneMonthAgo = DateTime.UtcNow.AddMonths(-1);
                    query = query.Where(b => b.AddedDate >= oneMonthAgo);
                    break;
                case "comingsoon":
                    // Books with a future publication date
                    query = query.Where(b => b.PublicationDate > DateTime.UtcNow);
                    break;
                case "deals":
                    // Books with active discounts
                    query = query.Where(b => _context.TimedDiscounts.Any(td => td.BookId == b.Id && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow));
                    break;
                case "all":
                default:
                    // No additional filtering for "All Books"
                    break;
            }

            // Sorting
            switch (sort.ToLower())
            {
                case "author":
                    query = query.OrderBy(b => b.Author ?? string.Empty);
                    break;
                case "publicationdate":
                    query = query.OrderByDescending(b => b.PublicationDate);
                    break;
                case "price":
                    query = query.OrderBy(b => b.Price);
                    break;
                case "popularity":
                    // Sort by most sold (based on Orders)
                    query = query
                        .GroupJoin(_context.Orders.Where(o => !o.IsCancelled),
                            b => b.Id,
                            o => o.BookId,
                            (b, orders) => new { Book = b, TotalSold = orders.Sum(o => o.Quantity) })
                        .OrderByDescending(x => x.TotalSold)
                        .Select(x => x.Book);
                    break;
                case "title":
                default:
                    query = query.OrderBy(b => b.Title ?? string.Empty);
                    break;
            }

            // Pagination
            var totalBooks = await query.CountAsync();
            var books = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Apply discounts
            var bookIds = books.Select(b => b.Id).ToList();
            var timedDiscounts = await _context.TimedDiscounts
                .Where(td => bookIds.Contains(td.BookId))
                .ToListAsync();

            var booksWithDiscounts = books.Select(book =>
            {
                var discount = timedDiscounts.FirstOrDefault(td => td.BookId == book.Id && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                return new DiscountCatalogEntry
                {
                    Book = book,
                    OnSaleFlag = discount?.OnSaleFlag ?? false,
                    IsDiscountActive = discount != null,
                    DiscountedPrice = discount != null ? book.Price * (1 - discount.DiscountPercentage) : book.Price
                };
            }).ToList();

            // Set ViewBag properties for the view
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalBooks / pageSize);
            ViewBag.CurrentPage = page;
            ViewBag.Search = search;
            ViewBag.Sort = sort;
            ViewBag.Category = category;
            ViewBag.Author = author;
            ViewBag.Genre = genre;
            ViewBag.Availability = availability;
            ViewBag.PhysicalLibraryAccess = physicalLibraryAccess;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.MinRating = minRating;
            ViewBag.Language = language;
            ViewBag.Format = format;
            ViewBag.Publisher = publisher;
            ViewBag.ISBN = isbn;

            // Handle cart items for authenticated users
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    ViewBag.HasCartItems = await _context.Carts.AnyAsync(c => c.UserId == user.Id);
                }
                else
                {
                    ViewBag.HasCartItems = false;
                }
            }
            else
            {
                ViewBag.HasCartItems = false;
            }

            return View(booksWithDiscounts);
        }

        // GET: Books/Details/{id}
        [AllowAnonymous] // Enables unregistered viewers to browse  the books
        public async Task<IActionResult> Details(int id)
        {
            var book = await _context.Books
                .Include(b => b.Reviews)
                .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (book == null)
            {
                return NotFound();
            }

            var discount = await _context.TimedDiscounts
                .FirstOrDefaultAsync(td => td.BookId == id && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);

            // Load reviews and their top replies
            var reviews = await _context.Reviews
                .Where(r => r.BookId == id && r.ParentReviewId == null)
                .Include(r => r.User)
                .Include(r => r.Replies)
                .ThenInclude(reply => reply.User)
                .ToListAsync();

            var model = new DiscountCatalogEntry
            {
                Book = book,
                OnSaleFlag = discount?.OnSaleFlag ?? false,
                IsDiscountActive = discount != null,
                DiscountedPrice = discount != null ? book.Price * (1 - discount.DiscountPercentage) : book.Price,
                Reviews = reviews
            };

            // Fetch recommendations
            // Most rated books based on the rating
            var mostRatedBooks = await _context.Books
                .Select(b => new
                {
                    Book = b,
                    AverageRating = b.Reviews.Any(r => r.ParentReviewId == null) ? b.Reviews.Where(r => r.ParentReviewId == null).Average(r => r.Rating) : 0
                })
                .Where(b => b.Book.Id != book.Id) // Exclude the current book
                .OrderByDescending(b => b.AverageRating)
                .Take(4)
                .Select(b => new DiscountCatalogEntry
                {
                    Book = b.Book,
                    OnSaleFlag = false,
                    IsDiscountActive = false,
                    DiscountedPrice = b.Book.Price
                })
                .ToListAsync();

            // Most ordered books
            var mostOrderedBooks = await _context.Orders
                .Where(o => !o.IsCancelled)
                .GroupBy(o => o.BookId)
                .Select(g => new
                {
                    BookId = g.Key,
                    TotalQuantity = g.Sum(o => o.Quantity)
                })
                .OrderByDescending(g => g.TotalQuantity)
                .Take(4)
                .Join(_context.Books,
                    g => g.BookId,
                    b => b.Id,
                    (g, b) => new DiscountCatalogEntry
                    {
                        Book = b,
                        OnSaleFlag = false,
                        IsDiscountActive = false,
                        DiscountedPrice = b.Price
                    })
                .Where(b => b.Book.Id != book.Id) // Exclude the current book
                .ToListAsync();

            ViewBag.MostRatedBooks = mostRatedBooks;
            ViewBag.MostOrderedBooks = mostOrderedBooks;

            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var hasPurchased = await _context.Orders
                        .AnyAsync(o => o.UserId == user.Id && o.BookId == id && !o.IsCancelled);
                    ViewBag.HasPurchased = hasPurchased;
                    ViewBag.HasCartItems = await _context.Carts.AnyAsync(c => c.UserId == user.Id);
                }
                else
                {
                    ViewBag.HasPurchased = false;
                    ViewBag.HasCartItems = false;
                }
            }
            else
            {
                ViewBag.HasPurchased = false;
                ViewBag.HasCartItems = false;
            }

            return View(model);
        }

        // GET: Books/GetBookStock?bookId={id}
        [AllowAnonymous]
        public async Task<IActionResult> GetBookStock(int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
            {
                return Json(new { stock = 0 });
            }

            return Json(new { stock = book.Quantity });
        }

        // POST: Books/AddToCart/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize] // Require authentication
        public async Task<IActionResult> AddToCart(int id, int quantity = 1)
        {
            _logger.LogInformation("Attempting to add book {BookId} to cart for current user", id);

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found in AddToCart");
                return Json(new { success = false, message = "User not found. Please log in." });
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                _logger.LogWarning("Book not found in AddToCart for id {BookId}", id);
                return Json(new { success = false, message = "Book not found." });
            }

            if (!book.IsAvailable)
            {
                return Json(new { success = false, message = "This book is currently out of stock." });
            }

            if (quantity < 1)
            {
                quantity = 1;
            }

            var existingEntry = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.BookId == id);
            if (existingEntry != null)
            {
                var newQuantity = existingEntry.Quantity + quantity;
                if (newQuantity > book.Quantity)
                {
                    return Json(new { success = false, message = $"Only {book.Quantity} copies of '{book.Title}' are available." });
                }
                existingEntry.Quantity = newQuantity;
            }
            else
            {
                if (quantity > book.Quantity)
                {
                    return Json(new { success = false, message = $"Only {book.Quantity} copies of '{book.Title}' are available." });
                }
                var cartEntry = new UserCartItem
                {
                    UserId = user.Id,
                    BookId = id,
                    Quantity = quantity
                };
                _context.Carts.Add(cartEntry);
            }

            await _context.SaveChangesAsync();

            // Broadcast updated cart count to all clients
            int cartCount = await _context.Carts
                .Where(c => c.UserId == user.Id)
                .Select(c => c.BookId)
                .Distinct()
                .CountAsync();
            await _hubContext.Clients.All.SendAsync("UpdateCartCount", cartCount);

            return Json(new { success = true, message = "Cart was added successfully!" });
        }

        // POST: Books/ToggleWishlist/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize] // Require authentication
        public async Task<IActionResult> ToggleWishlist(int id)
        {
            try
            {
                _logger.LogInformation("Attempting to toggle whitelist status for book {BookId} for current user", id);

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    _logger.LogWarning("User not found in ToggleWishlist");
                    return Json(new { success = false, message = "User not found." });
                }

                var book = await _context.Books.FindAsync(id);
                if (book == null)
                {
                    return Json(new { success = false, message = "Book not found." });
                }

                var existingEntry = await _context.Whitelists
                    .FirstOrDefaultAsync(w => w.UserId == user.Id && w.BookId == id);

                if (existingEntry != null)
                {
                    // Remove from whitelist
                    _context.Whitelists.Remove(existingEntry);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, inWishlist = false, message = "Book removed from your whitelist." });
                }
                else
                {
                    // Add to whitelist
                    var whitelistEntry = new Whitelist
                    {
                        UserId = user.Id,
                        BookId = id
                    };
                    _context.Whitelists.Add(whitelistEntry);
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, inWishlist = true, message = "Book added to your whitelist." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling whitelist for book {BookId}", id);
                return Json(new { success = false, message = "Server error: " + ex.Message });
            }
        }

        // GET: Books/IsInWishlist?bookId={id}
        [Authorize] 
        public async Task<IActionResult> IsInWishlist(int bookId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { inWishlist = false });
            }

            var inWishlist = await _context.Whitelists
                .AnyAsync(w => w.UserId == user.Id && w.BookId == bookId);

            return Json(new { inWishlist = inWishlist });
        }

        // Creating a new bookview
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public IActionResult Create()
        {
            return View(new BookView());
        }

        // Admin adds books
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookView model)
        {
            try
            {
                var book = new BookCatalogs
                {
                    Title = model.Title ?? string.Empty,
                    Author = model.Author ?? string.Empty,
                    BookDescription = model.Description ?? string.Empty,
                    AddedDate = DateTime.UtcNow,
                    Price = model.Price,
                    Quantity = model.Quantity
                };

                if (model.CoverImage != null && model.CoverImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images/book-covers");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        try
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to create directory {UploadsFolder}", uploadsFolder);
                            TempData["ErrorMessage"] = "Failed to create upload directory: " + ex.Message;
                            return View(model);
                        }
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.CoverImage.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    try
                    {
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await model.CoverImage.CopyToAsync(fileStream);
                        }
                        book.CoverImageUrl = "/images/book-covers/" + uniqueFileName;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to save cover image for book {BookTitle}", model.Title);
                        TempData["ErrorMessage"] = "Failed to save cover image: " + ex.Message;
                        return View(model);
                    }
                }

                _context.Add(book);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Book '{book.Title}' created successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating book {BookTitle}", model.Title);
                TempData["ErrorMessage"] = "A database error occurred while creating the book: " + (ex.InnerException?.Message ?? ex.Message);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating book {BookTitle}", model.Title);
                TempData["ErrorMessage"] = "An unexpected error occurred while creating the book: " + ex.Message;
                return View(model);
            }
        }

        // GET: Books/Edit/{id}
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            var model = new BookView
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                Description = book.BookDescription,
                CoverImageUrl = book.CoverImageUrl,
                Price = book.Price,
                Quantity = book.Quantity
            };

            return View(model);
        }

        // For editing books
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BookView model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Please correct the errors in the form.";
                return View(model);
            }

            try
            {
                var book = await _context.Books.FindAsync(id);
                if (book == null)
                {
                    return NotFound();
                }

                var oldImagePath = book.CoverImageUrl;

                book.Title = model.Title ?? book.Title;
                book.Author = model.Author ?? book.Author;
                book.BookDescription = model.Description ?? book.BookDescription;
                book.Price = model.Price;
                book.Quantity = model.Quantity;

                if (model.CoverImage != null && model.CoverImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images/book-covers");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.CoverImage.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.CoverImage.CopyToAsync(fileStream);
                    }

                    book.CoverImageUrl = "/images/book-covers/" + uniqueFileName;

                    if (!string.IsNullOrEmpty(oldImagePath))
                    {
                        var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, oldImagePath.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }
                }

                _context.Update(book);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Book '{book.Title}' updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookExists(model.Id))
                {
                    return NotFound();
                }
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing book {BookId}", id);
                TempData["ErrorMessage"] = "An error occurred while editing the book: " + ex.Message;
                return View(model);
            }
        }

        // For deleting books 
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        // POST: Books/DeleteConfirmed/{id}
        [Authorize(Roles = "Admin")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var book = await _context.Books.FindAsync(id);
                if (book != null)
                {
                    if (!string.IsNullOrEmpty(book.CoverImageUrl))
                    {
                        var filePath = Path.Combine(_webHostEnvironment.WebRootPath, book.CoverImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }

                    _context.Books.Remove(book);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Book '{book.Title}' deleted successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Book not found.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting book {BookId}", id);
                TempData["ErrorMessage"] = "An error occurred while deleting the book: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Books/ManageDiscounts/{id}
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ManageDiscounts(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            var discount = await _context.TimedDiscounts
                .FirstOrDefaultAsync(td => td.BookId == id);

            var model = new DiscountDisplayModel
            {
                BookId = book.Id,
                BookTitle = book.Title,
                DiscountPercentage = discount?.DiscountPercentage * 100 ?? 0,
                StartDate = discount?.StartDate ?? DateTime.UtcNow,
                ExpiresAt = discount?.ExpiresAt ?? DateTime.UtcNow.AddDays(7),
                OnSaleFlag = discount?.OnSaleFlag ?? false
            };

            return View(model);
        }

        // POST: Books/ManageDiscounts
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageDiscounts(DiscountDisplayModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var book = await _context.Books.FindAsync(model.BookId);
            if (book == null)
            {
                return NotFound();
            }

            var existingDiscount = await _context.TimedDiscounts
                .FirstOrDefaultAsync(td => td.BookId == model.BookId);

            if (model.DiscountPercentage > 0)
            {
                if (existingDiscount == null)
                {
                    var discount = new DiscountPeriod
                    {
                        BookId = model.BookId,
                        DiscountPercentage = model.DiscountPercentage / 100,
                        StartDate = model.StartDate,
                        ExpiresAt = model.ExpiresAt,
                        OnSaleFlag = model.OnSaleFlag
                    };
                    _context.TimedDiscounts.Add(discount);
                }
                else
                {
                    existingDiscount.DiscountPercentage = model.DiscountPercentage / 100;
                    existingDiscount.StartDate = model.StartDate;
                    existingDiscount.ExpiresAt = model.ExpiresAt;
                    existingDiscount.OnSaleFlag = model.OnSaleFlag;
                    _context.TimedDiscounts.Update(existingDiscount);
                }
            }
            else if (existingDiscount != null)
            {
                _context.TimedDiscounts.Remove(existingDiscount);
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Discount updated successfully.";
            return RedirectToAction("Details", new { id = model.BookId });
        }

        private bool BookExists(int id)
        {
            return _context.Books.Any(e => e.Id == id);
        }
    }
}

