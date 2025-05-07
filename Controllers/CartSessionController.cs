using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using PageWhispers.Data;
using PageWhispers.Hubs;
using PageWhispers.Model;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;


namespace PageWhispers.Controllers
{
    public class CartSessionController : Controller
    {
        private readonly UserManager<UserAccount> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CartSessionController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IHubContext<AnnouncementHub> _hubContext;
        private readonly IHubContext<OrderNotificationsHub> _orderHubContext;
        public CartSessionController(
            UserManager<UserAccount> userManager,
            ApplicationDbContext context,
            ILogger<CartSessionController> logger,
            IConfiguration configuration,
            IHubContext<AnnouncementHub> hubContext,
            IHubContext<OrderNotificationsHub> orderHubContext)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _orderHubContext = orderHubContext ?? throw new ArgumentNullException(nameof(orderHubContext));
        }

        // GET: Cart/Cart
        public async Task<IActionResult> Cart()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for cart view.");
                return NotFound("User not found.");
            }

            var cartItems = await _context.Carts
                .Where(c => c.UserId == user.Id)
                .Include(c => c.Book)
                .ToListAsync();

            // Reload book data to ensure fresh stock values
            foreach (var item in cartItems)
            {
                await _context.Entry(item.Book).ReloadAsync();
            }

            // Load timed discounts for the books in the cart
            var bookIds = cartItems.Select(c => c.BookId).ToList();
            var timedDiscounts = await _context.TimedDiscounts
                .Where(td => bookIds.Contains(td.BookId))
                .ToListAsync();

            // Create view models with discount information
            var cartItemsWithDiscounts = cartItems.Select(item =>
            {
                var bookPrice = item.Book.Price;
                var discount = timedDiscounts.FirstOrDefault(td => td.BookId == item.BookId && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                return new CartWithDiscount
                {
                    CartItem = item,
                    OnSaleFlag = discount?.OnSaleFlag ?? false,
                    IsDiscountActive = discount != null,
                    DiscountedPrice = discount != null ? bookPrice * (1 - discount.DiscountPercentage) : bookPrice
                };
            }).ToList();

            return View("~/Views/User/Cart.cshtml", cartItemsWithDiscounts);
        }

        // POST: Cart/UpdateCartQuantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCartQuantity(int bookId, int quantity)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for updating cart quantity.");
                return Json(new { success = false, message = "User not found." });
            }

            var cartItem = await _context.Carts
                .Include(c => c.Book)
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.BookId == bookId);

            if (cartItem == null)
            {
                return Json(new { success = false, message = "Book not in your cart." });
            }

            // Reload book to ensure fresh stock data
            await _context.Entry(cartItem.Book).ReloadAsync();

            if (quantity < 1)
            {
                return Json(new { success = false, message = "Quantity must be at least 1." });
            }

            if (quantity > cartItem.Book.Quantity)
            {
                return Json(new { success = false, message = $"Only {cartItem.Book.Quantity} copies of '{cartItem.Book.Title}' are available." });
            }

            cartItem.Quantity = quantity;
            await _context.SaveChangesAsync();

            // Broadcast updated cart count to all clients
            int cartCount = await _context.Carts
                .Where(c => c.UserId == user.Id)
                .Select(c => c.BookId)
                .Distinct()
                .CountAsync();
            await _orderHubContext.Clients.All.SendAsync("UpdateCartCount", cartCount);

            return Json(new { success = true, message = "Cart updated successfully." });
        }

        // GET: Cart/GetCartItem?bookId={id}
        [HttpGet]
        public async Task<IActionResult> GetCartItem(int bookId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { exists = false });
            }

            var cartItem = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.BookId == bookId);

            return Json(new { exists = cartItem != null, quantity = cartItem?.Quantity ?? 0 });
        }

        // POST: Cart/RemoveFromCart
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(int bookId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for removing book {BookId} from cart.", bookId);
                return NotFound("User not found.");
            }

            var cartEntry = await _context.Carts
                .FirstOrDefaultAsync(c => c.UserId == user.Id && c.BookId == bookId);
            if (cartEntry == null)
            {
                TempData["ErrorMessage"] = "Book not found in your cart.";
                return RedirectToAction(nameof(Cart));
            }

            _context.Carts.Remove(cartEntry);
            await _context.SaveChangesAsync();

            // Broadcast updated cart count to all clients
            int cartCount = await _context.Carts
                .Where(c => c.UserId == user.Id)
                .Select(c => c.BookId)
                .Distinct()
                .CountAsync();
            await _orderHubContext.Clients.All.SendAsync("UpdateCartCount", cartCount);

            TempData["SuccessMessage"] = "Book removed from your cart.";
            return RedirectToAction(nameof(Cart));
        }

        // GET: Cart/Checkout
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> Checkout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for checkout view.");
                return NotFound("User not found.");
            }

            var cartItems = await _context.Carts
                .Where(c => c.UserId == user.Id)
                .Include(c => c.Book)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction(nameof(Cart));
            }

            // Check if all books in the cart are in stock
            foreach (var item in cartItems)
            {
                if (item.Book.Quantity < item.Quantity)
                {
                    TempData["ErrorMessage"] = $"Not enough stock for '{item.Book.Title}'. Available: {item.Book.Quantity}, Requested: {item.Quantity}.";
                    return RedirectToAction(nameof(Cart));
                }
            }

            // Load timed discounts for the books in the cart
            var bookIds = cartItems.Select(c => c.BookId).ToList();
            var timedDiscounts = await _context.TimedDiscounts
                .Where(td => bookIds.Contains(td.BookId))
                .ToListAsync();

            // Calculate total price with timed discounts
            decimal totalPrice = 0;
            var cartItemsWithDiscounts = new List<(UserCartItem CartItem, decimal DiscountedPrice, bool OnSale)>();

            foreach (var item in cartItems)
            {
                decimal bookPrice = item.Book.Price;
                bool onSale = false;
                decimal discountedPrice = bookPrice;

                // Check for an active timed discount
                var discount = timedDiscounts.FirstOrDefault(td => td.BookId == item.BookId && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                if (discount != null)
                {
                    discountedPrice = bookPrice * (1 - discount.DiscountPercentage);
                    onSale = discount.OnSaleFlag;
                }

                totalPrice += discountedPrice * item.Quantity;
                cartItemsWithDiscounts.Add((item, discountedPrice, onSale));
            }

            // Calculate quantity and loyalty discounts
            int orderCount = await _context.Orders
                .CountAsync(o => o.UserId == user.Id && !o.IsCancelled && o.Status != "Received");
            int totalItems = cartItems.Sum(c => c.Quantity);
            decimal quantityDiscount = totalItems >= 5 ? 0.05m : 0m; // 5% if 5+ items
            decimal loyaltyDiscount = orderCount >= 10 ? 0.10m : 0m; // 10% if 10+ orders

            // Combine discounts (stackable with timed discounts applied first)
            decimal totalDiscount = quantityDiscount + loyaltyDiscount - (quantityDiscount * loyaltyDiscount);
            decimal discountAmount = totalPrice * totalDiscount;
            decimal finalPrice = totalPrice - discountAmount;

            // Pass values to the view
            ViewBag.TotalPrice = totalPrice;
            ViewBag.DiscountAmount = discountAmount;
            ViewBag.FinalPrice = finalPrice;
            ViewBag.QuantityDiscount = quantityDiscount * 100;
            ViewBag.LoyaltyDiscount = loyaltyDiscount * 100;
            ViewBag.OrderCount = orderCount;
            ViewBag.TotalItems = totalItems;

            return View("~/Views/User/Checkout.cshtml", cartItemsWithDiscounts);
        }

        // POST: Cart/CheckoutConfirmed
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> CheckoutConfirmed()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for checkout confirmation.");
                return NotFound("User not found.");
            }

            var cartItems = await _context.Carts
                .Where(c => c.UserId == user.Id)
                .Include(c => c.Book)
                .ToListAsync();

            if (!cartItems.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction(nameof(Cart));
            }

            try
            {
                // Re-validate stock availability to prevent race conditions
                foreach (var item in cartItems)
                {
                    if (item.Book.Quantity < item.Quantity)
                    {
                        TempData["ErrorMessage"] = $"Not enough stock for '{item.Book.Title}'. Available: {item.Book.Quantity}, Requested: {item.Quantity}.";
                        return RedirectToAction(nameof(Cart));
                    }
                }

                // Calculate total price with discounts for the bill
                decimal totalPrice = 0;
                var bookIds = cartItems.Select(c => c.BookId).ToList();
                var timedDiscounts = await _context.TimedDiscounts
                    .Where(td => bookIds.Contains(td.BookId))
                    .ToListAsync();

                var orders = new List<OrderModel>();

                foreach (var item in cartItems)
                {
                    decimal bookPrice = item.Book.Price;
                    var discount = timedDiscounts.FirstOrDefault(td => td.BookId == item.BookId && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                    decimal discountedPrice = discount != null ? bookPrice * (1 - discount.DiscountPercentage) : bookPrice;
                    totalPrice += discountedPrice * item.Quantity;
                }

                int orderCount = await _context.Orders
                    .CountAsync(o => o.UserId == user.Id && !o.IsCancelled && o.Status != "Received");
                int totalItems = cartItems.Sum(c => c.Quantity);
                decimal quantityDiscount = totalItems >= 5 ? 0.05m : 0m;
                decimal loyaltyDiscount = orderCount >= 10 ? 0.10m : 0m;
                decimal totalDiscount = quantityDiscount + loyaltyDiscount - (quantityDiscount * loyaltyDiscount);
                decimal discountAmount = totalPrice * totalDiscount;
                decimal finalPrice = totalPrice - discountAmount;

                // Create orders and decrease quantities
                var billDetails = new StringBuilder();
                billDetails.AppendLine("<h2 style='font-family: Arial, sans-serif; color: #333;'>Order Confirmation for " + user.UserName + "</h2>");
                billDetails.AppendLine("<p style='font-family: Arial, sans-serif; color: #555;'>Order Date: " + DateTime.UtcNow.ToString("dd MMM yyyy HH:mm") + "</p>");
                billDetails.AppendLine("<p style='font-family: Arial, sans-serif; color: #555;'>User ID: " + user.Id + "</p>");
                billDetails.AppendLine("<h3 style='font-family: Arial, sans-serif; color: #333;'>Order Details</h3>");
                billDetails.AppendLine("<table border='1' style='border-collapse: collapse; width: 100%; font-family: Arial, sans-serif; color: #555;'>");
                billDetails.AppendLine("<tr style='background-color: #f2f2f2;'><th style='padding: 8px;'>Book Title</th><th style='padding: 8px;'>Author</th><th style='padding: 8px;'>Quantity</th><th style='padding: 8px;'>Original Price</th><th style='padding: 8px;'>Discounted Price</th><th style='padding: 8px;'>Claim Code</th></tr>");

                foreach (var item in cartItems)
                {
                    decimal bookPrice = item.Book.Price;
                    var discount = timedDiscounts.FirstOrDefault(td => td.BookId == item.BookId && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                    decimal discountedPrice = discount != null ? bookPrice * (1 - discount.DiscountPercentage) : bookPrice;
                    decimal itemTotalPrice = discountedPrice * item.Quantity;
                    decimal itemDiscountAmount = itemTotalPrice * totalDiscount;
                    decimal itemFinalPrice = itemTotalPrice - itemDiscountAmount;

                    var order = new OrderModel
                    {
                        UserId = user.Id,
                        BookId = item.BookId,
                        OrderDate = DateTime.UtcNow,
                        TotalPrice = itemFinalPrice,
                        Quantity = item.Quantity,
                        IsCancelled = false,
                        ClaimCode = GenerateClaimCode(),
                        Status = "Placed"
                    };
                    _context.Orders.Add(order);
                    orders.Add(order);

                    // Decrease book quantity
                    item.Book.Quantity -= item.Quantity;

                    // Add order details to email body
                    billDetails.AppendLine("<tr>");
                    billDetails.AppendLine($"<td style='padding: 8px;'>{item.Book.Title}</td>");
                    billDetails.AppendLine($"<td style='padding: 8px;'>{item.Book.Author}</td>");
                    billDetails.AppendLine($"<td style='padding: 8px;'>{order.Quantity}</td>");
                    billDetails.AppendLine($"<td style='padding: 8px;'>${itemTotalPrice:F2}</td>");
                    billDetails.AppendLine($"<td style='padding: 8px;'>${(discount != null ? itemFinalPrice : itemTotalPrice):F2}</td>");
                    billDetails.AppendLine($"<td style='padding: 8px;'>{order.ClaimCode}</td>");
                    billDetails.AppendLine("</tr>");
                }

                billDetails.AppendLine("</table>");

                // Add billing summary
                billDetails.AppendLine("<h3 style='font-family: Arial, sans-serif; color: #333; margin-top: 20px;'>Billing Summary</h3>");
                billDetails.AppendLine("<p style='font-family: Arial, sans-serif; color: #555;'>Total Items: " + totalItems + "</p>");
                billDetails.AppendLine("<p style='font-family: Arial, sans-serif; color: #555;'>Total Price: $" + totalPrice.ToString("F2") + "</p>");
                billDetails.AppendLine("<p style='font-family: Arial, sans-serif; color: #555;'>Discount Applied: $" + discountAmount.ToString("F2") + "</p>");
                billDetails.AppendLine("<p style='font-family: Arial, sans-serif; color: #555; font-weight: bold;'>Final Price: $" + finalPrice.ToString("F2") + "</p>");
                billDetails.AppendLine("<p style='font-family: Arial, sans-serif; color: #555; margin-top: 20px;'>Please present your user ID and claim code at the store for in-store pickup.</p>");

                // Send the email
                try
                {
                    await SendEmailAsync(user.Email, "Your BookHive Order Confirmation", billDetails.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send order confirmation email to {Email}", user.Email);
                    TempData["ErrorMessage"] = "Order placed successfully, but failed to send confirmation email. Check your orders for details.";
                }

                _context.Carts.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                // Get the total number of orders placed (across all users)
                int totalOrderCount = await _context.Orders.CountAsync();

                // Broadcast the order placement message with the order count
                foreach (var item in cartItems)
                {
                    string message = $"Order for '{item.Book.Title}' by {user.FirstName} {user.LastName} has been placed! Order #{totalOrderCount}";
                    await _hubContext.Clients.All.SendAsync("ReceiveAnnouncement", message);
                }

                // Broadcast updated order count to all clients
                int updatedOrderCount = await _context.Orders
                    .Where(o => o.UserId == user.Id && !o.IsCancelled && o.Status != "Received")
                    .CountAsync();
                await _orderHubContext.Clients.All.SendAsync("UpdateOrderCount", updatedOrderCount);

                TempData["SuccessMessage"] = "Order placed successfully! A confirmation email has been sent with your claim code and billing details.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming checkout for user {UserId}", user.Id);
                TempData["ErrorMessage"] = "An error occurred while placing your order.";
            }

            return RedirectToAction("MyOrders", "Orders");
        }

        // GET: Cart/Whitelist
        public async Task<IActionResult> Whitelist()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for whitelist view.");
                return NotFound("User not found.");
            }

            var whitelistedBooks = await _context.Whitelists
                .Where(w => w.UserId == user.Id)
                .Include(w => w.Book)
                .Select(w => w.Book)
                .ToListAsync();

            // Load timed discounts for the books in the whitelist
            var bookIds = whitelistedBooks.Select(b => b.Id).ToList();
            var timedDiscounts = await _context.TimedDiscounts
                .Where(td => bookIds.Contains(td.BookId))
                .ToListAsync();

            // Create view models with discount information
            var booksWithDiscounts = whitelistedBooks.Select(book =>
            {
                var bookPrice = book.Price;
                var discount = timedDiscounts.FirstOrDefault(td => td.BookId == book.Id && td.StartDate <= DateTime.UtcNow && td.ExpiresAt >= DateTime.UtcNow);
                return new DiscountCatalogEntry
                {
                    Book = book,
                    OnSaleFlag = discount?.OnSaleFlag ?? false,
                    IsDiscountActive = discount != null,
                    DiscountedPrice = discount != null ? bookPrice * (1 - discount.DiscountPercentage) : bookPrice
                };
            }).ToList();

            return View("~/Views/User/Whitelist.cshtml", booksWithDiscounts);
        }

        // POST: Cart/RemoveFromWhitelist
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromWhitelist(int bookId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for removing book {BookId} from whitelist.", bookId);
                return NotFound("User not found.");
            }

            var whitelistEntry = await _context.Whitelists
                .FirstOrDefaultAsync(w => w.UserId == user.Id && w.BookId == bookId);
            if (whitelistEntry == null)
            {
                TempData["ErrorMessage"] = "Book not found in your whitelist.";
                return RedirectToAction(nameof(Whitelist));
            }

            _context.Whitelists.Remove(whitelistEntry);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Book removed from your whitelist.";
            return RedirectToAction(nameof(Whitelist));
        }

        // Helper method to generate a unique claim code
        private string GenerateClaimCode()
        {
            return Guid.NewGuid().ToString().Substring(0, 8).ToUpper(); // Simple 8-character claim code
        }

        // Helper method to send email
        private async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrEmpty(email))
            {
                throw new ArgumentNullException(nameof(email), "Email address cannot be null or empty.");
            }

            var smtpHost = _configuration["Smtp:Host"];
            var smtpPort = _configuration["Smtp:Port"];
            var smtpUsername = _configuration["Smtp:Username"];
            var smtpPassword = _configuration["Smtp:Password"];

            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpPort) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
            {
                throw new InvalidOperationException("SMTP configuration is missing or incomplete in appsettings.json.");
            }

            var smtpClient = new SmtpClient(smtpHost)
            {
                Port = int.Parse(smtpPort),
                Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                EnableSsl = true,
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpUsername),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true,
            };
            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage);
        }
    
     }
}
