using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using PageWhispers.Data;
using PageWhispers.Model;
using PageWhispers.Hubs;

namespace BookHive.Controllers
{
    [Authorize(Roles = "Staff")]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<StaffController> _logger;
        private readonly UserManager<UserAccount> _userManager;
        private readonly IHubContext<OrderNotificationsHub> _hubContext;

        public StaffController(
            ApplicationDbContext context,
            ILogger<StaffController> logger,
            UserManager<UserAccount> userManager,
            IHubContext<OrderNotificationsHub> hubContext)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        // GET: Staff/FulfillOrder
        public IActionResult FulfillOrder()
        {
            return View(new FulfillOrderViewModel());
        }

        // POST: Staff/FulfillOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FulfillOrder(FulfillOrderViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var order = await _context.Orders
                .Include(o => o.Book)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.ClaimCode == model.ClaimCode);

            if (order == null)
            {
                ModelState.AddModelError("ClaimCode", "Invalid claim code.");
                return View(model);
            }

            if (order.IsCancelled)
            {
                ModelState.AddModelError("ClaimCode", "This order has been cancelled and cannot be fulfilled.");
                return View(model);
            }

            if (order.IsFulfilled)
            {
                ModelState.AddModelError("ClaimCode", "This order has already been fulfilled.");
                return View(model);
            }

            // Verify user ID
            if (order.UserId != model.UserId)
            {
                ModelState.AddModelError("UserId", "The user ID does not match the order.");
                return View(model);
            }

            // Display order details for confirmation
            model.Order = order;
            model.IsConfirmationStep = true;
            return View(model);
        }

        // POST: Staff/ConfirmFulfillOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmFulfillOrder(string claimCode, string userId)
        {
            var order = await _context.Orders
                .Include(o => o.Book)
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.ClaimCode == claimCode);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Invalid claim code.";
                return RedirectToAction(nameof(FulfillOrder));
            }

            if (order.UserId != userId)
            {
                TempData["ErrorMessage"] = "The user ID does not match the order.";
                return RedirectToAction(nameof(FulfillOrder));
            }

            if (order.IsCancelled)
            {
                TempData["ErrorMessage"] = "This order has been cancelled and cannot be fulfilled.";
                return RedirectToAction(nameof(FulfillOrder));
            }

            if (order.IsFulfilled)
            {
                TempData["ErrorMessage"] = "This order has already been fulfilled.";
                return RedirectToAction(nameof(FulfillOrder));
            }

            // Mark the order as fulfilled and set status to "Received"
            order.IsFulfilled = true;
            order.FulfilledAt = DateTime.UtcNow; // The setter will ensure UTC
            order.Status = "Received";

            await _context.SaveChangesAsync();

            // Broadcast the message to all clients
            string message = $"Order for '{order.Book.Title}' by {order.User.FirstName} {order.User.LastName} has been successfully fulfilled!";
            await _hubContext.Clients.All.SendAsync("ReceiveOrderNotification", message);

            TempData["SuccessMessage"] = "Order fulfilled successfully.";
            return RedirectToAction(nameof(FulfillOrder));
        }
    }

    public class FulfillOrderViewModel
    {
        public string ClaimCode { get; set; }
        public string UserId { get; set; }
        public OrderModel? Order { get; set; }
        public bool IsConfirmationStep { get; set; }
    }
}