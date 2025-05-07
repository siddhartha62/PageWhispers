using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PageWhispers.Model;
using System.Threading.Tasks;
using PageWhispers.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using System.Globalization;
using Microsoft.AspNetCore.SignalR;
using PageWhispers.Hubs;

namespace PageWhispers.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly UserManager<UserAccount> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly IHubContext<AnnouncementHub> _hubContext;
        private readonly IHubContext<OrderNotificationsHub> _orderHubContext;

        public OrdersController(
            UserManager<UserAccount> userManager,
            ApplicationDbContext context,
            ILogger<OrdersController> logger,
            IHubContext<AnnouncementHub> hubContext,
            IHubContext<OrderNotificationsHub> orderHubContext)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _orderHubContext = orderHubContext ?? throw new ArgumentNullException(nameof(orderHubContext));
        }

        // GET: Orders/MyOrders
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for viewing orders.");
                return NotFound("User not found.");
            }

            var orders = await _context.Orders
                .Where(o => o.UserId == user.Id).Include(o => o.Book).OrderByDescending(o => o.OrderDate).ToListAsync();

            return View("~/Views/User/MyOrders.cshtml", orders);
        }

        // POST: Orders/CancelOrder (Single order cancellation)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> CancelOrder(string userId, int bookId, DateTime orderDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Id != userId)
            {
                _logger.LogWarning("User not found or unauthorized to cancel order.");
                return NotFound("User not found or unauthorized.");
            }

            var order = await _context.Orders
                .Include(o => o.Book)
                .FirstOrDefaultAsync(o => o.UserId == userId && o.BookId == bookId && o.OrderDate == orderDate);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction(nameof(MyOrders));
            }

            // If the order is "Received" or "Cancelled", redirect to DeleteOrder
            if (order.Status == "Received" || order.IsCancelled)
            {
                return await DeleteOrder(userId, bookId, orderDate);
            }

            if (!order.IsCancellable)
            {
                TempData["ErrorMessage"] = "This order cannot be cancelled. It may be past the 24-hour cancellation window.";
                return RedirectToAction(nameof(MyOrders));
            }

            // Cancel the order
            order.IsCancelled = true;
            order.CancelledAt = DateTime.UtcNow;
            order.Status = "Cancelled";

            // Restore book quantity
            order.Book.Quantity += order.Quantity;

            await _context.SaveChangesAsync();

            // Broadcast updated order count to all clients
            int updatedOrderCount = await _context.Orders
                .Where(o => o.UserId == user.Id && !o.IsCancelled && o.Status != "Received")
                .CountAsync();
            await _orderHubContext.Clients.All.SendAsync("UpdateOrderCount", updatedOrderCount);

            TempData["SuccessMessage"] = "Order cancelled successfully.";
            return RedirectToAction(nameof(MyOrders));
        }

        // POST: Orders/DeleteOrder (Single order deletion)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> DeleteOrder(string userId, int bookId, DateTime orderDate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.Id != userId)
            {
                _logger.LogWarning("User not found or unauthorized to delete order.");
                return NotFound("User not found or unauthorized.");
            }

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.UserId == userId && o.BookId == bookId && o.OrderDate == orderDate);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction(nameof(MyOrders));
            }

            // Only allow deletion if the order is "Received" or "Cancelled"
            if (order.Status != "Received" && !order.IsCancelled)
            {
                TempData["ErrorMessage"] = "Only received or cancelled orders can be deleted.";
                return RedirectToAction(nameof(MyOrders));
            }

            // Delete the order
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            // Broadcast updated order count to all clients (though count won't change since this order was already "Received" or "Cancelled")
            int updatedOrderCount = await _context.Orders
                .Where(o => o.UserId == user.Id && !o.IsCancelled && o.Status != "Received")
                .CountAsync();
            await _orderHubContext.Clients.All.SendAsync("UpdateOrderCount", updatedOrderCount);

            TempData["SuccessMessage"] = "Order deleted successfully.";
            return RedirectToAction(nameof(MyOrders));
        }

        // POST: Orders/CancelOrders (Bulk cancellation)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> CancelOrders(string[] selectedOrders)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for cancelling orders.");
                return NotFound("User not found.");
            }

            if (selectedOrders == null || !selectedOrders.Any())
            {
                TempData["ErrorMessage"] = "No orders selected for cancellation.";
                return RedirectToAction(nameof(MyOrders));
            }

            int cancelledCount = 0;
            foreach (var selectedOrder in selectedOrders)
            {
                var parts = selectedOrder.Split('|');
                if (parts.Length != 3)
                {
                    _logger.LogWarning("Invalid selected order format: {SelectedOrder}", selectedOrder);
                    continue;
                }

                var userId = parts[0];
                if (userId != user.Id)
                {
                    _logger.LogWarning("Unauthorized attempt to cancel order for user {UserId}", userId);
                    continue;
                }

                if (!int.TryParse(parts[1], out var bookId))
                {
                    _logger.LogWarning("Invalid book ID in selected order: {BookId}", parts[1]);
                    continue;
                }

                if (!DateTime.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDateTime))
                {
                    _logger.LogWarning("Invalid order date in selected order: {OrderDate}", parts[2]);
                    continue;
                }

                var orderDate = parsedDateTime.Kind == DateTimeKind.Utc ? parsedDateTime : parsedDateTime.ToUniversalTime();

                var order = await _context.Orders
                    .Include(o => o.Book)
                    .FirstOrDefaultAsync(o => o.UserId == userId && o.BookId == bookId && o.OrderDate == orderDate);

                if (order == null)
                {
                    _logger.LogWarning("Order not found for user {UserId}, book {BookId}, date {OrderDate}", userId, bookId, orderDate);
                    continue;
                }

                // If the order is "Received" or "Cancelled", redirect to bulk delete
                if (order.Status == "Received" || order.IsCancelled)
                {
                    continue; // Handle in DeleteOrders
                }

                if (order.IsCancelled)
                {
                    _logger.LogWarning("Attempted to cancel an already cancelled order for user {UserId}, book {BookId}", userId, bookId);
                    continue;
                }

                if (!order.IsCancellable)
                {
                    _logger.LogWarning("Order is not cancellable for user {UserId}, book {BookId}", userId, bookId);
                    continue;
                }

                // Cancel the order
                order.IsCancelled = true;
                order.CancelledAt = DateTime.UtcNow;
                order.Status = "Cancelled";

                // Restore book quantity
                order.Book.Quantity += order.Quantity;

                cancelledCount++;
            }

            await _context.SaveChangesAsync();

            // Now handle deletions for "Received" or "Cancelled" orders
            await DeleteOrders(selectedOrders);

            // Broadcast updated order count to all clients
            int updatedOrderCount = await _context.Orders
                .Where(o => o.UserId == user.Id && !o.IsCancelled && o.Status != "Received")
                .CountAsync();
            await _orderHubContext.Clients.All.SendAsync("UpdateOrderCount", updatedOrderCount);

            if (cancelledCount > 0)
            {
                TempData["SuccessMessage"] = $"{cancelledCount} order(s) cancelled successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "No orders were cancelled. They may already be cancelled or past the cancellation window.";
            }

            return RedirectToAction(nameof(MyOrders));
        }

        // POST: Orders/DeleteOrders (Bulk deletion)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "User,Member")]
        public async Task<IActionResult> DeleteOrders(string[] selectedOrders)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for deleting orders.");
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(MyOrders));
            }

            if (selectedOrders == null || !selectedOrders.Any())
            {
                TempData["ErrorMessage"] = "No orders selected for deletion.";
                return RedirectToAction(nameof(MyOrders));
            }

            int deletedCount = 0;

            foreach (var selectedOrder in selectedOrders)
            {
                var parts = selectedOrder.Split('|');
                if (parts.Length != 3)
                {
                    _logger.LogWarning("Invalid selected order format: {SelectedOrder}", selectedOrder);
                    continue;
                }

                var userId = parts[0];
                if (userId != user.Id)
                {
                    _logger.LogWarning("Unauthorized attempt to delete order for user {UserId}", userId);
                    continue;
                }

                if (!int.TryParse(parts[1], out var bookId))
                {
                    _logger.LogWarning("Invalid book ID in selected order: {BookId}", parts[1]);
                    continue;
                }

                if (!DateTime.TryParse(parts[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDateTime))
                {
                    _logger.LogWarning("Invalid order date in selected order: {OrderDate}", parts[2]);
                    continue;
                }

                var orderDate = parsedDateTime.Kind == DateTimeKind.Utc ? parsedDateTime : parsedDateTime.ToUniversalTime();

                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.UserId == userId && o.BookId == bookId && o.OrderDate == orderDate);

                if (order == null)
                {
                    _logger.LogWarning("Order not found for user {UserId}, book {BookId}, date {OrderDate}", userId, bookId, orderDate);
                    continue;
                }

                // Only delete if the order is "Received" or "Cancelled"
                if (order.Status != "Received" && !order.IsCancelled)
                {
                    _logger.LogWarning("Order is not in a deletable state for user {UserId}, book {BookId}", userId, bookId);
                    continue;
                }

                // Delete the order
                _context.Orders.Remove(order);
                deletedCount++;
            }

            await _context.SaveChangesAsync();

            // Broadcast updated order count to all clients
            int updatedOrderCount = await _context.Orders
                .Where(o => o.UserId == user.Id && !o.IsCancelled && o.Status != "Received")
                .CountAsync();
            await _orderHubContext.Clients.All.SendAsync("UpdateOrderCount", updatedOrderCount);

            if (deletedCount > 0)
            {
                TempData["SuccessMessage"] = TempData["SuccessMessage"]?.ToString() + $" {deletedCount} order(s) deleted successfully.";
            }

            return RedirectToAction(nameof(MyOrders));
        }
    }
}