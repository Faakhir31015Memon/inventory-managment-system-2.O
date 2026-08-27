/* ============================================================
   SIOMS - Sample Seed Data Script
   Run this in SSMS against your SIOMS database.
   Safe to run once. If you run it again, you'll get duplicate rows
   (no unique constraints on Name), so either clear tables first
   or just run it once after a fresh EnsureCreated().
   ============================================================ */

USE SIOMS;
GO

/* ============================================================
   1) CATEGORIES
   ============================================================ */
INSERT INTO Categories (Name, Description, CreatedAt) VALUES
(N'Electronics', N'Electronic gadgets and accessories', GETDATE()),
(N'Groceries', N'Daily grocery and food items', GETDATE()),
(N'Furniture', N'Home and office furniture', GETDATE()),
(N'Stationery', N'Office and school stationery items', GETDATE());
GO

DECLARE @CatElectronics INT = (SELECT Id FROM Categories WHERE Name = N'Electronics');
DECLARE @CatGroceries   INT = (SELECT Id FROM Categories WHERE Name = N'Groceries');
DECLARE @CatFurniture   INT = (SELECT Id FROM Categories WHERE Name = N'Furniture');
DECLARE @CatStationery  INT = (SELECT Id FROM Categories WHERE Name = N'Stationery');

/* ============================================================
   2) PRODUCTS
   ============================================================ */
INSERT INTO Products (Name, Description, CategoryId, Price, CostPrice, OriginalCurrency, StockQuantity, MinStockLimit, SKU, IsActive, CreatedAt) VALUES
(N'Wireless Mouse', N'Bluetooth wireless mouse', @CatElectronics, 1500.00, 900.00, N'PKR', 40, 10, N'ELEC-001', 1, GETDATE()),
(N'USB-C Charger 20W', N'Fast charging adapter', @CatElectronics, 2200.00, 1400.00, N'PKR', 8, 10, N'ELEC-002', 1, GETDATE()),
(N'LED Desk Lamp', N'Adjustable LED desk lamp', @CatElectronics, 3200.00, 2100.00, N'PKR', 25, 5, N'ELEC-003', 1, GETDATE()),
(N'Basmati Rice 5kg', N'Premium basmati rice bag', @CatGroceries, 1800.00, 1350.00, N'PKR', 60, 15, N'GRO-001', 1, GETDATE()),
(N'Cooking Oil 5L', N'Sunflower cooking oil', @CatGroceries, 2600.00, 2100.00, N'PKR', 5, 10, N'GRO-002', 1, GETDATE()),
(N'Office Chair', N'Ergonomic mesh office chair', @CatFurniture, 15500.00, 11000.00, N'PKR', 12, 3, N'FUR-001', 1, GETDATE()),
(N'Wooden Study Table', N'4x2 ft wooden study table', @CatFurniture, 22000.00, 16500.00, N'PKR', 6, 2, N'FUR-002', 1, GETDATE()),
(N'A4 Paper Ream', N'500 sheets A4 printing paper', @CatStationery, 950.00, 650.00, N'PKR', 100, 20, N'STA-001', 1, GETDATE()),
(N'Gel Pens Pack (10)', N'Blue ink gel pens pack of 10', @CatStationery, 400.00, 250.00, N'PKR', 3, 10, N'STA-002', 1, GETDATE()),
(N'Notebook Set (5)', N'A5 ruled notebooks pack of 5', @CatStationery, 750.00, 500.00, N'PKR', 45, 10, N'STA-003', 1, GETDATE());
GO

/* ============================================================
   3) CUSTOMERS
   ============================================================ */
INSERT INTO Customers (Name, Phone, Email, Address, City, CustomerType, CreatedAt, IsActive) VALUES
(N'Ahmed Raza', N'03001234567', N'ahmed.raza@example.com', N'Block 5, Gulshan-e-Iqbal', N'Karachi', N'Regular', GETDATE(), 1),
(N'Sana Malik', N'03211234567', N'sana.malik@example.com', N'DHA Phase 6', N'Karachi', N'Wholesale', GETDATE(), 1),
(N'Bilal Khan', N'03331234567', N'bilal.khan@example.com', N'Model Town', N'Lahore', N'Regular', GETDATE(), 1),
(N'Ayesha Siddiqui', N'03451234567', N'ayesha.s@example.com', N'F-10 Markaz', N'Islamabad', N'Regular', GETDATE(), 1),
(N'Usman Tariq', N'03021234567', N'usman.tariq@example.com', N'Johar Town', N'Lahore', N'Wholesale', GETDATE(), 1);
GO

/* ============================================================
   4) SUPPLIERS
   ============================================================ */
INSERT INTO Suppliers (Name, Phone, Email, Address, City, CreatedAt, IsActive) VALUES
(N'Al-Karam Traders', N'02134567890', N'contact@alkaram.com', N'Site Area', N'Karachi', GETDATE(), 1),
(N'Punjab Wholesale Mart', N'04236547890', N'info@punjabwholesale.com', N'Shahdara', N'Lahore', GETDATE(), 1),
(N'National Furniture House', N'05112345678', N'sales@nationalfurniture.com', N'I-9 Industrial Area', N'Islamabad', GETDATE(), 1);
GO

/* ============================================================
   5) PURCHASERS
   ============================================================ */
INSERT INTO Purchasers (Name, Phone, Email, Address, CreatedAt, IsActive) VALUES
(N'Faisal Iqbal', N'03111234567', N'faisal.iqbal@example.com', N'Head Office, Karachi', GETDATE(), 1),
(N'Hamza Sheikh', N'03221234567', N'hamza.sheikh@example.com', N'Branch Office, Lahore', GETDATE(), 1);
GO

/* ============================================================
   6) PURCHASE ORDERS + ITEMS
   (StockUpdated = 1 because we're setting Product.StockQuantity
   above to already reflect these purchases having been received)
   ============================================================ */
DECLARE @Purchaser1 INT = (SELECT Id FROM Purchasers WHERE Name = N'Faisal Iqbal');
DECLARE @Purchaser2 INT = (SELECT Id FROM Purchasers WHERE Name = N'Hamza Sheikh');
DECLARE @Supplier1  INT = (SELECT Id FROM Suppliers WHERE Name = N'Al-Karam Traders');
DECLARE @Supplier2  INT = (SELECT Id FROM Suppliers WHERE Name = N'Punjab Wholesale Mart');

DECLARE @ProdMouse   INT = (SELECT Id FROM Products WHERE SKU = N'ELEC-001');
DECLARE @ProdCharger INT = (SELECT Id FROM Products WHERE SKU = N'ELEC-002');
DECLARE @ProdRice    INT = (SELECT Id FROM Products WHERE SKU = N'GRO-001');
DECLARE @ProdOil     INT = (SELECT Id FROM Products WHERE SKU = N'GRO-002');
DECLARE @ProdChair   INT = (SELECT Id FROM Products WHERE SKU = N'FUR-001');

-- Purchase Order 1
INSERT INTO PurchaseOrders (OrderNumber, PurchaserId, SupplierId, OrderDate, ExpectedDeliveryDate, Status, Notes, TotalAmount, DiscountPercentage, TaxAmount, StockUpdated, PaidAmount, RemainingAmount, PaymentStatus, CreatedAt) VALUES
(N'PO-1001', @Purchaser1, @Supplier1, DATEADD(DAY, -10, GETDATE()), DATEADD(DAY, -5, GETDATE()), N'Completed', N'Initial electronics stock', 43000.00, 0, 0, 1, 43000.00, 0, N'Paid', DATEADD(DAY, -10, GETDATE()));
GO

DECLARE @PO1 INT = (SELECT Id FROM PurchaseOrders WHERE OrderNumber = N'PO-1001');
DECLARE @ProdMouse   INT = (SELECT Id FROM Products WHERE SKU = N'ELEC-001');
DECLARE @ProdCharger INT = (SELECT Id FROM Products WHERE SKU = N'ELEC-002');

INSERT INTO PurchaseOrderItems (PurchaseOrderId, ProductId, Quantity, UnitPrice, TotalPrice, ItemCurrency) VALUES
(@PO1, @ProdMouse, 20, 900.00, 18000.00, N'PKR'),
(@PO1, @ProdCharger, 18, 1400.00, 25200.00, N'PKR');
GO

-- Purchase Order 2
INSERT INTO PurchaseOrders (OrderNumber, PurchaserId, SupplierId, OrderDate, ExpectedDeliveryDate, Status, Notes, TotalAmount, DiscountPercentage, TaxAmount, StockUpdated, PaidAmount, RemainingAmount, PaymentStatus, CreatedAt) VALUES
(N'PO-1002', (SELECT Id FROM Purchasers WHERE Name = N'Hamza Sheikh'), (SELECT Id FROM Suppliers WHERE Name = N'Punjab Wholesale Mart'), DATEADD(DAY, -6, GETDATE()), DATEADD(DAY, -2, GETDATE()), N'Completed', N'Grocery restock', 78900.00, 0, 0, 1, 40000.00, 38900.00, N'Partial', DATEADD(DAY, -6, GETDATE()));
GO

DECLARE @PO2 INT = (SELECT Id FROM PurchaseOrders WHERE OrderNumber = N'PO-1002');
DECLARE @ProdRice INT = (SELECT Id FROM Products WHERE SKU = N'GRO-001');
DECLARE @ProdOil  INT = (SELECT Id FROM Products WHERE SKU = N'GRO-002');

INSERT INTO PurchaseOrderItems (PurchaseOrderId, ProductId, Quantity, UnitPrice, TotalPrice, ItemCurrency) VALUES
(@PO2, @ProdRice, 40, 1350.00, 54000.00, N'PKR'),
(@PO2, @ProdOil, 12, 2100.00, 25200.00, N'PKR');
GO

-- Payment against PO-1002 (partial)
INSERT INTO Payments (PurchaseOrderId, Amount, PaymentDate, Notes, CreatedAt) VALUES
((SELECT Id FROM PurchaseOrders WHERE OrderNumber = N'PO-1002'), 40000.00, DATEADD(DAY, -6, GETDATE()), N'Advance payment', DATEADD(DAY, -6, GETDATE()));
GO

/* ============================================================
   7) SALES ORDERS + ITEMS
   ============================================================ */
DECLARE @Cust1 INT = (SELECT Id FROM Customers WHERE Name = N'Ahmed Raza');
DECLARE @Cust2 INT = (SELECT Id FROM Customers WHERE Name = N'Sana Malik');
DECLARE @Cust3 INT = (SELECT Id FROM Customers WHERE Name = N'Bilal Khan');
DECLARE @Cust4 INT = (SELECT Id FROM Customers WHERE Name = N'Ayesha Siddiqui');

-- Sales Order 1
INSERT INTO SalesOrders (OrderNumber, CustomerId, OrderDate, Status, Notes, TotalAmount, DiscountPercentage, TaxAmount, GrandTotal, StockUpdated, PaidAmount, RemainingAmount, PaymentStatus, CreatedAt) VALUES
(N'SO-2001', @Cust1, DATEADD(DAY, -4, GETDATE()), N'Completed', N'Walk-in sale', 4700.00, 0, 0, 4700.00, 1, 4700.00, 0, N'Paid', DATEADD(DAY, -4, GETDATE()));
GO

DECLARE @SO1 INT = (SELECT Id FROM SalesOrders WHERE OrderNumber = N'SO-2001');
DECLARE @ProdMouse2 INT = (SELECT Id FROM Products WHERE SKU = N'ELEC-001');
DECLARE @ProdLamp   INT = (SELECT Id FROM Products WHERE SKU = N'ELEC-003');

INSERT INTO SalesOrderItems (SalesOrderId, ProductId, Quantity, UnitPrice, TotalPrice, ItemCurrency) VALUES
(@SO1, @ProdMouse2, 1, 1500.00, 1500.00, N'PKR'),
(@SO1, @ProdLamp, 1, 3200.00, 3200.00, N'PKR');
GO

-- Sales Order 2
INSERT INTO SalesOrders (OrderNumber, CustomerId, OrderDate, Status, Notes, TotalAmount, DiscountPercentage, TaxAmount, GrandTotal, StockUpdated, PaidAmount, RemainingAmount, PaymentStatus, CreatedAt) VALUES
(N'SO-2002', (SELECT Id FROM Customers WHERE Name = N'Sana Malik'), DATEADD(DAY, -3, GETDATE()), N'Completed', N'Bulk grocery order', 27200.00, 5, 0, 25840.00, 1, 15000.00, 10840.00, N'Partial', DATEADD(DAY, -3, GETDATE()));
GO

DECLARE @SO2 INT = (SELECT Id FROM SalesOrders WHERE OrderNumber = N'SO-2002');
DECLARE @ProdRice2 INT = (SELECT Id FROM Products WHERE SKU = N'GRO-001');
DECLARE @ProdOil2  INT = (SELECT Id FROM Products WHERE SKU = N'GRO-002');

INSERT INTO SalesOrderItems (SalesOrderId, ProductId, Quantity, UnitPrice, TotalPrice, ItemCurrency) VALUES
(@SO2, @ProdRice2, 10, 1800.00, 18000.00, N'PKR'),
(@SO2, @ProdOil2, 3, 2600.00, 7800.00, N'PKR'),
(@SO2, @ProdOil2, 1, 1400.00, 1400.00, N'PKR');
GO

-- Payment against SO-2002 (partial)
INSERT INTO Payments (SalesOrderId, Amount, PaymentDate, Notes, CreatedAt) VALUES
((SELECT Id FROM SalesOrders WHERE OrderNumber = N'SO-2002'), 15000.00, DATEADD(DAY, -3, GETDATE()), N'Partial payment received', DATEADD(DAY, -3, GETDATE()));
GO

-- Sales Order 3
INSERT INTO SalesOrders (OrderNumber, CustomerId, OrderDate, Status, Notes, TotalAmount, DiscountPercentage, TaxAmount, GrandTotal, StockUpdated, PaidAmount, RemainingAmount, PaymentStatus, CreatedAt) VALUES
(N'SO-2003', (SELECT Id FROM Customers WHERE Name = N'Bilal Khan'), DATEADD(DAY, -1, GETDATE()), N'Processing', N'Furniture order', 15500.00, 0, 0, 15500.00, 0, 0, 15500.00, N'Unpaid', DATEADD(DAY, -1, GETDATE()));
GO

DECLARE @SO3 INT = (SELECT Id FROM SalesOrders WHERE OrderNumber = N'SO-2003');
DECLARE @ProdChair2 INT = (SELECT Id FROM Products WHERE SKU = N'FUR-001');

INSERT INTO SalesOrderItems (SalesOrderId, ProductId, Quantity, UnitPrice, TotalPrice, ItemCurrency) VALUES
(@SO3, @ProdChair2, 1, 15500.00, 15500.00, N'PKR');
GO

-- Sales Order 4 (today, for "recent sales" widgets)
INSERT INTO SalesOrders (OrderNumber, CustomerId, OrderDate, Status, Notes, TotalAmount, DiscountPercentage, TaxAmount, GrandTotal, StockUpdated, PaidAmount, RemainingAmount, PaymentStatus, CreatedAt) VALUES
(N'SO-2004', (SELECT Id FROM Customers WHERE Name = N'Ayesha Siddiqui'), GETDATE(), N'Completed', N'Stationery bulk order', 3050.00, 0, 0, 3050.00, 1, 3050.00, 0, N'Paid', GETDATE());
GO

DECLARE @SO4 INT = (SELECT Id FROM SalesOrders WHERE OrderNumber = N'SO-2004');
DECLARE @ProdPaper  INT = (SELECT Id FROM Products WHERE SKU = N'STA-001');
DECLARE @ProdPens   INT = (SELECT Id FROM Products WHERE SKU = N'STA-002');
DECLARE @ProdBooks  INT = (SELECT Id FROM Products WHERE SKU = N'STA-003');

INSERT INTO SalesOrderItems (SalesOrderId, ProductId, Quantity, UnitPrice, TotalPrice, ItemCurrency) VALUES
(@SO4, @ProdPaper, 2, 950.00, 1900.00, N'PKR'),
(@SO4, @ProdPens, 1, 400.00, 400.00, N'PKR'),
(@SO4, @ProdBooks, 1, 750.00, 750.00, N'PKR');
GO

/* ============================================================
   8) ALERT LOGS (Low Stock alerts - matches products with
      StockQuantity below MinStockLimit: Charger, Cooking Oil, Gel Pens)
   ============================================================ */
INSERT INTO AlertLogs (ProductId, Message, AlertType, AlertDate, IsResolved) VALUES
((SELECT Id FROM Products WHERE SKU = N'ELEC-002'), N'USB-C Charger 20W stock is below minimum limit (8/10)', N'LowStock', GETDATE(), 0),
((SELECT Id FROM Products WHERE SKU = N'GRO-002'), N'Cooking Oil 5L stock is below minimum limit (5/10)', N'LowStock', GETDATE(), 0),
((SELECT Id FROM Products WHERE SKU = N'STA-002'), N'Gel Pens Pack stock is below minimum limit (3/10)', N'LowStock', GETDATE(), 0);
GO

PRINT 'Seed data inserted successfully.';