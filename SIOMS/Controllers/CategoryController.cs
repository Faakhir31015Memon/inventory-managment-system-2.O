using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SIOMS.Controllers
{
    public class CategoryController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CategoryController> _logger;

        public CategoryController(ApplicationDbContext context, ILogger<CategoryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string search)
        {
            var query = _context.Categories.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(search) ||
                    (c.Description != null && c.Description.ToLower().Contains(search)));
            }

            ViewBag.SearchFilter = search;
            return View(await query.OrderBy(c => c.Name).ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound();

            return View(category);
        }

        public IActionResult Create()
        {
            return View(new Category());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Category category)
        {
            try
            {
                ModelState.Remove("Products");

                if (string.IsNullOrWhiteSpace(category.Name))
                    ModelState.AddModelError("Name", "Category name is required");

                if (!ModelState.IsValid)
                    return View(category);

                category.Name = category.Name.Trim();
                if (!string.IsNullOrWhiteSpace(category.Description))
                    category.Description = category.Description.Trim();

                category.CreatedAt = DateTime.Now;

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Category '{category.Name}' created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating category: {ex.Message}";
                return View(category);
            }
        }

        // ✅ AJAX Quick Create for Category - used from Product Create/Edit pages
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuickCreate(string name, string? description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return Json(new { success = false, message = "Category name is required" });

                name = name.Trim();

                bool nameExists = await _context.Categories
                    .AnyAsync(c => c.Name.ToLower() == name.ToLower());
                if (nameExists)
                    return Json(new { success = false, message = $"A category named '{name}' already exists" });

                var category = new Category
                {
                    Name = name,
                    Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                    CreatedAt = DateTime.Now
                };

                _context.Categories.Add(category);
                await _context.SaveChangesAsync();

                return Json(new { success = true, id = category.Id, name = category.Name });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error creating category: {ex.Message}" });
            }
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var category = await _context.Categories.FindAsync(id);
            if (category == null)
                return NotFound();

            return View(category);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Category category)
        {
            if (id != category.Id)
                return NotFound();

            try
            {
                ModelState.Remove("Products");

                if (string.IsNullOrWhiteSpace(category.Name))
                    ModelState.AddModelError("Name", "Category name is required");

                if (!ModelState.IsValid)
                    return View(category);

                var existingCategory = await _context.Categories.FindAsync(id);
                if (existingCategory == null)
                    return NotFound();

                existingCategory.Name = category.Name.Trim();
                existingCategory.Description = category.Description?.Trim();
                existingCategory.UpdatedAt = DateTime.Now;

                _context.Update(existingCategory);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Category updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating category: {ex.Message}";
                return View(category);
            }
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return NotFound();

            return View(category);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var category = await _context.Categories
                    .Include(c => c.Products)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (category == null)
                    return NotFound();

                if (category.Products.Any())
                {
                    TempData["Error"] = $"Cannot delete category '{category.Name}' because it has {category.Products.Count} products.";
                    return RedirectToAction(nameof(Index));
                }

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Category deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting category: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }
    }
}