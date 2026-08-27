using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SIOMS.Controllers
{
    public class PurchaserController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;

        public PurchaserController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Purchaser/Index
        public async Task<IActionResult> Index(string search)
        {
            var query = _context.Purchasers.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(search) ||
                    (p.Phone != null && p.Phone.Contains(search)) ||
                    (p.Email != null && p.Email.ToLower().Contains(search)));
            }

            ViewBag.SearchTerm = search;
            return View(await query.OrderBy(p => p.Name).ToListAsync());
        }

        // GET: Purchaser/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var purchaser = await _context.Purchasers
                .Include(p => p.PurchaseOrders)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchaser == null)
                return NotFound();

            return View(purchaser);
        }

        // GET: Purchaser/Create
        public IActionResult Create()
        {
            return View(new Purchaser());
        }

        // POST: Purchaser/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Purchaser purchaser)
        {
            try
            {
                // Remove navigation property from ModelState
                ModelState.Remove("PurchaseOrders");

                if (string.IsNullOrWhiteSpace(purchaser.Name))
                {
                    ModelState.AddModelError("Name", "Purchaser name is required");
                }

                if (string.IsNullOrWhiteSpace(purchaser.Phone))
                {
                    ModelState.AddModelError("Phone", "Contact number is required");
                }

                // Check for duplicate name
                if (!string.IsNullOrWhiteSpace(purchaser.Name))
                {
                    bool nameExists = await _context.Purchasers
                        .AnyAsync(p => p.Name.ToLower() == purchaser.Name.ToLower().Trim());
                    
                    if (nameExists)
                    {
                        ModelState.AddModelError("Name", "A purchaser with this name already exists");
                    }
                }

                // Check for duplicate email if provided
                if (!string.IsNullOrWhiteSpace(purchaser.Email))
                {
                    bool emailExists = await _context.Purchasers
                        .AnyAsync(p => p.Email.ToLower() == purchaser.Email.ToLower().Trim());
                    
                    if (emailExists)
                    {
                        ModelState.AddModelError("Email", "A purchaser with this email already exists");
                    }
                }

                if (!ModelState.IsValid)
                {
                    return View(purchaser);
                }

                purchaser.Name = purchaser.Name.Trim();
                purchaser.Phone = purchaser.Phone.Trim();
                if (!string.IsNullOrWhiteSpace(purchaser.Email))
                    purchaser.Email = purchaser.Email.Trim();
                if (!string.IsNullOrWhiteSpace(purchaser.Address))
                    purchaser.Address = purchaser.Address.Trim();

                purchaser.CreatedAt = DateTime.Now;
                purchaser.IsActive = true;

                _context.Purchasers.Add(purchaser);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Purchaser '{purchaser.Name}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating purchaser: {ex.Message}";
                return View(purchaser);
            }
        }

        // POST: Purchaser/QuickCreate
        // ✅ AJAX Quick Create used from Purchase Order Create page.
        // Name required, Contact Number optional (note: the full Purchaser/Create
        // form still requires Phone - this endpoint intentionally relaxes that
        // just for the quick-add flow, per requirement).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickCreate(string name, string? phone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return Json(new { success = false, message = "Purchaser name is required" });
                }

                name = name.Trim();
                phone = string.IsNullOrWhiteSpace(phone) ? string.Empty : phone.Trim();

                bool nameExists = await _context.Purchasers
                    .AnyAsync(p => p.Name.ToLower() == name.ToLower());
                if (nameExists)
                {
                    return Json(new { success = false, message = $"A purchaser named '{name}' already exists" });
                }

                var purchaser = new Purchaser
                {
                    Name = name,
                    Phone = phone,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Purchasers.Add(purchaser);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = purchaser.Id, name = purchaser.Name });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error creating purchaser: {ex.Message}" });
            }
        }

        // GET: Purchaser/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var purchaser = await _context.Purchasers.FindAsync(id);
            if (purchaser == null)
                return NotFound();

            return View(purchaser);
        }

        // POST: Purchaser/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Purchaser purchaser)
        {
            if (id != purchaser.Id)
                return NotFound();

            try
            {
                ModelState.Remove("PurchaseOrders");

                if (string.IsNullOrWhiteSpace(purchaser.Name))
                {
                    ModelState.AddModelError("Name", "Purchaser name is required");
                }

                if (string.IsNullOrWhiteSpace(purchaser.Phone))
                {
                    ModelState.AddModelError("Phone", "Contact number is required");
                }

                var existingPurchaser = await _context.Purchasers.FindAsync(id);
                if (existingPurchaser == null)
                    return NotFound();

                // Check for duplicate name (excluding current)
                if (!string.IsNullOrWhiteSpace(purchaser.Name) &&
                    existingPurchaser.Name.ToLower() != purchaser.Name.ToLower().Trim())
                {
                    bool nameExists = await _context.Purchasers
                        .AnyAsync(p => p.Name.ToLower() == purchaser.Name.ToLower().Trim() && p.Id != id);
                    
                    if (nameExists)
                    {
                        ModelState.AddModelError("Name", "A purchaser with this name already exists");
                    }
                }

                // Check for duplicate email if provided (excluding current)
                if (!string.IsNullOrWhiteSpace(purchaser.Email) &&
                    existingPurchaser.Email?.ToLower() != purchaser.Email.ToLower().Trim())
                {
                    bool emailExists = await _context.Purchasers
                        .AnyAsync(p => p.Email.ToLower() == purchaser.Email.ToLower().Trim() && p.Id != id);
                    
                    if (emailExists)
                    {
                        ModelState.AddModelError("Email", "A purchaser with this email already exists");
                    }
                }

                if (!ModelState.IsValid)
                {
                    return View(purchaser);
                }

                existingPurchaser.Name = purchaser.Name.Trim();
                existingPurchaser.Phone = purchaser.Phone.Trim();
                existingPurchaser.Email = purchaser.Email?.Trim();
                existingPurchaser.Address = purchaser.Address?.Trim();
                existingPurchaser.IsActive = purchaser.IsActive;

                _context.Update(existingPurchaser);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Purchaser '{purchaser.Name}' updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating purchaser: {ex.Message}";
                return View(purchaser);
            }
        }

        // GET: Purchaser/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var purchaser = await _context.Purchasers
                .Include(p => p.PurchaseOrders)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchaser == null)
                return NotFound();

            if (purchaser.PurchaseOrders != null && purchaser.PurchaseOrders.Any())
            {
                TempData["Error"] = "Cannot delete purchaser because it has purchase orders.";
                return RedirectToAction(nameof(Index));
            }

            return View(purchaser);
        }

        // POST: Purchaser/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var purchaser = await _context.Purchasers
                    .Include(p => p.PurchaseOrders)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (purchaser == null)
                    return NotFound();

                if (purchaser.PurchaseOrders != null && purchaser.PurchaseOrders.Any())
                {
                    TempData["Error"] = "Cannot delete purchaser because it has purchase orders.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Purchasers.Remove(purchaser);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Purchaser deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting purchaser: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Purchaser/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var purchaser = await _context.Purchasers.FindAsync(id);
                if (purchaser == null)
                    return Json(new { success = false, message = "Purchaser not found" });

                purchaser.IsActive = !purchaser.IsActive;
                _context.Update(purchaser);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = purchaser.IsActive ? "Purchaser activated!" : "Purchaser deactivated!",
                    isActive = purchaser.IsActive
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }
    }
}
