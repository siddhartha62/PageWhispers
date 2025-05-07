using System.Net.Mail;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PageWhispers.Data;
using PageWhispers.Hubs;
using PageWhispers.Model;

namespace PageWhispers.Controllers
{
    public class AdminPanelController : Controller
    {
        private readonly UserManager<UserAccount> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AdminPanelController> _logger;
        private readonly IHubContext<AnnouncementHub> _announcementHub;

        public AdminPanelController(
            UserManager<UserAccount> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<AdminPanelController> logger,
            IHubContext<AnnouncementHub> announcementHub)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _announcementHub = announcementHub ?? throw new ArgumentNullException(nameof(announcementHub));
        }

        // GET: Admin/Index
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            foreach (var user in users)
            {
                user.Roles = (await _userManager.GetRolesAsync(user)).ToList(); // Populate Roles property
            }
            return View(users);
        }

        // GET: Admin/ViewProfile/{id}
        public async Task<IActionResult> ViewProfile(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("User ID is required.");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.Roles = roles;
            return View(user);
        }

        // GET: Admin/SendDeletionEmail/{id} (Legacy method, redirects to SendDeletionNotice)
        [HttpGet]
        public async Task<IActionResult> SendDeletionEmail(string id)
        {
            // Redirect to SendDeletionNotice for consistency
            return RedirectToAction("SendDeletionNotice", new { id });
        }

        // GET: Admin/SendDeletionNotice/{id}
        public async Task<IActionResult> SendDeletionNotice(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("User ID is required.");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin == null || currentAdmin.Id == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            var model = new UserAccountDeletionModel
            {
                UserId = user.Id
            };

            return View(model);
        }

        // POST: Admin/SendDeletionNotice
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendDeletionNotice(UserAccountDeletionModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            try
            {
                if (string.IsNullOrEmpty(user.Email))
                {
                    TempData["ErrorMessage"] = "User email is not set.";
                    return RedirectToAction(nameof(Index));
                }

                await SendEmailAsync(
                    user.Email,
                    "Deletion Notice from BookHive",
                    $"<p>Dear {user.FirstName ?? "User"},</p>" +
                    $"<p>You have received a deletion notice from the BookHive Admin:</p>" +
                    $"<p>{model.Message}</p>" +
                    $"<p>Please respond to this email or take appropriate action to avoid account deletion.</p>" +
                    $"<p>Best regards,<br>BookHive Admin</p>"
                );

                TempData["PendingDeletionUserId"] = user.Id;
                TempData["SuccessMessage"] = $"Deletion notice sent to {user.Email}. Waiting for user response.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send deletion notice to {Email}", user.Email);
                TempData["ErrorMessage"] = $"Failed to send deletion notice: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/CancelDeletion/{id}
        public async Task<IActionResult> CancelDeletion(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("User ID is required.");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            try
            {
                if (string.IsNullOrEmpty(user.Email))
                {
                    TempData["ErrorMessage"] = "User email is not set.";
                    return RedirectToAction(nameof(Index));
                }

                await SendEmailAsync(
                    user.Email,
                    "Deletion Notice Canceled - BookHive",
                    $"<p>Dear {user.FirstName ?? "User"},</p>" +
                    $"<p>We are pleased to inform you that the deletion notice for your account has been canceled.</p>" +
                    $"<p>Thank you for addressing the concerns. Your account remains active.</p>" +
                    $"<p>Best regards,<br>BookHive Admin</p>"
                );

                TempData["SuccessMessage"] = $"Deletion notice for {user.Email} has been canceled.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send cancellation notice to {Email}", user.Email);
                TempData["ErrorMessage"] = $"Failed to send cancellation notice: {ex.Message}";
            }

            TempData["PendingDeletionUserId"] = null;
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/ConfirmDeletion/{id}
        public async Task<IActionResult> ConfirmDeletion(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("User ID is required.");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin == null || currentAdmin.Id == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(user);
        }

        // POST: Admin/DeleteUser (Used by both ConfirmDeletion flows)
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound("User ID is required.");
            }

            var currentAdmin = await _userManager.GetUserAsync(User);
            if (currentAdmin == null || currentAdmin.Id == id)
            {
                TempData["ErrorMessage"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            // Delete the user's profile image if it exists
            if (!string.IsNullOrEmpty(user.ProfileImageUrl))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", user.ProfileImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // Delete the user
            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = $"User {user.Email ?? user.UserName} has been deleted.";
                TempData["PendingDeletionUserId"] = null;
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to delete user: {string.Join(", ", result.Errors.Select(e => e.Description))}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/CreateStaff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["ErrorMessage"] = "Email and password are required.";
                return RedirectToAction(nameof(Index));
            }

            var user = new UserAccount
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                if (!await _roleManager.RoleExistsAsync("Staff"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Staff"));
                }
                await _userManager.AddToRoleAsync(user, "Staff");
                try
                {
                    await SendEmailAsync(
                        email,
                        "Staff Account Created - BookHive",
                        $"<p>Dear Staff,</p>" +
                        $"<p>Your staff account has been created. Please log in with the following credentials:</p>" +
                        $"<p>Email: {email}</p>" +
                        $"<p>Password: {password}</p>" +
                        $"<p>Best regards,<br>BookHive Admin</p>"
                    );
                    TempData["SuccessMessage"] = $"Staff account created for {email}.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send staff account creation email to {Email}", email);
                    TempData["SuccessMessage"] = $"Staff account created for {email}, but failed to send email: {ex.Message}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/SendAnnouncement
        [HttpPost]
        public async Task<IActionResult> SendAnnouncement(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                TempData["ErrorMessage"] = "Announcement message cannot be empty.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // Send announcement via SignalR
                await _announcementHub.Clients.All.SendAsync("ReceiveAnnouncement", message);
                TempData["SuccessMessage"] = "Announcement sent successfully!";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send announcement");
                TempData["ErrorMessage"] = $"Failed to send announcement: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/CreateTimedAnnouncement
        public IActionResult CreateTimedAnnouncement()
        {
            return View(new TimedAnnouncement());
        }

        // POST: Admin/CreateTimedAnnouncement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTimedAnnouncement(TimedAnnouncement model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (string.IsNullOrEmpty(model.Message))
            {
                ModelState.AddModelError("Message", "Message is required.");
                return View(model);
            }

            // Ensure ExpiresAt is in UTC
            if (model.ExpiresAt.Kind == DateTimeKind.Unspecified)
            {
                model.ExpiresAt = DateTime.SpecifyKind(model.ExpiresAt, DateTimeKind.Local).ToUniversalTime();
            }
            else if (model.ExpiresAt.Kind == DateTimeKind.Local)
            {
                model.ExpiresAt = model.ExpiresAt.ToUniversalTime();
            }

            // Validate expiration time is in the future
            if (model.ExpiresAt <= DateTime.UtcNow)
            {
                ModelState.AddModelError("ExpiresAt", "Expiration time must be in the future.");
                return View(model);
            }

            // Set CreatedAt to UTC
            model.CreatedAt = DateTime.UtcNow;

            _context.TimedAnnouncements.Add(model);
            await _context.SaveChangesAsync();

            // Send to all users via SignalR
            await _announcementHub.Clients.All.SendAsync("ReceiveTimedAnnouncement", model.Message, model.CreatedAt, model.ExpiresAt);

            TempData["SuccessMessage"] = "Timed announcement created successfully!";
            return RedirectToAction(nameof(Index));
        }

        private async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Attempted to send email with null or empty address.");
                return;
            }

            try
            {
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
                _logger.LogInformation("Email sent successfully to {Email} with subject: {Subject}", email, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email} with subject: {Subject}", email, subject);
                throw;
            }
        }
    }
}
