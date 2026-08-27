using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIOMS.Controllers
{
    public class OrderController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<OrderController> _logger;

        public OrderController(ApplicationDbContext context, ILogger<OrderController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ============================================================
        // ========== SALES ORDERS - COMPLETE CRUD ==========
        // ============================================================

        // GET: Order/SalesOrders
        public async Task<IActionResult> SalesOrders(string status, DateTime? fromDate, DateTime? toDate)
        {
            var orders = _context.SalesOrders
                .Include(so => so.Customer)
                .Include(so => so.Supplier)
                .Include(so => so.Items)
                .ThenInclude(i => i.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                orders = orders.Where(so => so.Status == status);
            }

            if (fromDate.HasValue && toDate.HasValue && toDate.Value.Date < fromDate.Value.Date)
            {
                ViewBag.DateRangeError = "\"To\" date can't be before \"From\" date. Showing results for \"From\" date onward only.";
                toDate = null;
            }

            if (fromDate.HasValue)
            {
                orders = orders.Where(so => so.OrderDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                orders = orders.Where(so => so.OrderDate <= toDate.Value);
            }

            ViewBag.StatusFilter = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(await orders.OrderByDescending(so => so.OrderDate).ToListAsync());
        }

        // GET: Order/SalesOrderDetails/5
        public async Task<IActionResult> SalesOrderDetails(int? id)
        {
            if (id == null)
                return NotFound();

            var order = await _context.SalesOrders
                .Include(so => so.Customer)
                .Include(so => so.Supplier)
                .Include(so => so.Items)
                .ThenInclude(i => i.Product)
                .Include(so => so.Payments)
                .FirstOrDefaultAsync(so => so.Id == id);

            if (order == null)
                return NotFound();
            ViewBag.CompanySettings = await _context.Settings.AsNoTracking().FirstOrDefaultAsync();
            return View(order);
        }

        // GET: Order/CreateSalesOrder
        public IActionResult CreateSalesOrder()
        {
            ViewBag.Customers = _context.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToList();

            ViewBag.Suppliers = _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.Products = _context.Products
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .ToList();

            var order = new SalesOrder
            {
                OrderDate = DateTime.Now,
                DiscountPercentage = 0,
                TaxAmount = 0,
                OrderNumber = "SO-" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                Status = "Processing"
            };

            return View(order);
        }

        // POST: Order/CreateSalesOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSalesOrder(SalesOrder order, [FromForm] List<SalesOrderItem> items)
        {
            order.Items = new List<SalesOrderItem>();

            if (order.DueDate.HasValue && order.DueDate.Value.Date < order.OrderDate.Date)
            {
                TempData["Error"] = "Due Date can't be before Order Date.";
                return RedirectToAction(nameof(CreateSalesOrder));
            }

            try
            {
                _logger.LogInformation("=== CREATE SALES ORDER STARTED ===");
                _logger.LogInformation($"CustomerId: {order.CustomerId}");
                _logger.LogInformation($"SupplierId: {order.SupplierId}");
                _logger.LogInformation($"Items Count: {items?.Count ?? 0}");

                // ✅ FIX: Don't filter out items with UnitPrice = 0
                var validItems = new List<SalesOrderItem>();
                if (items != null && items.Any())
                {
                    // Only filter by ProductId and Quantity
                    validItems = items.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();

                    // ✅ If UnitPrice is 0, fetch from product
                    foreach (var item in validItems)
                    {
                        if (item.UnitPrice <= 0)
                        {
                            var product = await _context.Products.FindAsync(item.ProductId);
                            if (product != null)
                            {
                                item.UnitPrice = product.Price;
                            }
                        }
                    }
                    _logger.LogInformation($"Items after filtering: {validItems.Count}");
                }

                // ✅ Remove duplicate ProductIds
                if (validItems.Count > 1)
                {
                    var uniqueItems = new List<SalesOrderItem>();
                    var seenProductIds = new HashSet<int>();

                    foreach (var item in validItems)
                    {
                        if (!seenProductIds.Contains(item.ProductId))
                        {
                            seenProductIds.Add(item.ProductId);
                            uniqueItems.Add(item);
                        }
                    }
                    validItems = uniqueItems;
                    _logger.LogInformation($"Unique Items after deduplication: {validItems.Count}");
                }

                // ✅ Remove ModelState for ALL items
                ModelState.Remove("Customer");
                ModelState.Remove("Supplier");
                ModelState.Remove("Items");
                ModelState.Remove("SalesOrder");
                ModelState.Remove("Product");
                ModelState.Remove("OrderNumber");

                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        ModelState.Remove($"items[{i}].SalesOrder");
                        ModelState.Remove($"items[{i}].Product");
                    }
                }

                // Customer is REQUIRED
                if (order.CustomerId <= 0)
                {
                    ModelState.AddModelError("CustomerId", "Please select a customer");
                }

                if (validItems == null || !validItems.Any())
                {
                    ModelState.AddModelError("", "Please add at least one item to the order");
                }

                if (validItems != null && validItems.Any())
                {
                    foreach (var item in validItems)
                    {
                        if (item.ProductId <= 0)
                        {
                            ModelState.AddModelError("", "Please select a product for all items");
                            break;
                        }
                        if (item.Quantity <= 0)
                        {
                            ModelState.AddModelError("", "Quantity must be greater than 0");
                            break;
                        }
                        if (item.UnitPrice <= 0)
                        {
                            ModelState.AddModelError("", "Unit price must be greater than 0");
                            break;
                        }

                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null && product.StockQuantity < item.Quantity)
                        {
                            ModelState.AddModelError("", $"Insufficient stock for {product.Name}. Available: {product.StockQuantity}");
                            break;
                        }
                    }
                }

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState is invalid");
                    foreach (var key in ModelState.Keys)
                    {
                        foreach (var error in ModelState[key].Errors)
                        {
                            _logger.LogWarning($"ModelState Error - {key}: {error.ErrorMessage}");
                        }
                    }

                    ViewBag.Customers = _context.Customers
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToList();
                    ViewBag.Suppliers = _context.Suppliers
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.Name)
                        .ToList();
                    ViewBag.Products = _context.Products
                        .Include(p => p.Category)
                        .OrderBy(p => p.Name)
                        .ToList();
                    return View(order);
                }

                // ✅ Calculate using validItems
                decimal subtotal = validItems.Sum(i => i.Quantity * i.UnitPrice);
                decimal discount = subtotal * (order.DiscountPercentage / 100);
                order.TotalAmount = subtotal - discount;
                order.GrandTotal = order.TotalAmount + order.TaxAmount;
                order.CreatedAt = DateTime.Now;
                order.Status = "Processing";
                order.StockUpdated = false;

                // Phase 6 - Partial Payment
                order.PaidAmount = 0;
                order.RemainingAmount = order.GrandTotal;
                order.PaymentStatus = "Unpaid";

                // SAVE ORDER FIRST
                _context.SalesOrders.Add(order);
                await _context.SaveChangesAsync();
                _logger.LogInformation($"Order ID after save: {order.Id}");

                if (order.Id == 0)
                {
                    _logger.LogError("Order ID is 0 - Save failed!");
                    TempData["Error"] = "Failed to save order. Please try again.";
                    ViewBag.Customers = _context.Customers
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToList();
                    ViewBag.Suppliers = _context.Suppliers
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.Name)
                        .ToList();
                    ViewBag.Products = _context.Products
                        .Include(p => p.Category)
                        .OrderBy(p => p.Name)
                        .ToList();
                    return View(order);
                }

                // Insert ONLY validItems
                foreach (var item in validItems)
                {
                    item.SalesOrderId = order.Id;
                    item.TotalPrice = item.Quantity * item.UnitPrice;

                    var itemProduct = await _context.Products.FindAsync(item.ProductId);
                    item.ItemCurrency = itemProduct?.OriginalCurrency ?? "PKR";

                    _context.SalesOrderItems.Add(item);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Sales order #{order.OrderNumber} created successfully!";
                return RedirectToAction(nameof(SalesOrders));
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR creating sales order: {ex.Message}");
                _logger.LogError($"STACK: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    _logger.LogError($"INNER EXCEPTION: {ex.InnerException.Message}");
                    _logger.LogError($"INNER STACK: {ex.InnerException.StackTrace}");
                }

                TempData["Error"] = $"Error creating sales order: {ex.Message}";
                ViewBag.Customers = _context.Customers
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToList();
                ViewBag.Suppliers = _context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .ToList();
                ViewBag.Products = _context.Products
                    .Include(p => p.Category)
                    .OrderBy(p => p.Name)
                    .ToList();
                return View(order);
            }
        }

        // GET: Order/EditSalesOrder/5
        public async Task<IActionResult> EditSalesOrder(int? id)
        {
            if (id == null)
                return NotFound();

            var order = await _context.SalesOrders
                .Include(so => so.Items)
                .Include(so => so.Payments)
                .FirstOrDefaultAsync(so => so.Id == id);

            if (order == null)
                return NotFound();

            ViewBag.Customers = _context.Customers
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .ToList();

            ViewBag.Suppliers = _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.Products = _context.Products
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .ToList();

            return View(order);
        }

        // POST: Order/EditSalesOrder/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSalesOrder(int id, SalesOrder order, [FromForm] List<SalesOrderItem> items)
        {
            if (id != order.Id)
                return NotFound();

            try
            {
                var existingOrder = await _context.SalesOrders
                    .Include(so => so.Items)
                    .FirstOrDefaultAsync(so => so.Id == id);

                if (existingOrder == null)
                    return NotFound();

                // ✅ Once an order is Completed (delivered) or Cancelled, its
                // items/details can no longer be changed here — only payment
                // can still be recorded (via RecordSalesPayment) until Paid.
                if (existingOrder.Status == "Completed" || existingOrder.Status == "Cancelled")
                {
                    TempData["Error"] = $"Cannot edit a {existingOrder.Status} order's details. Only payment can still be recorded until it is fully paid.";
                    return RedirectToAction(nameof(SalesOrders));
                }

                if (order.DueDate.HasValue && order.DueDate.Value.Date < order.OrderDate.Date)
                {
                    TempData["Error"] = "Due Date can't be before Order Date.";
                    return RedirectToAction(nameof(EditSalesOrder), new { id = id });
                }

                // ✅ FIX: Don't filter out items with UnitPrice = 0
                var validItems = new List<SalesOrderItem>();
                if (items != null && items.Any())
                {
                    validItems = items.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();

                    // ✅ If UnitPrice is 0, fetch from product
                    foreach (var item in validItems)
                    {
                        if (item.UnitPrice <= 0)
                        {
                            var product = await _context.Products.FindAsync(item.ProductId);
                            if (product != null)
                            {
                                item.UnitPrice = product.Price;
                            }
                        }
                    }
                }

                ModelState.Remove("Customer");
                ModelState.Remove("Supplier");
                ModelState.Remove("Items");
                ModelState.Remove("SalesOrder");
                ModelState.Remove("Product");
                ModelState.Remove("OrderNumber");

                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        ModelState.Remove($"items[{i}].SalesOrder");
                        ModelState.Remove($"items[{i}].Product");
                    }
                }

                if (order.CustomerId <= 0)
                {
                    ModelState.AddModelError("CustomerId", "Please select a customer");
                }

                if (validItems == null || !validItems.Any())
                {
                    ModelState.AddModelError("", "Please add at least one item to the order");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.Customers = _context.Customers
                        .Where(c => c.IsActive)
                        .OrderBy(c => c.Name)
                        .ToList();
                    ViewBag.Suppliers = _context.Suppliers
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.Name)
                        .ToList();
                    ViewBag.Products = _context.Products
                        .Include(p => p.Category)
                        .OrderBy(p => p.Name)
                        .ToList();
                    return View(order);
                }

                // STOCK HANDLING - Check if status changed
                var newStatus = order.Status;

                // If status changed to Completed and stock not updated yet
                if (newStatus == "Completed" && !existingOrder.StockUpdated)
                {
                    foreach (var item in validItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product == null || product.StockQuantity < item.Quantity)
                        {
                            TempData["Error"] = $"Insufficient stock for {product?.Name ?? "product"}.";
                            ViewBag.Customers = _context.Customers.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
                            ViewBag.Suppliers = _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
                            ViewBag.Products = _context.Products.Include(p => p.Category).OrderBy(p => p.Name).ToList();
                            return View(order);
                        }
                    }

                    foreach (var item in validItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity -= item.Quantity;
                            product.UpdatedAt = DateTime.Now;
                            _context.Products.Update(product);
                        }
                    }
                    existingOrder.StockUpdated = true;
                }
                // If status changed to Cancelled and stock was updated
                else if (newStatus == "Cancelled" && existingOrder.StockUpdated)
                {
                    foreach (var item in validItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            product.UpdatedAt = DateTime.Now;
                            _context.Products.Update(product);
                        }
                    }
                    existingOrder.StockUpdated = false;
                }

                // Remove existing items
                if (existingOrder.Items != null && existingOrder.Items.Any())
                {
                    _context.SalesOrderItems.RemoveRange(existingOrder.Items);
                }

                // Update order details
                existingOrder.CustomerId = order.CustomerId;
                existingOrder.SupplierId = order.SupplierId;
                existingOrder.OrderDate = order.OrderDate;
                existingOrder.Status = newStatus;
                existingOrder.DiscountPercentage = order.DiscountPercentage;
                existingOrder.TaxAmount = order.TaxAmount;
                existingOrder.Notes = order.Notes;
                existingOrder.DueDate = order.DueDate;
                existingOrder.UpdatedAt = DateTime.Now;

                decimal subtotal = validItems.Sum(i => i.Quantity * i.UnitPrice);
                decimal discount = subtotal * (order.DiscountPercentage / 100);
                decimal newTotalAmount = subtotal - discount;
                decimal newGrandTotal = newTotalAmount + order.TaxAmount;

                // ✅ Prevent data mismatch: don't let the new total drop below
                // what the customer has already paid (this used to silently
                // clamp RemainingAmount to 0 and mark the order "Paid" even
                // though the real order value was never fully collected).
                if (newGrandTotal < existingOrder.PaidAmount)
                {
                    TempData["Error"] = $"Cannot reduce order total ({newGrandTotal:N2}) below the amount already paid ({existingOrder.PaidAmount:N2}). Please adjust items/discount/tax so the total is at least {existingOrder.PaidAmount:N2}.";
                    ViewBag.Customers = _context.Customers.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
                    ViewBag.Suppliers = _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
                    ViewBag.Products = _context.Products.Include(p => p.Category).OrderBy(p => p.Name).ToList();
                    return View(order);
                }

                existingOrder.TotalAmount = newTotalAmount;
                existingOrder.GrandTotal = newGrandTotal;

                RecalculateSalesPaymentStatus(existingOrder);

                _context.Update(existingOrder);

                // Add new items
                foreach (var newItem in validItems)
                {
                    newItem.SalesOrderId = existingOrder.Id;
                    newItem.TotalPrice = newItem.Quantity * newItem.UnitPrice;

                    var editItemProduct = await _context.Products.FindAsync(newItem.ProductId);
                    newItem.ItemCurrency = editItemProduct?.OriginalCurrency ?? "PKR";

                    _context.SalesOrderItems.Add(newItem);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Sales order #{existingOrder.OrderNumber} updated successfully!";
                return RedirectToAction(nameof(SalesOrders));
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR editing sales order: {ex.Message}");
                TempData["Error"] = $"Error updating sales order: {ex.Message}";
                ViewBag.Customers = _context.Customers
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .ToList();
                ViewBag.Suppliers = _context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .ToList();
                ViewBag.Products = _context.Products
                    .Include(p => p.Category)
                    .OrderBy(p => p.Name)
                    .ToList();
                return View(order);
            }
        }

        // POST: Order/UpdateSalesOrderStatus/5
        [HttpPost]
        public async Task<IActionResult> UpdateSalesOrderStatus(int id, [FromBody] StatusUpdateModel model)
        {
            try
            {
                var order = await _context.SalesOrders
                    .Include(so => so.Items)
                    .FirstOrDefaultAsync(so => so.Id == id);

                if (order == null)
                    return Json(new { success = false, message = "Order not found" });

                // ✅ Once an order is Completed (delivered) or Cancelled, that is
                // final — status cannot change again from here.
                if (order.Status == "Completed" || order.Status == "Cancelled")
                {
                    return Json(new { success = false, message = $"This order is already {order.Status}, so its status can no longer be changed." });
                }

                var newStatus = model.Status;

                // Complete → Decrease Stock (only once)
                if (newStatus == "Completed" && !order.StockUpdated)
                {
                    foreach (var item in order.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product == null || product.StockQuantity < item.Quantity)
                        {
                            return Json(new { success = false, message = $"Insufficient stock to complete this order." });
                        }
                    }

                    foreach (var item in order.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity -= item.Quantity;
                            product.UpdatedAt = DateTime.Now;
                            _context.Products.Update(product);
                        }
                    }
                    order.StockUpdated = true;
                }
                // Cancelled → Restore Stock (only if it was deducted)
                else if (newStatus == "Cancelled" && order.StockUpdated)
                {
                    foreach (var item in order.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            product.UpdatedAt = DateTime.Now;
                            _context.Products.Update(product);
                        }
                    }
                    order.StockUpdated = false;
                }

                order.Status = newStatus;
                order.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Order status updated to {model.Status}" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR updating sales order status: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: Order/DeleteSalesOrder/5
        public async Task<IActionResult> DeleteSalesOrder(int? id)
        {
            if (id == null)
                return NotFound();

            var order = await _context.SalesOrders
                .Include(so => so.Customer)
                .Include(so => so.Supplier)
                .Include(so => so.Items)
                .FirstOrDefaultAsync(so => so.Id == id);

            if (order == null)
                return NotFound();

            if (order.Status == "Completed")
            {
                TempData["Error"] = "Cannot delete completed sales order.";
                return RedirectToAction(nameof(SalesOrders));
            }

            return View(order);
        }

        // POST: Order/DeleteSalesOrder/5
        [HttpPost, ActionName("DeleteSalesOrder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSalesOrderConfirmed(int id)
        {
            try
            {
                var order = await _context.SalesOrders
                    .Include(so => so.Items)
                    .FirstOrDefaultAsync(so => so.Id == id);

                if (order == null)
                    return NotFound();

                if (order.Status == "Completed")
                {
                    TempData["Error"] = "Cannot delete completed sales order.";
                    return RedirectToAction(nameof(SalesOrders));
                }

                // If stock was deducted, restore it before deleting
                if (order.StockUpdated && order.Items != null)
                {
                    foreach (var item in order.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity += item.Quantity;
                            _context.Products.Update(product);
                        }
                    }
                }

                if (order.Items != null && order.Items.Any())
                {
                    _context.SalesOrderItems.RemoveRange(order.Items);
                }

                _context.SalesOrders.Remove(order);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Sales order #{order.OrderNumber} deleted successfully!";
                return RedirectToAction(nameof(SalesOrders));
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR deleting sales order: {ex.Message}");
                TempData["Error"] = $"Error deleting sales order: {ex.Message}";
                return RedirectToAction(nameof(SalesOrders));
            }
        }

        // ============================================================
        // ========== PURCHASE ORDERS - COMPLETE CRUD ==========
        // ============================================================

        // GET: Order/PurchaseOrders
        public async Task<IActionResult> PurchaseOrders(string status, DateTime? fromDate, DateTime? toDate)
        {
            var orders = _context.PurchaseOrders
                .Include(po => po.Purchaser)
                .Include(po => po.Supplier)
                .Include(po => po.Items)
                .ThenInclude(i => i.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                orders = orders.Where(po => po.Status == status);
            }

            if (fromDate.HasValue && toDate.HasValue && toDate.Value.Date < fromDate.Value.Date)
            {
                ViewBag.DateRangeError = "\"To\" date can't be before \"From\" date. Showing results for \"From\" date onward only.";
                toDate = null;
            }

            if (fromDate.HasValue)
            {
                orders = orders.Where(po => po.OrderDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                orders = orders.Where(po => po.OrderDate <= toDate.Value);
            }

            ViewBag.StatusFilter = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(await orders.OrderByDescending(po => po.OrderDate).ToListAsync());
        }

        // GET: Order/PurchaseOrderDetails/5
        public async Task<IActionResult> PurchaseOrderDetails(int? id)
        {
            if (id == null)
                return NotFound();

            var order = await _context.PurchaseOrders
                .Include(po => po.Purchaser)
                .Include(po => po.Supplier)
                .Include(po => po.Items)
                .ThenInclude(i => i.Product)
                .Include(po => po.Payments)
                .FirstOrDefaultAsync(po => po.Id == id);

            if (order == null)
                return NotFound();
            ViewBag.CompanySettings = await _context.Settings.AsNoTracking().FirstOrDefaultAsync();
            return View(order);
        }

        // GET: Order/CreatePurchaseOrder
        public IActionResult CreatePurchaseOrder(int? productId)
        {
            ViewBag.Purchasers = _context.Purchasers
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.Suppliers = _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.Products = _context.Products
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .ToList();
            ViewBag.PreselectedProductId = productId ?? 0;
            var order = new PurchaseOrder
            {
                OrderDate = DateTime.Now,
                ExpectedDeliveryDate = DateTime.Now.AddDays(7),
                OrderNumber = "PO-" + DateTime.Now.ToString("yyyyMMddHHmmss")
            };

            return View(order);
        }

        // POST: Order/CreatePurchaseOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePurchaseOrder(PurchaseOrder order, [FromForm] List<PurchaseOrderItem> items)
        {
            if (order.ExpectedDeliveryDate.HasValue && order.ExpectedDeliveryDate.Value.Date < order.OrderDate.Date)
            {
                TempData["Error"] = "Expected Delivery Date can't be before Order Date.";
                return RedirectToAction(nameof(CreatePurchaseOrder));
            }

            if (order.DueDate.HasValue && order.DueDate.Value.Date < order.OrderDate.Date)
            {
                TempData["Error"] = "Due Date can't be before Order Date.";
                return RedirectToAction(nameof(CreatePurchaseOrder));
            }

            try
            {
                _logger.LogInformation("=== CREATE PURCHASE ORDER STARTED ===");
                _logger.LogInformation($"PurchaserId: {order.PurchaserId}");
                _logger.LogInformation($"Items Count: {items?.Count ?? 0}");

                // ✅ FIX: Don't filter out items with UnitPrice = 0
                var validItems = new List<PurchaseOrderItem>();
                if (items != null && items.Any())
                {
                    validItems = items.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();

                    // ✅ If UnitPrice (Unit Cost) is 0, fetch default from product's Cost Price.
                    // Unit Cost remains user-editable in the UI; this is only a fallback.
                    foreach (var item in validItems)
                    {
                        if (item.UnitPrice <= 0)
                        {
                            var product = await _context.Products.FindAsync(item.ProductId);
                            if (product != null)
                            {
                                item.UnitPrice = product.CostPrice;
                            }
                        }
                    }
                    _logger.LogInformation($"Items after filtering: {validItems.Count}");
                }

                ModelState.Remove("Purchaser");
                ModelState.Remove("Supplier");
                ModelState.Remove("Items");
                ModelState.Remove("PurchaseOrder");
                ModelState.Remove("Product");
                ModelState.Remove("OrderNumber");

                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        ModelState.Remove($"items[{i}].PurchaseOrder");
                        ModelState.Remove($"items[{i}].Product");
                    }
                }

                // Purchaser is REQUIRED
                if (order.PurchaserId <= 0)
                {
                    ModelState.AddModelError("PurchaserId", "Please select a purchaser");
                }

                if (validItems == null || !validItems.Any())
                {
                    ModelState.AddModelError("", "Please add at least one item to the order");
                }

                if (validItems != null && validItems.Any())
                {
                    foreach (var item in validItems)
                    {
                        if (item.ProductId <= 0)
                        {
                            ModelState.AddModelError("", "Please select a product for all items");
                            break;
                        }
                        if (item.Quantity <= 0)
                        {
                            ModelState.AddModelError("", "Quantity must be greater than 0");
                            break;
                        }
                        if (item.UnitPrice <= 0)
                        {
                            ModelState.AddModelError("", "Unit price must be greater than 0");
                            break;
                        }
                    }
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.Purchasers = _context.Purchasers
                        .Where(p => p.IsActive)
                        .OrderBy(p => p.Name)
                        .ToList();
                    ViewBag.Suppliers = _context.Suppliers
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.Name)
                        .ToList();
                    ViewBag.Products = _context.Products
                        .Include(p => p.Category)
                        .OrderBy(p => p.Name)
                        .ToList();
                    return View(order);
                }

                // ✅ Purchase Module - Tax & Discount support
                decimal poSubtotal = validItems.Sum(i => i.Quantity * i.UnitPrice);
                decimal poDiscount = poSubtotal * (order.DiscountPercentage / 100);
                order.TotalAmount = (poSubtotal - poDiscount) + order.TaxAmount;
                order.CreatedAt = DateTime.Now;
                order.Status = "Processing";

                // Phase 6 - Partial Payment
                order.PaidAmount = 0;
                order.RemainingAmount = order.TotalAmount;
                order.PaymentStatus = "Unpaid";

                _context.PurchaseOrders.Add(order);
                _logger.LogInformation("Before SaveChanges - PurchaseOrder added to context");

                var result = await _context.SaveChangesAsync();
                _logger.LogInformation($"After SaveChanges - Rows affected: {result}");
                _logger.LogInformation($"Order ID after save: {order.Id}");

                foreach (var item in validItems)
                {
                    item.PurchaseOrderId = order.Id;
                    item.TotalPrice = item.Quantity * item.UnitPrice;

                    var poItemProduct = await _context.Products.FindAsync(item.ProductId);
                    item.ItemCurrency = poItemProduct?.OriginalCurrency ?? "PKR";

                    _context.PurchaseOrderItems.Add(item);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Purchase order #{order.OrderNumber} created successfully!";
                return RedirectToAction(nameof(PurchaseOrders));
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR creating purchase order: {ex.Message}");
                _logger.LogError($"STACK: {ex.StackTrace}");
                TempData["Error"] = $"Error creating purchase order: {ex.Message}";
                ViewBag.Purchasers = _context.Purchasers
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .ToList();
                ViewBag.Suppliers = _context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .ToList();
                ViewBag.Products = _context.Products
                    .Include(p => p.Category)
                    .OrderBy(p => p.Name)
                    .ToList();
                return View(order);
            }
        }

        // GET: Order/EditPurchaseOrder/5
        public async Task<IActionResult> EditPurchaseOrder(int? id)
        {
            if (id == null)
                return NotFound();

            var order = await _context.PurchaseOrders
                .Include(po => po.Items)
                .Include(po => po.Payments)
                .FirstOrDefaultAsync(po => po.Id == id);

            if (order == null)
                return NotFound();

            ViewBag.Purchasers = _context.Purchasers
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToList();

            ViewBag.Suppliers = _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToList();

            ViewBag.Products = _context.Products
                .Include(p => p.Category)
                .OrderBy(p => p.Name)
                .ToList();

            return View(order);
        }

        // POST: Order/EditPurchaseOrder/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPurchaseOrder(int id, PurchaseOrder order, [FromForm] List<PurchaseOrderItem> items)
        {
            if (id != order.Id)
                return NotFound();

            if (order.ExpectedDeliveryDate.HasValue && order.ExpectedDeliveryDate.Value.Date < order.OrderDate.Date)
            {
                TempData["Error"] = "Expected Delivery Date can't be before Order Date.";
                return RedirectToAction(nameof(EditPurchaseOrder), new { id = id });
            }

            if (order.DueDate.HasValue && order.DueDate.Value.Date < order.OrderDate.Date)
            {
                TempData["Error"] = "Due Date can't be before Order Date.";
                return RedirectToAction(nameof(EditPurchaseOrder), new { id = id });
            }

            try
            {
                // ✅ FIX: Don't filter out items with UnitPrice = 0
                var validItems = new List<PurchaseOrderItem>();
                if (items != null && items.Any())
                {
                    validItems = items.Where(i => i.ProductId > 0 && i.Quantity > 0).ToList();

                    // ✅ If UnitPrice is 0, fetch from product
                    foreach (var item in validItems)
                    {
                        if (item.UnitPrice <= 0)
                        {
                            var product = await _context.Products.FindAsync(item.ProductId);
                            if (product != null)
                            {
                                item.UnitPrice = product.Price;
                            }
                        }
                    }
                }

                ModelState.Remove("Purchaser");
                ModelState.Remove("Supplier");
                ModelState.Remove("Items");
                ModelState.Remove("PurchaseOrder");
                ModelState.Remove("Product");
                ModelState.Remove("OrderNumber");

                if (items != null)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        ModelState.Remove($"items[{i}].PurchaseOrder");
                        ModelState.Remove($"items[{i}].Product");
                    }
                }

                if (order.PurchaserId <= 0)
                {
                    ModelState.AddModelError("PurchaserId", "Please select a purchaser");
                }

                if (validItems == null || !validItems.Any())
                {
                    ModelState.AddModelError("", "Please add at least one item to the order");
                }

                if (!ModelState.IsValid)
                {
                    ViewBag.Purchasers = _context.Purchasers
                        .Where(p => p.IsActive)
                        .OrderBy(p => p.Name)
                        .ToList();
                    ViewBag.Suppliers = _context.Suppliers
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.Name)
                        .ToList();
                    ViewBag.Products = _context.Products
                        .Include(p => p.Category)
                        .OrderBy(p => p.Name)
                        .ToList();
                    return View(order);
                }

                var existingOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == id);

                if (existingOrder == null)
                    return NotFound();

                // ✅ FIX: Purchase orders previously had NO server-side lock at
                // all (only the view hid the form). Once Completed (delivered)
                // or Cancelled, items/details can no longer be changed here —
                // only payment can still be recorded until it is fully paid.
                if (existingOrder.Status == "Completed" || existingOrder.Status == "Cancelled")
                {
                    TempData["Error"] = $"Cannot edit a {existingOrder.Status} order's details. Only payment can still be recorded until it is fully paid.";
                    return RedirectToAction(nameof(PurchaseOrders));
                }

                // STOCK HANDLING - Check if status changed
                var newStatus = order.Status;

                // If status changed to Completed and stock not updated yet
                if (newStatus == "Completed" && !existingOrder.StockUpdated)
                {
                    foreach (var item in validItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            // ✅ Purchase Module - Weighted Average Cost Price (see UpdatePurchaseOrderStatus)
                            decimal existingQty = product.StockQuantity;
                            decimal existingCost = product.CostPrice;
                            decimal incomingQty = item.Quantity;
                            decimal incomingCost = item.UnitPrice;
                            decimal combinedQty = existingQty + incomingQty;

                            if (combinedQty > 0)
                            {
                                product.CostPrice = Math.Round(((existingQty * existingCost) + (incomingQty * incomingCost)) / combinedQty, 2);
                            }

                            product.StockQuantity += item.Quantity;
                            product.UpdatedAt = DateTime.Now;
                            _context.Products.Update(product);
                        }
                    }
                    existingOrder.StockUpdated = true;
                }
                // If status changed to Cancelled and stock was updated
                else if (newStatus == "Cancelled" && existingOrder.StockUpdated)
                {
                    foreach (var item in validItems)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
                            product.UpdatedAt = DateTime.Now;
                            _context.Products.Update(product);
                        }
                    }
                    existingOrder.StockUpdated = false;
                }

                // Remove existing items
                if (existingOrder.Items != null && existingOrder.Items.Any())
                {
                    _context.PurchaseOrderItems.RemoveRange(existingOrder.Items);
                }

                // Update order
                existingOrder.PurchaserId = order.PurchaserId;
                existingOrder.SupplierId = order.SupplierId;
                existingOrder.OrderDate = order.OrderDate;
                existingOrder.ExpectedDeliveryDate = order.ExpectedDeliveryDate;
                existingOrder.Status = newStatus;
                existingOrder.Notes = order.Notes;
                existingOrder.DueDate = order.DueDate;
                existingOrder.UpdatedAt = DateTime.Now;

                decimal newPoTotal = validItems.Sum(i => i.Quantity * i.UnitPrice);

                // ✅ Prevent data mismatch: don't let the new total drop below
                // what has already been paid to the supplier.
                if (newPoTotal < existingOrder.PaidAmount)
                {
                    TempData["Error"] = $"Cannot reduce order total ({newPoTotal:N2}) below the amount already paid ({existingOrder.PaidAmount:N2}). Please adjust items so the total is at least {existingOrder.PaidAmount:N2}.";
                    ViewBag.Purchasers = _context.Purchasers.Where(p => p.IsActive).OrderBy(p => p.Name).ToList();
                    ViewBag.Suppliers = _context.Suppliers.Where(s => s.IsActive).OrderBy(s => s.Name).ToList();
                    ViewBag.Products = _context.Products.Include(p => p.Category).OrderBy(p => p.Name).ToList();
                    return View(order);
                }

                existingOrder.TotalAmount = newPoTotal;

                RecalculatePurchasePaymentStatus(existingOrder);

                _context.Update(existingOrder);

                foreach (var item in validItems)
                {
                    item.PurchaseOrderId = existingOrder.Id;
                    item.TotalPrice = item.Quantity * item.UnitPrice;

                    var editPoItemProduct = await _context.Products.FindAsync(item.ProductId);
                    item.ItemCurrency = editPoItemProduct?.OriginalCurrency ?? "PKR";

                    _context.PurchaseOrderItems.Add(item);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Purchase order #{existingOrder.OrderNumber} updated successfully!";
                return RedirectToAction(nameof(PurchaseOrders));
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR editing purchase order: {ex.Message}");
                TempData["Error"] = $"Error updating purchase order: {ex.Message}";
                ViewBag.Purchasers = _context.Purchasers
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .ToList();
                ViewBag.Suppliers = _context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .ToList();
                ViewBag.Products = _context.Products
                    .Include(p => p.Category)
                    .OrderBy(p => p.Name)
                    .ToList();
                return View(order);
            }
        }

        // POST: Order/UpdatePurchaseOrderStatus/5
        [HttpPost]
        public async Task<IActionResult> UpdatePurchaseOrderStatus(int id, [FromBody] StatusUpdateModel model)
        {
            try
            {
                var order = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == id);

                if (order == null)
                    return Json(new { success = false, message = "Order not found" });

                // ✅ Once an order is Completed (delivered) or Cancelled, that is
                // final — status cannot change again from here.
                if (order.Status == "Completed" || order.Status == "Cancelled")
                {
                    return Json(new { success = false, message = $"This order is already {order.Status}, so its status can no longer be changed." });
                }

                var newStatus = model.Status;

                // Complete → Increase Stock (only once)
                if (newStatus == "Completed" && !order.StockUpdated)
                {
                    foreach (var item in order.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            // ✅ Purchase Module - Weighted Average Cost Price
                            // NewCost = ((OldQty * OldCost) + (PurchasedQty * PurchaseUnitCost)) / (OldQty + PurchasedQty)
                            // Applied here (on stock receipt) rather than at order-creation time,
                            // since this is the point stock actually enters inventory.
                            decimal existingQty = product.StockQuantity;
                            decimal existingCost = product.CostPrice;
                            decimal incomingQty = item.Quantity;
                            decimal incomingCost = item.UnitPrice;
                            decimal combinedQty = existingQty + incomingQty;

                            if (combinedQty > 0)
                            {
                                product.CostPrice = Math.Round(((existingQty * existingCost) + (incomingQty * incomingCost)) / combinedQty, 2);
                            }

                            product.StockQuantity += item.Quantity;
                            product.UpdatedAt = DateTime.Now;
                            _context.Products.Update(product);
                        }
                    }
                    order.StockUpdated = true;
                }
                // Cancelled → No stock change (do NOT increase stock)
                else if (newStatus == "Cancelled" && order.StockUpdated)
                {
                    // If order was completed then cancelled, reverse stock
                    foreach (var item in order.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
                            product.UpdatedAt = DateTime.Now;
                            _context.Products.Update(product);
                        }
                    }
                    order.StockUpdated = false;
                }

                order.Status = newStatus;
                order.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Order status updated to {model.Status}" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR updating purchase order status: {ex.Message}");
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        // GET: Order/DeletePurchaseOrder/5
        public async Task<IActionResult> DeletePurchaseOrder(int? id)
        {
            if (id == null)
                return NotFound();

            var order = await _context.PurchaseOrders
                .Include(po => po.Purchaser)
                .Include(po => po.Supplier)
                .Include(po => po.Items)
                .FirstOrDefaultAsync(po => po.Id == id);

            if (order == null)
                return NotFound();

            if (order.Status == "Completed")
            {
                TempData["Error"] = "Cannot delete completed purchase order.";
                return RedirectToAction(nameof(PurchaseOrders));
            }

            return View(order);
        }

        // POST: Order/DeletePurchaseOrder/5
        [HttpPost, ActionName("DeletePurchaseOrder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePurchaseOrderConfirmed(int id)
        {
            try
            {
                var order = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == id);

                if (order == null)
                    return NotFound();

                if (order.Status == "Completed")
                {
                    TempData["Error"] = "Cannot delete completed purchase order.";
                    return RedirectToAction(nameof(PurchaseOrders));
                }

                // If stock was updated, restore it before deleting
                if (order.StockUpdated && order.Items != null)
                {
                    foreach (var item in order.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            product.StockQuantity = Math.Max(0, product.StockQuantity - item.Quantity);
                            _context.Products.Update(product);
                        }
                    }
                }

                if (order.Items != null && order.Items.Any())
                {
                    _context.PurchaseOrderItems.RemoveRange(order.Items);
                }

                _context.PurchaseOrders.Remove(order);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Purchase order #{order.OrderNumber} deleted successfully!";
                return RedirectToAction(nameof(PurchaseOrders));
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR deleting purchase order: {ex.Message}");
                TempData["Error"] = $"Error deleting purchase order: {ex.Message}";
                return RedirectToAction(nameof(PurchaseOrders));
            }
        }

        // ============================================================
        // ========== PHASE 6 - PARTIAL PAYMENT / CREDIT SYSTEM ==========
        // ============================================================

        // POST: Order/RecordSalesPayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordSalesPayment(int orderId, decimal amount, string? notes)
        {
            try
            {
                var order = await _context.SalesOrders.FirstOrDefaultAsync(o => o.Id == orderId);
                if (order == null)
                {
                    TempData["Error"] = "Sales order not found.";
                    return RedirectToAction(nameof(SalesOrders));
                }

                if (order.Status == "Cancelled")
                {
                    TempData["Error"] = "Cannot record a payment against a cancelled order.";
                    return RedirectToAction(nameof(EditSalesOrder), new { id = orderId });
                }

                if (amount <= 0)
                {
                    TempData["Error"] = "Payment amount must be greater than 0.";
                    return RedirectToAction(nameof(EditSalesOrder), new { id = orderId });
                }

                if (amount > order.RemainingAmount)
                {
                    TempData["Error"] = $"Payment cannot exceed the remaining amount ({order.RemainingAmount:N2}).";
                    return RedirectToAction(nameof(EditSalesOrder), new { id = orderId });
                }

                _context.Payments.Add(new Payment
                {
                    SalesOrderId = order.Id,
                    Amount = amount,
                    Notes = notes,
                    PaymentDate = DateTime.Now
                });

                order.PaidAmount += amount;
                RecalculateSalesPaymentStatus(order);
                order.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Payment recorded successfully.";
                return RedirectToAction(nameof(EditSalesOrder), new { id = orderId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error recording payment: {ex.Message}";
                return RedirectToAction(nameof(EditSalesOrder), new { id = orderId });
            }
        }

        // POST: Order/RecordPurchasePayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPurchasePayment(int orderId, decimal amount, string? notes)
        {
            try
            {
                var order = await _context.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == orderId);
                if (order == null)
                {
                    TempData["Error"] = "Purchase order not found.";
                    return RedirectToAction(nameof(PurchaseOrders));
                }

                if (order.Status == "Cancelled")
                {
                    TempData["Error"] = "Cannot record a payment against a cancelled order.";
                    return RedirectToAction(nameof(EditPurchaseOrder), new { id = orderId });
                }

                if (amount <= 0)
                {
                    TempData["Error"] = "Payment amount must be greater than 0.";
                    return RedirectToAction(nameof(EditPurchaseOrder), new { id = orderId });
                }

                if (amount > order.RemainingAmount)
                {
                    TempData["Error"] = $"Payment cannot exceed the remaining amount ({order.RemainingAmount:N2}).";
                    return RedirectToAction(nameof(EditPurchaseOrder), new { id = orderId });
                }

                _context.Payments.Add(new Payment
                {
                    PurchaseOrderId = order.Id,
                    Amount = amount,
                    Notes = notes,
                    PaymentDate = DateTime.Now
                });

                order.PaidAmount += amount;
                RecalculatePurchasePaymentStatus(order);
                order.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Payment recorded successfully.";
                return RedirectToAction(nameof(EditPurchaseOrder), new { id = orderId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error recording payment: {ex.Message}";
                return RedirectToAction(nameof(EditPurchaseOrder), new { id = orderId });
            }
        }

        // ============================================================
        // ========== HELPER METHODS ==========
        // ============================================================

        private void RecalculateSalesPaymentStatus(SalesOrder order)
        {
            order.RemainingAmount = Math.Max(0, order.GrandTotal - order.PaidAmount);
            if (order.PaidAmount <= 0) order.PaymentStatus = "Unpaid";
            else if (order.RemainingAmount <= 0) order.PaymentStatus = "Paid";
            else order.PaymentStatus = "Partial";
        }

        private void RecalculatePurchasePaymentStatus(PurchaseOrder order)
        {
            order.RemainingAmount = Math.Max(0, order.TotalAmount - order.PaidAmount);
            if (order.PaidAmount <= 0) order.PaymentStatus = "Unpaid";
            else if (order.RemainingAmount <= 0) order.PaymentStatus = "Paid";
            else order.PaymentStatus = "Partial";
        }

        private bool PurchaseOrderExists(int id)
        {
            return _context.PurchaseOrders.Any(e => e.Id == id);
        }

        private bool SalesOrderExists(int id)
        {
            return _context.SalesOrders.Any(e => e.Id == id);
        }

        public class StatusUpdateModel
        {
            public string? Status { get; set; } = string.Empty;
        }
    }
}
