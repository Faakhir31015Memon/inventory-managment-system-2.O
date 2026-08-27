using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SIOMS.Controllers
{
    public class AlertController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;

        public AlertController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Alert
        public async Task<IActionResult> Index(bool? resolved)
        {
            // Sync AlertLogs from the current product stock state
            await SyncAlertsAsync();

            var alerts = _context.AlertLogs
                .Include(a => a.Product)
                .AsQueryable();

            if (resolved.HasValue)
            {
                alerts = alerts.Where(a => a.IsResolved == resolved.Value);
            }

            return View(await alerts.OrderByDescending(a => a.AlertDate).ToListAsync());
        }

        // ✅ GET: Admin/Alert/Details/5 — YE MISSING THA, AB ADD KAR DIYA
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var alert = await _context.AlertLogs
                .Include(a => a.Product)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (alert == null)
                return NotFound();

            return View(alert);
        }

        private async Task SyncAlertsAsync()
        {
            var products = await _context.Products.ToListAsync();
            var activeAlerts = await _context.AlertLogs
                .Where(a => !a.IsResolved)
                .ToListAsync();

            foreach (var product in products)
            {
                string? desiredType = product.StockQuantity == 0
                    ? "OutOfStock"
                    : product.StockQuantity <= product.MinStockLimit
                        ? "LowStock"
                        : null;

                var existing = activeAlerts.FirstOrDefault(a => a.ProductId == product.Id);

                if (desiredType == null)
                {
                    if (existing != null)
                    {
                        existing.IsResolved = true;
                        existing.ResolvedDate = DateTime.Now;
                        existing.ResolutionNotes = "Auto-resolved: stock replenished above minimum limit";
                    }
                    continue;
                }

                if (existing != null && existing.AlertType == desiredType)
                {
                    continue;
                }

                if (existing != null)
                {
                    existing.IsResolved = true;
                    existing.ResolvedDate = DateTime.Now;
                    existing.ResolutionNotes = "Auto-resolved: alert status changed";
                }

                _context.AlertLogs.Add(new AlertLog
                {
                    ProductId = product.Id,
                    AlertType = desiredType,
                    Message = desiredType == "OutOfStock"
                        ? $"{product.Name} is out of stock."
                        : $"{product.Name} stock ({product.StockQuantity}) is at or below the minimum limit ({product.MinStockLimit}).",
                    AlertDate = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        // POST: Admin/Alert/Resolve/5
        [HttpPost]
        public async Task<IActionResult> Resolve(int id, string resolutionNotes)
        {
            var alert = await _context.AlertLogs.FindAsync(id);
            if (alert == null) return NotFound();

            alert.IsResolved = true;
            alert.ResolvedDate = DateTime.Now;
            alert.ResolutionNotes = resolutionNotes;

            _context.Update(alert);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Alert resolved successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Alert/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var alert = await _context.AlertLogs.FindAsync(id);
            if (alert == null) return NotFound();

            // ✅ NEW: Active (unresolved) alert ko seedha delete na hone do
            if (!alert.IsResolved)
            {
                TempData["Error"] = "Cannot delete an active alert. Please resolve it first.";
                return RedirectToAction(nameof(Index));
            }

            _context.AlertLogs.Remove(alert);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Alert deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}