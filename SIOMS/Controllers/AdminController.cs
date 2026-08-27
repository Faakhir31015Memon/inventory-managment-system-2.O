using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SIOMS.Data;
using SIOMS.Helpers;
using SIOMS.Models;
using SIOMS.ViewModels;
using System;
using System.Security.Cryptography;

namespace SIOMS.Controllers
{
    public class AdminController : Controller
    {
        private readonly ILogger<AdminController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AdminController(ILogger<AdminController> logger, ApplicationDbContext context, IConfiguration config)
        {
            _logger = logger;
            _context = context;
            _config = config;
        }

        private async Task<Settings> GetOrCreateSettingsAsync()
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new Settings
                {
                    Username = "admin",
                    PasswordHash = PasswordHasher.Hash("admin123")
                };
                _context.Settings.Add(settings);
                await _context.SaveChangesAsync();
            }
            else if (string.IsNullOrEmpty(settings.PasswordHash))
            {
                settings.Username = "admin";
                settings.PasswordHash = PasswordHasher.Hash("admin123");
                await _context.SaveChangesAsync();
            }
            return settings;
        }

        private static string GenerateOtp()
        {
            var bytes = new byte[4];
            RandomNumberGenerator.Fill(bytes);
            var value = BitConverter.ToUInt32(bytes, 0) % 1000000;
            return value.ToString("D6");
        }

        private static string MaskEmail(string email)
        {
            var atIndex = email.IndexOf('@');
            if (atIndex <= 1) return email;
            var visible = email.Substring(0, 2);
            return $"{visible}{new string('*', atIndex - 2)}{email[atIndex..]}";
        }

        public IActionResult Login()
        {
            _logger.LogInformation("Login page accessed");

            var user = HttpContext.Session.GetString("AdminUser");
            if (!string.IsNullOrEmpty(user))
            {
                _logger.LogInformation($"User {user} already logged in, redirecting to dashboard");
                return RedirectToAction("Index", "Dashboard");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(AdminLoginViewModel model)
        {
            _logger.LogInformation($"Login attempt - Username/Email: {model?.Username}");

            if (model == null)
            {
                ModelState.AddModelError("", "Please enter username/email and password.");
                return View();
            }

            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("", "Username/Email and password are required.");
                return View(model);
            }

            var settings = await GetOrCreateSettingsAsync();
            var identifier = model.Username.Trim();

            bool identifierMatches =
                identifier.Equals(settings.Username, StringComparison.OrdinalIgnoreCase) ||
                identifier.Equals(settings.SiteEmail, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(settings.RecoveryEmail) &&
                 identifier.Equals(settings.RecoveryEmail, StringComparison.OrdinalIgnoreCase));

            if (identifierMatches && PasswordHasher.Verify(model.Password, settings.PasswordHash))
            {
                try
                {
                    HttpContext.Session.SetString("AdminUser", settings.Username);
                    HttpContext.Session.SetString("LoginTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    HttpContext.Session.SetInt32("UserId", 1);

                    TempData["Success"] = $"Welcome back, {settings.Username}!";
                    _logger.LogInformation($"User {settings.Username} logged in successfully");

                    return RedirectToAction("Index", "Dashboard");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Session error during login");
                    ModelState.AddModelError("", "A session error occurred. Please try again.");
                    return View(model);
                }
            }
            else
            {
                _logger.LogWarning($"Invalid login attempt: {model.Username}");
                ModelState.AddModelError("", "Invalid username/email or password.");
                return View(model);
            }
        }

        public IActionResult Logout()
        {
            var username = HttpContext.Session.GetString("AdminUser");
            HttpContext.Session.Clear();
            Response.Cookies.Delete("SIOMS.Session");
            _logger.LogInformation($"User {username} logged out");
            TempData["Success"] = "You have been logged out successfully.";
            return RedirectToAction("Login", "Admin");
        }

        // ============== FORGOT PASSWORD FLOW ==============

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(AdminForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var settings = await GetOrCreateSettingsAsync();
            var identifier = model.Identifier.Trim();

            bool matches =
                identifier.Equals(settings.Username, StringComparison.OrdinalIgnoreCase) ||
                identifier.Equals(settings.SiteEmail, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(settings.RecoveryEmail) &&
                 identifier.Equals(settings.RecoveryEmail, StringComparison.OrdinalIgnoreCase));

            if (!matches)
            {
                ModelState.AddModelError("", "No account is registered with this username or email.");
                return View(model);
            }

            var otp = GenerateOtp();
            settings.OtpCodeHash = PasswordHasher.Hash(otp);
            settings.OtpExpiry = DateTime.Now.AddMinutes(10);
            await _context.SaveChangesAsync();

            var targetEmail = !string.IsNullOrEmpty(settings.RecoveryEmail) ? settings.RecoveryEmail : settings.SiteEmail;

            try
            {
                await EmailHelper.SendOtpEmailAsync(_config, targetEmail, settings.SiteName, otp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send OTP email");
                ModelState.AddModelError("", "We couldn't send the OTP email right now. Please try again in a moment.");
                return View(model);
            }

            HttpContext.Session.SetString("PasswordResetPending", "true");
            TempData["Info"] = $"An OTP has been sent to {MaskEmail(targetEmail)}. Please check your inbox.";

            return RedirectToAction("VerifyOtp");
        }

        [HttpGet]
        public IActionResult VerifyOtp()
        {
            if (HttpContext.Session.GetString("PasswordResetPending") != "true")
                return RedirectToAction("ForgotPassword");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(AdminVerifyOtpViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var settings = await GetOrCreateSettingsAsync();
            bool pending = HttpContext.Session.GetString("PasswordResetPending") == "true";

            bool valid = pending
                && !string.IsNullOrEmpty(settings.OtpCodeHash)
                && settings.OtpExpiry.HasValue
                && settings.OtpExpiry.Value > DateTime.Now
                && PasswordHasher.Verify(model.Otp.Trim(), settings.OtpCodeHash);

            if (!valid)
            {
                ModelState.AddModelError("", "The OTP you entered is invalid or has expired. Please try again.");
                return View(model);
            }

            HttpContext.Session.SetString("PasswordResetVerified", "true");
            HttpContext.Session.Remove("PasswordResetPending");

            return RedirectToAction("ResetPassword");
        }

        [HttpGet]
        public IActionResult ResetPassword()
        {
            if (HttpContext.Session.GetString("PasswordResetVerified") != "true")
                return RedirectToAction("ForgotPassword");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(AdminResetPasswordViewModel model)
        {
            if (HttpContext.Session.GetString("PasswordResetVerified") != "true")
                return RedirectToAction("ForgotPassword");

            if (!ModelState.IsValid) return View(model);

            var settings = await GetOrCreateSettingsAsync();

            settings.PasswordHash = PasswordHasher.Hash(model.NewPassword);
            settings.OtpCodeHash = null;
            settings.OtpExpiry = null;
            await _context.SaveChangesAsync();

            HttpContext.Session.Clear();

            TempData["Success"] = "Your password has been updated successfully. Please log in with your new password.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult CheckSession()
        {
            var user = HttpContext.Session.GetString("AdminUser");
            var loginTime = HttpContext.Session.GetString("LoginTime");
            var sessionId = HttpContext.Session.Id;

            return Json(new
            {
                isAuthenticated = !string.IsNullOrEmpty(user),
                username = user,
                loginTime = loginTime,
                sessionId = sessionId,
                sessionKeys = HttpContext.Session.Keys
            });
        }
    }
}