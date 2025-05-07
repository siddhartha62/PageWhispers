using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Mail;
using System.Net;
using System.Text.Encodings.Web;
using PageWhispers.Model;

namespace BookHive.Controllers
{
    [Authorize]
    public class UserProfilesController : Controller
    {
        private readonly UserManager<UserAccount> _userManager;
        private readonly SignInManager<UserAccount> _signInManager;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserProfilesController> _logger;

        public UserProfilesController(
            UserManager<UserAccount> userManager,
            SignInManager<UserAccount> signInManager,
            IWebHostEnvironment webHostEnvironment,
            IConfiguration configuration,
            ILogger<UserProfilesController> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _webHostEnvironment = webHostEnvironment ?? throw new ArgumentNullException(nameof(webHostEnvironment));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // GET: Profile/Index
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for profile index.");
                return NotFound("User not found.");
            }
            return View(user);
        }

        // GET: Profile/ViewProfile
        public async Task<IActionResult> ViewProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for profile view.");
                return NotFound("User not found.");
            }

            var model = new Profile
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ProfileImageUrl = user.ProfileImageUrl
            };

            return View(model);
        }

        // GET: Profile/Edit
        public async Task<IActionResult> Edit()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for profile edit.");
                return NotFound("User not found.");
            }

            var model = new Profile
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                ProfileImageUrl = user.ProfileImageUrl
            };

            return View(model);
        }

        // POST: Profile/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Profile model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for profile edit POST.");
                return NotFound("User not found.");
            }

            try
            {
                user.UserName = model.UserName ?? string.Empty;
                user.Email = model.Email ?? string.Empty;
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;

                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    // Validate image size (max 5MB)
                    if (model.ProfileImage.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ProfileImage", "Image size must not exceed 5MB.");
                        return View(model);
                    }

                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images/profile-pics");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(model.ProfileImage.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ProfileImage.CopyToAsync(fileStream);
                    }

                    // Delete old profile image if exists
                    if (!string.IsNullOrEmpty(user.ProfileImageUrl))
                    {
                        var oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, user.ProfileImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    user.ProfileImageUrl = "/images/profile-pics/" + uniqueFileName;
                }

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    await _signInManager.RefreshSignInAsync(user);
                    TempData["SuccessMessage"] = "Profile updated successfully.";
                    return RedirectToAction("ViewProfile");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", user.Id);
                TempData["ErrorMessage"] = "An error occurred while updating your profile.";
            }

            return View(model);
        }

        // GET: Profile/ChangePassword
        public IActionResult ChangePassword()
        {
            return View();
        }

        // POST: Profile/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for password change.");
                return NotFound("User not found.");
            }

            if (string.IsNullOrEmpty(user.Email))
            {
                TempData["ErrorMessage"] = "User email is not set.";
                return RedirectToAction("ViewProfile");
            }

            try
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetLink = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);

                if (resetLink == null)
                {
                    TempData["ErrorMessage"] = "Failed to generate password reset link.";
                    return RedirectToAction("ViewProfile");
                }

                await SendEmailAsync(
                    user.Email,
                    "Reset Your BookHive Password",
                    $"<p>Dear {user.FirstName ?? "User"},</p>" +
                    $"<p>Please reset your password by <a href='{HtmlEncoder.Default.Encode(resetLink)}'>clicking here</a>.</p>" +
                    "<p>This link will expire in 24 hours.</p>" +
                    "<p>Best regards,<br>BookHive Team</p>");

                TempData["SuccessMessage"] = "Password reset link sent to your email.";
                return RedirectToAction("ViewProfile");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending password reset email for user {UserId}", user.Id);
                TempData["ErrorMessage"] = "Failed to send password reset email.";
                return RedirectToAction("ViewProfile");
            }
        }

        private async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Attempted to send email with null or empty address.");
                throw new ArgumentNullException(nameof(email), "Email address cannot be null or empty.");
            }

            try
            {
                var smtpHost = _configuration["Smtp:Host"];
                var smtpPort = _configuration["Smtp:Port"];
                var smtpUsername = _configuration["Smtp:Username"];
                var smtpPassword = _configuration["Smtp:Password"];

                if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(smtpPort) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    _logger.LogError("SMTP configuration is missing or incomplete in appsettings.json.");
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