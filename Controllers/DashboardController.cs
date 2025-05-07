using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PageWhispers.Data;
using PageWhispers.Model;
using Microsoft.AspNetCore.Identity;
using System.Diagnostics;
using System.Threading.Tasks;

namespace BookHive.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<UserAccount> _userManager;

        public DashboardController(ApplicationDbContext context, UserManager<UserAccount> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            // Fetch the 6 most recently added books
            var books = await _context.Books
                .OrderByDescending(b => b.AddedDate)
                .Take(5)
                .ToListAsync();

            // Fetch timed discounts for these books
            var bookIds = books.Select(b => b.Id).ToList();
            var timedDiscounts = await _context.TimedDiscounts
                .Where(td => bookIds.Contains(td.BookId))
                .ToListAsync();

            // Create a list of BookWithDiscountViewModel with discount information
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

            // Get the current user if logged in
            UserAccount? currentUser = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                currentUser = await _userManager.GetUserAsync(User);
            }

            // Get live timed announcements
            var timedAnnouncements = await _context.TimedAnnouncements
                .Where(a => a.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

           
            var model = new HomeViewModel
            {
                Books = booksWithDiscounts,
                CurrentUser = currentUser,
                TimedAnnouncements = timedAnnouncements
            };

            ViewData["CurrentUser"] = currentUser;
            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}