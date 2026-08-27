using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Helpers;
using SIOMS.Models;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SIOMS.Controllers
{
    public class SettingsController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;
        private static readonly Dictionary<string, string> CurrencySymbols = new()
        {
            { "PKR", "Rs." },
            { "USD", "$" },
            { "EUR", "€" },
            { "GBP", "£" },
            { "INR", "₹" },
            { "AED", "AED" }
        };

        private static readonly Regex PasswordPolicy =
            new(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,}$", RegexOptions.Compiled);

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Settings
        public async Task<IActionResult> Index()
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new Settings();
                _context.Settings.Add(settings);
                await _context.SaveChangesAsync();
            }

            ViewBag.CurrentUsername = settings.Username ?? "admin";

            return View(settings);
        }

        // POST: Admin/Settings
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(Settings model)
        {
            ModelState.Remove("CurrencySymbol");
            ModelState.Remove("Username");
            ModelState.Remove("PasswordHash");

            if (ModelState.IsValid)
            {
                try
                {
                    var settings = await _context.Settings.FirstOrDefaultAsync();

                    if (settings == null)
                    {
                        settings = new Settings();
                        _context.Settings.Add(settings);
                    }

                    settings.SiteName = model.SiteName;
                    settings.SiteEmail = model.SiteEmail;
                    settings.Currency = CurrencySymbols.ContainsKey(model.Currency) ? model.Currency : "PKR";
                    settings.CurrencySymbol = CurrencySymbols[settings.Currency];
                    settings.CompanyName = model.CompanyName;
                    settings.LowStockThreshold = model.LowStockThreshold;
                    settings.EnableEmailNotifications = model.EnableEmailNotifications;
                    settings.EnableSMSAlerts = model.EnableSMSAlerts;
                    settings.CompanyAddress = model.CompanyAddress;
                    settings.CompanyPhone = model.CompanyPhone;
                    settings.UpdatedAt = DateTime.Now;

                    await _context.SaveChangesAsync();

                    CurrencyFormatter.Symbol = settings.CurrencySymbol;

                    TempData["Success"] = "Settings updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error updating settings: {ex.Message}";
                }
            }

            var currentSettings = await _context.Settings.FirstOrDefaultAsync();
            ViewBag.CurrentUsername = currentSettings?.Username ?? "admin";
            return View(model);
        }

        // POST: Admin/Settings/ChangeUsername
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeUsername(string currentUsername, string newUsername)
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();

            if (settings == null || string.IsNullOrWhiteSpace(newUsername)
                || !(currentUsername ?? "").Trim().Equals(settings.Username, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Current username is incorrect.";
                return RedirectToAction(nameof(Index));
            }

            settings.Username = newUsername.Trim();
            settings.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("AdminUser", settings.Username);

            TempData["Success"] = "Username updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Settings/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();

            if (settings == null || !PasswordHasher.Verify(currentPassword ?? "", settings.PasswordHash))
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(newPassword) || !PasswordPolicy.IsMatch(newPassword))
            {
                TempData["Error"] = "New password must be at least 8 characters and include an uppercase letter, a lowercase letter, a number, and a special character.";
                return RedirectToAction(nameof(Index));
            }

            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToAction(nameof(Index));
            }

            settings.PasswordHash = PasswordHasher.Hash(newPassword);
            settings.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            HttpContext.Session.Clear();

            TempData["Success"] = "Password updated successfully! Please log in again.";
            return RedirectToAction("Login", "Admin");
        }
    }
}