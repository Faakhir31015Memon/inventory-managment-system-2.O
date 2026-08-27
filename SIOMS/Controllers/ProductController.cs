using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SIOMS.Controllers
{
    public class ProductController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly SIOMS.Services.ExchangeRateService _exchangeRateService;

        public ProductController(ApplicationDbContext context, IWebHostEnvironment environment, SIOMS.Services.ExchangeRateService exchangeRateService)
        {
            _context = context;
            _environment = environment;
            _exchangeRateService = exchangeRateService;
        }

        // GET: Product/Index
        public async Task<IActionResult> Index(string search, int? categoryId)
        {
            try
            {
                var query = _context.Products
                    .Include(p => p.Category)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.ToLower();
                    query = query.Where(p =>
                        p.Name.ToLower().Contains(search) ||
                        (p.SKU != null && p.SKU.ToLower().Contains(search)) ||
                        (p.Description != null && p.Description.ToLower().Contains(search)));
                }

                if (categoryId.HasValue && categoryId > 0)
                {
                    query = query.Where(p => p.CategoryId == categoryId.Value);
                }

                ViewBag.SearchFilter = search;
                ViewBag.CategoryFilter = categoryId;
                ViewBag.Categories = await _context.Categories.ToListAsync();

                var products = await query
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                // ✅ Phase 5 - Automatic Currency Conversion: original values in the DB
                // are never touched; we only compute display-time converted amounts here.
                var settings = await _context.Settings.AsNoTracking().FirstOrDefaultAsync();
                var displayCurrency = settings?.Currency ?? "PKR";
                var rates = await _exchangeRateService.GetRatesAsync();

                ViewBag.DisplayCurrency = displayCurrency;
                ViewBag.ConvertedPrices = products.ToDictionary(
                    p => p.Id,
                    p => SIOMS.Services.ExchangeRateService.Convert(p.Price, p.OriginalCurrency, displayCurrency, rates));
                ViewBag.ConvertedCostPrices = products.ToDictionary(
                    p => p.Id,
                    p => SIOMS.Services.ExchangeRateService.Convert(p.CostPrice, p.OriginalCurrency, displayCurrency, rates));

                // Products that appear on a Completed sales or purchase order
                // can't be deleted (keeps historical order data intact).
                var productIds = products.Select(p => p.Id).ToList();
                var completedSalesProductIds = await _context.SalesOrderItems
                    .Where(i => productIds.Contains(i.ProductId) && i.SalesOrder.Status == "Completed")
                    .Select(i => i.ProductId)
                    .Distinct()
                    .ToListAsync();
                var completedPurchaseProductIds = await _context.PurchaseOrderItems
                    .Where(i => productIds.Contains(i.ProductId) && i.PurchaseOrder.Status == "Completed")
                    .Select(i => i.ProductId)
                    .Distinct()
                    .ToListAsync();
                ViewBag.LockedProductIds = new HashSet<int>(completedSalesProductIds.Concat(completedPurchaseProductIds));

                return View(products);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading products: {ex.Message}";
                ViewBag.SearchFilter = search;
                ViewBag.CategoryFilter = categoryId;
                ViewBag.Categories = await _context.Categories.ToListAsync();
                return View(new System.Collections.Generic.List<Product>());
            }
        }

        // GET: Product/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    TempData["Error"] = "Invalid product ID";
                    return RedirectToAction("Index");
                }

                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    TempData["Error"] = "Product not found";
                    return RedirectToAction("Index");
                }

                // ✅ Phase 5 - Automatic Currency Conversion: display-time only, original stays untouched.
                var settings = await _context.Settings.AsNoTracking().FirstOrDefaultAsync();
                var displayCurrency = settings?.Currency ?? "PKR";
                var rates = await _exchangeRateService.GetRatesAsync();

                ViewBag.DisplayCurrency = displayCurrency;
                ViewBag.ConvertedPrice = SIOMS.Services.ExchangeRateService.Convert(product.Price, product.OriginalCurrency, displayCurrency, rates);
                ViewBag.ConvertedCostPrice = SIOMS.Services.ExchangeRateService.Convert(product.CostPrice, product.OriginalCurrency, displayCurrency, rates);

                bool hasCompletedSalesOrder = await _context.SalesOrderItems
                    .AnyAsync(i => i.ProductId == product.Id && i.SalesOrder.Status == "Completed");
                bool hasCompletedPurchaseOrder = await _context.PurchaseOrderItems
                    .AnyAsync(i => i.ProductId == product.Id && i.PurchaseOrder.Status == "Completed");
                ViewBag.IsLocked = hasCompletedSalesOrder || hasCompletedPurchaseOrder;

                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading product: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // GET: Product/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.Currencies = SIOMS.Helpers.CurrencyFormatter.SupportedCurrencies;
            return View(new Product());
        }

        // POST: Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            try
            {
                ModelState.Remove("ImageFile");
                ModelState.Remove("ImageUrl");
                ModelState.Remove("SKU");
                ModelState.Remove("Category");

                if (string.IsNullOrWhiteSpace(product.Name))
                {
                    ModelState.AddModelError("Name", "Product name is required");
                }

                if (product.CategoryId <= 0)
                {
                    ModelState.AddModelError("CategoryId", "Please select a category");
                }

                if (product.Price <= 0)
                {
                    ModelState.AddModelError("Price", "Sell Price must be greater than 0");
                }

                if (product.CostPrice <= 0)
                {
                    ModelState.AddModelError("CostPrice", "Purchase Price must be greater than 0");
                }

                if (product.StockQuantity < 0)
                {
                    ModelState.AddModelError("StockQuantity", "Stock quantity cannot be negative");
                }

                if (product.MinStockLimit < 0)
                {
                    ModelState.AddModelError("MinStockLimit", "Minimum stock limit cannot be negative");
                }

                // ✅ Phase 2 - Multi-Currency: validate OriginalCurrency against supported list
                if (string.IsNullOrWhiteSpace(product.OriginalCurrency) ||
                    !SIOMS.Helpers.CurrencyFormatter.SupportedCurrencies.Contains(product.OriginalCurrency))
                {
                    ModelState.AddModelError("OriginalCurrency", "Please select a valid currency");
                }

                bool hasImageFile = product.ImageFile != null && product.ImageFile.Length > 0;
                bool hasImageUrl = !string.IsNullOrWhiteSpace(product.ImageUrl);

                if (hasImageFile && hasImageUrl)
                {
                    ModelState.AddModelError("ImageFile", "Please choose only one: upload file OR enter URL, not both");
                    ModelState.AddModelError("ImageUrl", "Please choose only one: upload file OR enter URL, not both");
                }
                else if (hasImageFile)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                    var fileExtension = Path.GetExtension(product.ImageFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("ImageFile", "Only image files (JPG, PNG, GIF, BMP, WEBP) are allowed");
                    }

                    if (product.ImageFile.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ImageFile", "Image file size cannot exceed 5MB");
                    }
                }
                else if (hasImageUrl)
                {
                    if (!Uri.IsWellFormedUriString(product.ImageUrl, UriKind.Absolute))
                    {
                        ModelState.AddModelError("ImageUrl", "Please enter a valid URL");
                    }
                }

                bool hasErrors = ModelState.Keys
                    .Where(key => key != "ImageFile" && key != "ImageUrl" && key != "SKU" && key != "Category")
                    .Any(key => ModelState[key].Errors.Any());

                if (hasErrors)
                {
                    ViewBag.Categories = await _context.Categories.ToListAsync();
                    ViewBag.Currencies = SIOMS.Helpers.CurrencyFormatter.SupportedCurrencies;
                    return View(product);
                }

                if (string.IsNullOrWhiteSpace(product.SKU))
                {
                    product.SKU = $"PROD-{DateTime.Now:yyyyMMddHHmmss}";
                }

                if (hasImageFile)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "products");

                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(product.ImageFile.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await product.ImageFile.CopyToAsync(fileStream);
                    }

                    product.ImageUrl = $"/uploads/products/{uniqueFileName}";
                }
                else if (hasImageUrl)
                {
                    product.ImageUrl = product.ImageUrl.Trim();
                }

                product.CreatedAt = DateTime.Now;
                product.IsActive = true;

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Product '{product.Name}' created successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating product: {ex.Message}";
                ViewBag.Categories = await _context.Categories.ToListAsync();
                    ViewBag.Currencies = SIOMS.Helpers.CurrencyFormatter.SupportedCurrencies;
                return View(product);
            }
        }

        // GET: Product/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    TempData["Error"] = "Invalid product ID";
                    return RedirectToAction("Index");
                }

                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    TempData["Error"] = "Product not found";
                    return RedirectToAction("Index");
                }

                ViewBag.Categories = await _context.Categories.ToListAsync();
                    ViewBag.Currencies = SIOMS.Helpers.CurrencyFormatter.SupportedCurrencies;
                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading product: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // POST: Product/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            try
            {
                if (id != product.Id)
                {
                    TempData["Error"] = "Invalid product ID";
                    return RedirectToAction("Index");
                }

                ModelState.Remove("ImageFile");
                ModelState.Remove("ImageUrl");
                ModelState.Remove("SKU");
                ModelState.Remove("Category");
                ModelState.Remove("StockQuantity");

                if (string.IsNullOrWhiteSpace(product.Name))
                {
                    ModelState.AddModelError("Name", "Product name is required");
                }

                if (product.CategoryId <= 0)
                {
                    ModelState.AddModelError("CategoryId", "Please select a category");
                }

                if (product.Price <= 0)
                {
                    ModelState.AddModelError("Price", "Sell Price must be greater than 0");
                }

                if (product.CostPrice <= 0)
                {
                    ModelState.AddModelError("CostPrice", "Purchase Price must be greater than 0");
                }

                if (product.MinStockLimit < 0)
                {
                    ModelState.AddModelError("MinStockLimit", "Minimum stock limit cannot be negative");
                }

                // ✅ Phase 2 - Multi-Currency: validate OriginalCurrency against supported list
                if (string.IsNullOrWhiteSpace(product.OriginalCurrency) ||
                    !SIOMS.Helpers.CurrencyFormatter.SupportedCurrencies.Contains(product.OriginalCurrency))
                {
                    ModelState.AddModelError("OriginalCurrency", "Please select a valid currency");
                }

                bool hasImageFile = product.ImageFile != null && product.ImageFile.Length > 0;
                bool hasImageUrl = !string.IsNullOrWhiteSpace(product.ImageUrl);

                if (hasImageFile && hasImageUrl)
                {
                    ModelState.AddModelError("ImageFile", "Please choose only one: upload file OR enter URL, not both");
                    ModelState.AddModelError("ImageUrl", "Please choose only one: upload file OR enter URL, not both");
                }
                else if (hasImageFile)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                    var fileExtension = Path.GetExtension(product.ImageFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("ImageFile", "Only image files (JPG, PNG, GIF, BMP, WEBP) are allowed");
                    }

                    if (product.ImageFile.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("ImageFile", "Image file size cannot exceed 5MB");
                    }
                }
                else if (hasImageUrl)
                {
                    if (!Uri.IsWellFormedUriString(product.ImageUrl, UriKind.Absolute))
                    {
                        ModelState.AddModelError("ImageUrl", "Please enter a valid URL");
                    }
                }

                bool hasErrors = ModelState.Keys
                    .Where(key => key != "ImageFile" && key != "ImageUrl" && key != "SKU" &&
                                 key != "Category" && key != "StockQuantity")
                    .Any(key => ModelState[key].Errors.Any());

                if (hasErrors)
                {
                    ViewBag.Categories = await _context.Categories.ToListAsync();
                    ViewBag.Currencies = SIOMS.Helpers.CurrencyFormatter.SupportedCurrencies;
                    return View(product);
                }

                var existingProduct = await _context.Products.FindAsync(id);
                if (existingProduct == null)
                {
                    TempData["Error"] = "Product not found";
                    return RedirectToAction("Index");
                }

                if (hasImageFile)
                {
                    if (!string.IsNullOrEmpty(existingProduct.ImageUrl) &&
                        existingProduct.ImageUrl.StartsWith("/uploads/products/"))
                    {
                        var oldImagePath = Path.Combine(_environment.WebRootPath, existingProduct.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                            System.IO.File.Delete(oldImagePath);
                    }

                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "products");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(product.ImageFile.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await product.ImageFile.CopyToAsync(fileStream);
                    }

                    existingProduct.ImageUrl = $"/uploads/products/{uniqueFileName}";
                }
                else if (hasImageUrl)
                {
                    if (!string.IsNullOrEmpty(existingProduct.ImageUrl) &&
                        existingProduct.ImageUrl.StartsWith("/uploads/products/"))
                    {
                        var oldImagePath = Path.Combine(_environment.WebRootPath, existingProduct.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                            System.IO.File.Delete(oldImagePath);
                    }

                    existingProduct.ImageUrl = product.ImageUrl.Trim();
                }
                else if (string.IsNullOrEmpty(product.ImageUrl) &&
                         !string.IsNullOrEmpty(existingProduct.ImageUrl) &&
                         !hasImageFile)
                {
                    if (existingProduct.ImageUrl.StartsWith("/uploads/products/"))
                    {
                        var oldImagePath = Path.Combine(_environment.WebRootPath, existingProduct.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldImagePath))
                            System.IO.File.Delete(oldImagePath);
                    }
                    existingProduct.ImageUrl = null;
                }

                existingProduct.Name = product.Name;
                existingProduct.Description = product.Description;
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.Price = product.Price;
                existingProduct.CostPrice = product.CostPrice;
                existingProduct.OriginalCurrency = product.OriginalCurrency;
                existingProduct.MinStockLimit = product.MinStockLimit;
                existingProduct.SKU = product.SKU;
                existingProduct.IsActive = product.IsActive;
                existingProduct.UpdatedAt = DateTime.Now;

                _context.Update(existingProduct);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Product '{product.Name}' updated successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating product: {ex.Message}";
                ViewBag.Categories = await _context.Categories.ToListAsync();
                    ViewBag.Currencies = SIOMS.Helpers.CurrencyFormatter.SupportedCurrencies;
                return View(product);
            }
        }

        // GET: Product/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            try
            {
                if (id == null || id <= 0)
                {
                    TempData["Error"] = "Invalid product ID";
                    return RedirectToAction("Index");
                }

                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    TempData["Error"] = "Product not found";
                    return RedirectToAction("Index");
                }

                return View(product);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error loading product: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        // POST: Product/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (product == null)
                {
                    TempData["Error"] = "Product not found";
                    return RedirectToAction("Index");
                }

                bool hasCompletedSalesOrder = await _context.SalesOrderItems
                    .AnyAsync(i => i.ProductId == id && i.SalesOrder.Status == "Completed");
                bool hasCompletedPurchaseOrder = await _context.PurchaseOrderItems
                    .AnyAsync(i => i.ProductId == id && i.PurchaseOrder.Status == "Completed");

                if (hasCompletedSalesOrder || hasCompletedPurchaseOrder)
                {
                    TempData["Error"] = $"Product '{product.Name}' cannot be deleted because it is part of a completed order.";
                    return RedirectToAction("Index");
                }

                if (!string.IsNullOrEmpty(product.ImageUrl) &&
                    product.ImageUrl.StartsWith("/uploads/products/"))
                {
                    var imagePath = Path.Combine(_environment.WebRootPath, product.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(imagePath))
                        System.IO.File.Delete(imagePath);
                }

                _context.Products.Remove(product);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Product '{product.Name}' deleted successfully";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting product: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        private bool ProductExists(int id)
        {
            return _context.Products.Any(e => e.Id == id);
        }
    }
}
