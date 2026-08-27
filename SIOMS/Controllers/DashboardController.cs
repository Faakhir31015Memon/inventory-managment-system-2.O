using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIOMS.Data;
using SIOMS.Models;
using SIOMS.Services;
using SIOMS.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIOMS.Controllers
{
    public class DashboardController : AdminBaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly ExchangeRateService _exchangeRateService;

        public DashboardController(ApplicationDbContext context, ExchangeRateService exchangeRateService)
        {
            _context = context;
            _exchangeRateService = exchangeRateService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var dashboardData = await GetDashboardDataAsync();
                return View(dashboardData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dashboard Error: {ex.Message}");
                TempData["Error"] = "Error loading dashboard data.";
                return View(new DashboardViewModel());
            }
        }

        private async Task<DashboardViewModel> GetDashboardDataAsync()
        {
            var model = new DashboardViewModel();

            try
            {
                var settings = await _context.Settings.AsNoTracking().FirstOrDefaultAsync();
                var displayCurrency = settings?.Currency ?? "PKR";
                var rates = await _exchangeRateService.GetRatesAsync();
                ViewBag.DisplayCurrency = displayCurrency;

                model.TotalProducts = await _context.Products.CountAsync();
                model.TotalCategories = await _context.Categories.CountAsync();
                model.TotalSuppliers = await _context.Suppliers.CountAsync();
                model.TotalCustomers = await _context.Customers.CountAsync();

                model.LowStockItems = await _context.Products
                    .Where(p => p.StockQuantity <= p.MinStockLimit && p.StockQuantity > 0)
                    .CountAsync();
                model.OutOfStockItems = await _context.Products
                    .Where(p => p.StockQuantity == 0)
                    .CountAsync();

                var allProducts = await _context.Products
                    .Select(p => new { p.StockQuantity, p.Price, p.OriginalCurrency })
                    .ToListAsync();
                model.TotalStockValue = allProducts.Sum(p =>
                    p.StockQuantity * ExchangeRateService.Convert(p.Price, p.OriginalCurrency, displayCurrency, rates));

                model.PendingPurchaseOrders = await _context.PurchaseOrders
                    .Where(po => po.Status == "Pending")
                    .CountAsync();
                model.PendingSalesOrders = await _context.SalesOrders
                    .Where(so => so.Status == "Pending")
                    .CountAsync();

                var currentYear = DateTime.Now.Year;

                var yearSalesOrders = await _context.SalesOrders
                    .Include(so => so.Items)
                    .Where(so => so.OrderDate.Year == currentYear && so.Status == "Completed")
                    .ToListAsync();

                var yearPurchaseOrders = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .Where(po => po.OrderDate.Year == currentYear && po.Status == "Delivered")
                    .ToListAsync();

                model.TodaySales = yearSalesOrders
                    .Where(so => so.OrderDate.Date == DateTime.Today)
                    .Sum(so => ConvertItemsTotal(so.Items, displayCurrency, rates));

                model.MonthlySales = GetMonthlySalesData(yearSalesOrders, currentYear, displayCurrency, rates);
                model.MonthlyPurchases = GetMonthlyPurchasesData(yearPurchaseOrders, currentYear, displayCurrency, rates);

                model.RecentAlerts = await GetRecentAlertsAsync();
                model.RecentOrders = await GetRecentOrdersAsync(displayCurrency, rates);
                model.TopSellingProducts = await GetTopSellingProductsAsync(displayCurrency, rates);
                model.StockByCategory = await GetStockByCategoryAsync();

                var currentMonth = DateTime.Now.Month;

                model.MonthlyRevenue = yearSalesOrders
                    .Where(so => so.OrderDate.Month == currentMonth)
                    .Sum(so => ConvertItemsTotal(so.Items, displayCurrency, rates));

                model.MonthlyExpenses = yearPurchaseOrders
                    .Where(po => po.OrderDate.Month == currentMonth)
                    .Sum(po => ConvertItemsTotal(po.Items, displayCurrency, rates));

                model.ProfitMargin = model.MonthlyRevenue > 0 ?
                    ((model.MonthlyRevenue - model.MonthlyExpenses) / model.MonthlyRevenue) * 100 : 0;

                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDashboardDataAsync: {ex.Message}");
                return model;
            }
        }

        private static decimal ConvertItemsTotal(IEnumerable<SalesOrderItem> items, string displayCurrency, Dictionary<string, decimal> rates)
        {
            return items?.Sum(i => ExchangeRateService.Convert(i.Quantity * i.UnitPrice, i.ItemCurrency, displayCurrency, rates)) ?? 0;
        }

        private static decimal ConvertItemsTotal(IEnumerable<PurchaseOrderItem> items, string displayCurrency, Dictionary<string, decimal> rates)
        {
            return items?.Sum(i => ExchangeRateService.Convert(i.Quantity * i.UnitPrice, i.ItemCurrency, displayCurrency, rates)) ?? 0;
        }

        private static List<MonthlyData> GetMonthlySalesData(List<SalesOrder> yearSalesOrders, int currentYear, string displayCurrency, Dictionary<string, decimal> rates)
        {
            var monthlySales = new List<MonthlyData>();
            for (int month = 1; month <= 12; month++)
            {
                var sales = yearSalesOrders
                    .Where(so => so.OrderDate.Month == month)
                    .Sum(so => ConvertItemsTotal(so.Items, displayCurrency, rates));

                monthlySales.Add(new MonthlyData
                {
                    Month = month,
                    MonthName = new DateTime(currentYear, month, 1).ToString("MMM"),
                    Sales = sales
                });
            }
            return monthlySales;
        }

        private static List<MonthlyData> GetMonthlyPurchasesData(List<PurchaseOrder> yearPurchaseOrders, int currentYear, string displayCurrency, Dictionary<string, decimal> rates)
        {
            var monthlyPurchases = new List<MonthlyData>();
            for (int month = 1; month <= 12; month++)
            {
                var purchases = yearPurchaseOrders
                    .Where(po => po.OrderDate.Month == month)
                    .Sum(po => ConvertItemsTotal(po.Items, displayCurrency, rates));

                monthlyPurchases.Add(new MonthlyData
                {
                    Month = month,
                    MonthName = new DateTime(currentYear, month, 1).ToString("MMM"),
                    Purchases = purchases
                });
            }
            return monthlyPurchases;
        }

        private async Task<List<AlertViewModel>> GetRecentAlertsAsync()
        {
            return await _context.AlertLogs
                .Include(a => a.Product)
                .Where(a => !a.IsResolved)
                .OrderByDescending(a => a.AlertDate)
                .Take(10)
                .Select(a => new AlertViewModel
                {
                    Id = a.Id,
                    ProductName = a.Product.Name,
                    Message = a.Message,
                    AlertType = a.AlertType,
                    AlertDate = a.AlertDate,
                    IsResolved = a.IsResolved
                })
                .ToListAsync();
        }

        private async Task<List<RecentOrder>> GetRecentOrdersAsync(string displayCurrency, Dictionary<string, decimal> rates)
        {
            var orders = await _context.SalesOrders
                .Include(so => so.Customer)
                .Include(so => so.Items)
                .OrderByDescending(so => so.OrderDate)
                .Take(5)
                .ToListAsync();

            return orders.Select(so => new RecentOrder
            {
                OrderNumber = so.OrderNumber,
                CustomerName = so.Customer != null ? so.Customer.Name : "N/A",
                Amount = ConvertItemsTotal(so.Items, displayCurrency, rates),
                OrderDate = so.OrderDate,
                Status = so.Status
            }).ToList();
        }

        private async Task<List<RecentProduct>> GetTopSellingProductsAsync(string displayCurrency, Dictionary<string, decimal> rates)
        {
            var soldItems = await _context.SalesOrderItems
                .Include(i => i.Product)
                .Include(i => i.SalesOrder)
                .Where(i => i.SalesOrder.Status == "Completed")
                .ToListAsync();

            return soldItems
                .GroupBy(i => i.ProductId)
                .Select(g => new RecentProduct
                {
                    ProductName = g.First().Product.Name,
                    QuantitySold = g.Sum(i => i.Quantity),
                    Revenue = g.Sum(i => ExchangeRateService.Convert(i.TotalPrice, i.ItemCurrency, displayCurrency, rates))
                })
                .OrderByDescending(p => p.QuantitySold)
                .Take(5)
                .ToList();
        }

        private async Task<List<CategoryData>> GetStockByCategoryAsync()
        {
            return await _context.Categories
                .Include(c => c.Products)
                .Select(c => new CategoryData
                {
                    CategoryName = c.Name,
                    ProductCount = c.Products.Count,
                    TotalStock = c.Products.Sum(p => p.StockQuantity)
                })
                .ToListAsync();
        }
    }
}
