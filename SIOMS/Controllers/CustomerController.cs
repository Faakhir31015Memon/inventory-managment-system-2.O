using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SIOMS.Controllers
{
    public class CustomerController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;

        public CustomerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Customers/Index
        public async Task<IActionResult> Index(string search, string customerType)
        {
            var customers = _context.Customers
                .Include(c => c.SalesOrders)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                customers = customers.Where(c =>
                    c.Name.ToLower().Contains(search) ||
                    (c.Phone != null && c.Phone.Contains(search)) ||
                    (c.Email != null && c.Email.ToLower().Contains(search)));
            }

            if (!string.IsNullOrEmpty(customerType))
            {
                customers = customers.Where(c => c.CustomerType == customerType);
            }

            ViewBag.SearchTerm = search;
            ViewBag.CustomerType = customerType;
            return View(await customers.OrderBy(c => c.Name).ToListAsync());
        }

        // GET: Customers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var customer = await _context.Customers
                .Include(c => c.SalesOrders)
                .ThenInclude(so => so.Items)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null)
                return NotFound();

            // Calculate statistics
            ViewBag.TotalOrders = customer.SalesOrders?.Count ?? 0;
            ViewBag.TotalSpent = customer.SalesOrders?
                .Where(so => so.Status == "Completed")
                .Sum(so => so.GrandTotal) ?? 0;
            ViewBag.AvgOrderValue = customer.SalesOrders?
                .Where(so => so.Status == "Completed")
                .DefaultIfEmpty()
                .Average(so => so?.GrandTotal) ?? 0;

            return View(customer);
        }

        // GET: Customers/Create
        public IActionResult Create()
        {
            return View(new Customer());
        }

        // POST: Customers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            try
            {
                // Remove navigation property from ModelState
                ModelState.Remove("SalesOrders");

                if (string.IsNullOrWhiteSpace(customer.Name))
                {
                    ModelState.AddModelError("Name", "Customer name is required");
                    return View(customer);
                }

                // Trim inputs
                customer.Name = customer.Name.Trim();
                if (!string.IsNullOrWhiteSpace(customer.Phone))
                    customer.Phone = customer.Phone.Trim();
                if (!string.IsNullOrWhiteSpace(customer.Email))
                    customer.Email = customer.Email.Trim();
                if (!string.IsNullOrWhiteSpace(customer.Address))
                    customer.Address = customer.Address.Trim();
                if (!string.IsNullOrWhiteSpace(customer.City))
                    customer.City = customer.City.Trim();

                // Check for duplicate email if provided
                if (!string.IsNullOrWhiteSpace(customer.Email))
                {
                    bool emailExists = await _context.Customers
                        .AnyAsync(c => c.Email.ToLower() == customer.Email.ToLower());
                    
                    if (emailExists)
                    {
                        ModelState.AddModelError("Email", "A customer with this email already exists");
                        return View(customer);
                    }
                }

                customer.CreatedAt = DateTime.Now;
                customer.IsActive = true;
                customer.SalesOrders = new List<SalesOrder>(); // Initialize collection

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = $"Customer '{customer.Name}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating customer: {ex.Message}";
                return View(customer);
            }
        }

        // POST: Customer/QuickCreate
        // ✅ AJAX Quick Create used from Sales Order Create page.
        // Name required, Contact Number optional. Returns JSON, no redirect/page refresh.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickCreate(string name, string? phone)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return Json(new { success = false, message = "Customer name is required" });
                }

                name = name.Trim();
                phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();

                bool nameExists = await _context.Customers
                    .AnyAsync(c => c.Name.ToLower() == name.ToLower());
                if (nameExists)
                {
                    return Json(new { success = false, message = $"A customer named '{name}' already exists" });
                }

                var customer = new Customer
                {
                    Name = name,
                    Phone = phone,
                    CustomerType = "Regular",
                    CreatedAt = DateTime.Now,
                    IsActive = true,
                    SalesOrders = new List<SalesOrder>()
                };

                _context.Customers.Add(customer);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = customer.Id, name = customer.Name });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error creating customer: {ex.Message}" });
            }
        }

        // GET: Customers/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                return NotFound();
                
            return View(customer);
        }

        // POST: Customers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer)
        {
            if (id != customer.Id)
                return NotFound();

            try
            {
                ModelState.Remove("SalesOrders");

                if (string.IsNullOrWhiteSpace(customer.Name))
                {
                    ModelState.AddModelError("Name", "Customer name is required");
                    return View(customer);
                }

                var existingCustomer = await _context.Customers.FindAsync(id);
                if (existingCustomer == null)
                {
                    TempData["Error"] = "Customer not found";
                    return RedirectToAction(nameof(Index));
                }

                // Trim inputs
                customer.Name = customer.Name.Trim();
                if (!string.IsNullOrWhiteSpace(customer.Phone))
                    customer.Phone = customer.Phone.Trim();
                if (!string.IsNullOrWhiteSpace(customer.Email))
                    customer.Email = customer.Email.Trim();
                if (!string.IsNullOrWhiteSpace(customer.Address))
                    customer.Address = customer.Address.Trim();
                if (!string.IsNullOrWhiteSpace(customer.City))
                    customer.City = customer.City.Trim();

                // Check for duplicate email (excluding current customer)
                if (!string.IsNullOrWhiteSpace(customer.Email) && 
                    existingCustomer.Email?.ToLower() != customer.Email.ToLower())
                {
                    bool emailExists = await _context.Customers
                        .AnyAsync(c => c.Email.ToLower() == customer.Email.ToLower() && c.Id != id);
                    
                    if (emailExists)
                    {
                        ModelState.AddModelError("Email", "A customer with this email already exists");
                        return View(customer);
                    }
                }

                // Update properties
                existingCustomer.Name = customer.Name;
                existingCustomer.Phone = customer.Phone;
                existingCustomer.Email = customer.Email;
                existingCustomer.Address = customer.Address;
                existingCustomer.City = customer.City;
                existingCustomer.CustomerType = customer.CustomerType;
                existingCustomer.IsActive = customer.IsActive;

                _context.Update(existingCustomer);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = $"Customer '{customer.Name}' updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CustomerExists(customer.Id))
                    return NotFound();
                else
                    throw;
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating customer: {ex.Message}";
                return View(customer);
            }
        }

        // GET: Customers/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var customer = await _context.Customers
                .Include(c => c.SalesOrders)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (customer == null)
                return NotFound();

            if (customer.SalesOrders != null && customer.SalesOrders.Any())
            {
                TempData["Error"] = "Cannot delete customer because it has sales orders.";
                return RedirectToAction(nameof(Index));
            }

            return View(customer);
        }

        // POST: Customers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var customer = await _context.Customers
                    .Include(c => c.SalesOrders)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (customer == null)
                {
                    TempData["Error"] = "Customer not found";
                    return RedirectToAction(nameof(Index));
                }

                // Check for related records
                if (customer.SalesOrders != null && customer.SalesOrders.Any())
                {
                    TempData["Error"] = "Cannot delete customer because it has sales orders.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Customers.Remove(customer);
                await _context.SaveChangesAsync();
                
                TempData["Success"] = $"Customer '{customer.Name}' deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting customer: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Customers/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            try
            {
                var customer = await _context.Customers.FindAsync(id);
                if (customer == null)
                    return Json(new { success = false, message = "Customer not found" });

                customer.IsActive = !customer.IsActive;
                _context.Update(customer);
                await _context.SaveChangesAsync();

                string message = customer.IsActive ? 
                    "Customer activated successfully!" : 
                    "Customer deactivated successfully!";

                return Json(new { 
                    success = true, 
                    message = message,
                    isActive = customer.IsActive 
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

        private bool CustomerExists(int id)
        {
            return _context.Customers.Any(e => e.Id == id);
        }
    }
}