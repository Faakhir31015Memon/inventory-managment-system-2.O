using System;
using System.Collections.Generic;

namespace SIOMS.ViewModels
{
    public class DashboardViewModel
    {
        // Summary
        public int TotalProducts { get; set; }
        public int TotalCategories { get; set; }
        public int TotalSuppliers { get; set; }
        public int TotalCustomers { get; set; }

        // Stock
        public int LowStockItems { get; set; }
        public int OutOfStockItems { get; set; }
        public decimal TotalStockValue { get; set; }

        // Orders
        public int PendingPurchaseOrders { get; set; }
        public int PendingSalesOrders { get; set; }
        public decimal TodaySales { get; set; }

        // Financial
        public decimal MonthlyRevenue { get; set; }
        public decimal MonthlyExpenses { get; set; }
        public decimal ProfitMargin { get; set; }

        // Charts
        public List<MonthlyData> MonthlySales { get; set; } = new List<MonthlyData>();
        public List<MonthlyData> MonthlyPurchases { get; set; } = new List<MonthlyData>();
        public List<CategoryData> StockByCategory { get; set; } = new List<CategoryData>();

        // Recent Activities
        public List<AlertViewModel> RecentAlerts { get; set; } = new List<AlertViewModel>();
        public List<RecentOrder> RecentOrders { get; set; } = new List<RecentOrder>();
        public List<RecentProduct> TopSellingProducts { get; set; } = new List<RecentProduct>();
    }

    public class MonthlyData
    {
        public int Month { get; set; }
        public string MonthName { get; set; } = string.Empty;
        public decimal Sales { get; set; } // Use Sales instead of Value
        public decimal Purchases { get; set; }
    }

    public class CategoryData
    {
        public string CategoryName { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int TotalStock { get; set; }
    }

    public class AlertViewModel
    {
        public int Id { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string AlertType { get; set; } = string.Empty;
        public DateTime AlertDate { get; set; }
        public bool IsResolved { get; set; }
    }

    public class RecentOrder
    {
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class RecentProduct
    {
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; } // Use QuantitySold
        public decimal Revenue { get; set; }
    }
}
