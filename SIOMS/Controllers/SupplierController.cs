using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SIOMS.Controllers
{
    public class SupplierController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;

        public SupplierController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Supplier
        public async Task<IActionResult> Index(string search)
        {
            try
            {
                var query = _context.Suppliers.AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();
                    query = query.Where(s =>
                        s.Name.ToLower().Contains(search) ||
                        (s.Phone != null && s.Phone.Contains(search)) ||
                        (s.Email != null && s.Email.ToLower().Contains(search)));
                }

                var suppliers = await query
                    .OrderBy(s => s.Name)
                    .ToListAsync();

                ViewBag.SearchTerm = search;
                return View(suppliers);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading suppliers: {ex.Message}";
                return View(new System.Collections.Generic.List<Supplier>());
            }
        }

        // GET: Admin/Supplier/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    TempData["Error"] = "Invalid supplier ID";
                    return RedirectToAction("Index");
                }

                var supplier = await _context.Suppliers
                    .Include(s => s.PurchaseOrders)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (supplier == null)
                {
                    TempData["Error"] = "Supplier not found";
                    return RedirectToAction("Index");
                }

                return View(supplier);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading supplier: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // GET: Admin/Supplier/Create
        public IActionResult Create()
        {
            return View(new Supplier());
        }

        // POST: Admin/Supplier/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Supplier supplier)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(supplier.Name))
                {
                    ModelState.AddModelError("Name", "Supplier name is required");
                    return View(supplier);
                }

                supplier.Name = supplier.Name.Trim();
                if (!string.IsNullOrWhiteSpace(supplier.Phone))
                    supplier.Phone = supplier.Phone.Trim();
                if (!string.IsNullOrWhiteSpace(supplier.Email))
                    supplier.Email = supplier.Email.Trim();
                if (!string.IsNullOrWhiteSpace(supplier.Address))
                    supplier.Address = supplier.Address.Trim();
                if (!string.IsNullOrWhiteSpace(supplier.City))
                    supplier.City = supplier.City.Trim();

                if (!string.IsNullOrWhiteSpace(supplier.Email))
                {
                    bool emailExists = await _context.Suppliers
                        .AnyAsync(s => s.Email.ToLower() == supplier.Email.ToLower());
                    
                    if (emailExists)
                    {
                        ModelState.AddModelError("Email", "A supplier with this email already exists");
                        return View(supplier);
                    }
                }

                supplier.CreatedAt = DateTime.Now;
                supplier.IsActive = true;
                
                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Supplier '{supplier.Name}' created successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating supplier: {ex.Message}";
                return View(supplier);
            }
        }

        // POST: Supplier/QuickCreate
        // ✅ AJAX Quick Create used from Sales Order / Purchase Order Create pages.
        // Name required. Contact Number is REQUIRED here (unlike Customer/Purchaser
        // QuickCreate) because whenever a Supplier is provided on an order, a contact
        // number for that Supplier must be captured - per requirement. The Supplier
        // field itself on the order remains optional; this only fires when the user
        // is actually creating a Supplier on the fly.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickCreate(string name, string? phone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return Json(new { success = false, message = "Supplier name is required" });
                }

                name = name.Trim();
                phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

                if (string.IsNullOrWhiteSpace(phone))
                {
                    return Json(new { success = false, message = "Contact number is required when creating a supplier" });
                }

                if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^\+?[1-9]\d{6,14}$"))
                {
                    return Json(new { success = false, message = "Please enter a valid international phone number for the supplier" });
                }

                bool nameExists = await _context.Suppliers
                    .AnyAsync(s => s.Name.ToLower() == name.ToLower());
                if (nameExists)
                {
                    return Json(new { success = false, message = $"A supplier named '{name}' already exists" });
                }

                var supplier = new Supplier
                {
                    Name = name,
                    Phone = phone,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = supplier.Id, name = supplier.Name });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error creating supplier: {ex.Message}" });
            }
        }

        // GET: Admin/Supplier/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    TempData["Error"] = "Invalid supplier ID";
                    return RedirectToAction("Index");
                }

                var supplier = await _context.Suppliers.FindAsync(id);
                
                if (supplier == null)
                {
                    TempData["Error"] = "Supplier not found";
                    return RedirectToAction("Index");
                }

                return View(supplier);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading supplier: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // POST: Admin/Supplier/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Supplier supplier)
        {
            try
            {
                if (id != supplier.Id)
                {
                    TempData["Error"] = "Invalid supplier ID";
                    return RedirectToAction("Index");
                }

                if (string.IsNullOrWhiteSpace(supplier.Name))
                {
                    ModelState.AddModelError("Name", "Supplier name is required");
                    return View(supplier);
                }

                supplier.Name = supplier.Name.Trim();
                if (!string.IsNullOrWhiteSpace(supplier.Phone))
                    supplier.Phone = supplier.Phone.Trim();
                if (!string.IsNullOrWhiteSpace(supplier.Email))
                    supplier.Email = supplier.Email.Trim();
                if (!string.IsNullOrWhiteSpace(supplier.Address))
                    supplier.Address = supplier.Address.Trim();
                if (!string.IsNullOrWhiteSpace(supplier.City))
                    supplier.City = supplier.City.Trim();

                var existingSupplier = await _context.Suppliers.FindAsync(id);
                if (existingSupplier == null)
                {
                    TempData["Error"] = "Supplier not found";
                    return RedirectToAction("Index");
                }

                if (!string.IsNullOrWhiteSpace(supplier.Email) && 
                    existingSupplier.Email?.ToLower() != supplier.Email.ToLower())
                {
                    bool emailExists = await _context.Suppliers
                        .AnyAsync(s => s.Email.ToLower() == supplier.Email.ToLower() && s.Id != id);
                    
                    if (emailExists)
                    {
                        ModelState.AddModelError("Email", "A supplier with this email already exists");
                        return View(supplier);
                    }
                }

                existingSupplier.Name = supplier.Name;
                existingSupplier.Phone = supplier.Phone;
                existingSupplier.Email = supplier.Email;
                existingSupplier.Address = supplier.Address;
                existingSupplier.City = supplier.City;
                existingSupplier.IsActive = supplier.IsActive;

                _context.Update(existingSupplier);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Supplier '{supplier.Name}' updated successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating supplier: {ex.Message}";
                return View(supplier);
            }
        }

        // GET: Admin/Supplier/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    TempData["Error"] = "Invalid supplier ID";
                    return RedirectToAction("Index");
                }

                var supplier = await _context.Suppliers
                    .Include(s => s.PurchaseOrders)
                    .FirstOrDefaultAsync(s => s.Id == id);
                if (supplier == null)
                {
                    TempData["Error"] = "Supplier not found";
                    return RedirectToAction("Index");
                }

                if (supplier.PurchaseOrders.Any())
                {
                    TempData["Error"] = $"Cannot delete supplier '{supplier.Name}' because it has purchase orders.";
                    return RedirectToAction("Index");
                }

                return View(supplier);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading supplier: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // POST: Admin/Supplier/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.PurchaseOrders)
                    .FirstOrDefaultAsync(s => s.Id == id);

                if (supplier == null)
                {
                    TempData["Error"] = "Supplier not found";
                    return RedirectToAction("Index");
                }

                if (supplier.PurchaseOrders.Any())
                {
                    TempData["Error"] = $"Cannot delete supplier '{supplier.Name}' because it has purchase orders.";
                    return RedirectToAction("Index");
                }
                _context.Suppliers.Remove(supplier);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Supplier '{supplier.Name}' deleted successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting supplier: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // POST: Admin/Supplier/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var supplier = await _context.Suppliers.FindAsync(id);
                if (supplier == null)
                    return Json(new { success = false, message = "Supplier not found" });

                supplier.IsActive = !supplier.IsActive;
                _context.Update(supplier);
                await _context.SaveChangesAsync();

                string message = supplier.IsActive ? 
                    "Supplier activated successfully!" : 
                    "Supplier deactivated successfully!";

                return Json(new { 
                    success = true, 
                    message = message,
                    isActive = supplier.IsActive 
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = $"Error: {ex.Message}"
                });
            }
        }

        private bool SupplierExists(int id)
        {
            return _context.Suppliers.Any(e => e.Id == id);
        }
    }
}
