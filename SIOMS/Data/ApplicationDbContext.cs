using Microsoft.EntityFrameworkCore;
using SIOMS.Models;

namespace SIOMS.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Settings> Settings { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Purchaser> Purchasers { get; set; }
        public DbSet<SalesOrder> SalesOrders { get; set; }
        public DbSet<SalesOrderItem> SalesOrderItems { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
        public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
        public DbSet<AlertLog> AlertLogs { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ========== Settings ==========
            modelBuilder.Entity<Settings>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).HasMaxLength(100);
                entity.Property(e => e.PasswordHash).HasMaxLength(200);
                entity.Property(e => e.SiteName).HasMaxLength(100);
                entity.Property(e => e.SiteEmail).HasMaxLength(100);
                entity.Property(e => e.Currency).HasMaxLength(10);
                entity.Property(e => e.CurrencySymbol).HasMaxLength(10);
                entity.Property(e => e.CompanyName).HasMaxLength(100);
                entity.Property(e => e.CompanyPhone).HasMaxLength(20);
                entity.Property(e => e.CachedExchangeRatesJson).HasColumnType("nvarchar(max)");
            });

            // ========== Category ==========
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
            });

            // ========== Product ==========
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.SKU).HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OriginalCurrency).HasMaxLength(3).HasDefaultValue("PKR");
                entity.Property(e => e.StockQuantity).HasDefaultValue(0);
                entity.Property(e => e.MinStockLimit).HasDefaultValue(10);
                entity.Property(e => e.ImageUrl).HasMaxLength(int.MaxValue);

                entity.HasOne(e => e.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== Customer ==========
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.Address).HasMaxLength(500);
                entity.Property(e => e.City).HasMaxLength(100);
                entity.Property(e => e.CustomerType).HasMaxLength(50).HasDefaultValue("Regular");
            });

            // ========== Supplier ==========
            modelBuilder.Entity<Supplier>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.Address).HasMaxLength(500);
                entity.Property(e => e.City).HasMaxLength(100);
            });

            // ========== Purchaser (NEW) ==========
            modelBuilder.Entity<Purchaser>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
                entity.Property(e => e.Phone).HasMaxLength(20);
            });

            // ========== SalesOrder ==========
            modelBuilder.Entity<SalesOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Processing");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.TaxAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.GrandTotal).HasColumnType("decimal(18,2)");
                entity.Property(e => e.StockUpdated).HasDefaultValue(false);
                entity.Property(e => e.PaidAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.RemainingAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.PaymentStatus).HasMaxLength(20).HasDefaultValue("Unpaid");

                entity.HasOne(e => e.Customer)
                    .WithMany(c => c.SalesOrders)
                    .HasForeignKey(e => e.CustomerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== SalesOrderItem ==========
            modelBuilder.Entity<SalesOrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ItemCurrency).HasMaxLength(3).HasDefaultValue("PKR");

                entity.HasOne(e => e.SalesOrder)
                    .WithMany(o => o.Items)
                    .HasForeignKey(e => e.SalesOrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== PurchaseOrder ==========
            modelBuilder.Entity<PurchaseOrder>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OrderNumber).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("Processing");
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.StockUpdated).HasDefaultValue(false);
                entity.Property(e => e.PaidAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.RemainingAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.PaymentStatus).HasMaxLength(20).HasDefaultValue("Unpaid");

                // ✅ Purchaser - REQUIRED
                entity.HasOne(e => e.Purchaser)
                    .WithMany(p => p.PurchaseOrders)
                    .HasForeignKey(e => e.PurchaserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // ✅ Supplier - OPTIONAL
                entity.HasOne(e => e.Supplier)
                    .WithMany(s => s.PurchaseOrders)
                    .HasForeignKey(e => e.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== PurchaseOrderItem ==========
            modelBuilder.Entity<PurchaseOrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.ItemCurrency).HasMaxLength(3).HasDefaultValue("PKR");

                entity.HasOne(e => e.PurchaseOrder)
                    .WithMany(o => o.Items)
                    .HasForeignKey(e => e.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ========== Payment (Phase 6 - Partial Payment / Credit System) ==========
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes).HasMaxLength(300);

                entity.HasOne(e => e.SalesOrder)
                    .WithMany(o => o.Payments)
                    .HasForeignKey(e => e.SalesOrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.PurchaseOrder)
                    .WithMany(o => o.Payments)
                    .HasForeignKey(e => e.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ========== AlertLog ==========
            modelBuilder.Entity<AlertLog>(entity =>
            {                entity.HasKey(e => e.Id);
                entity.Property(e => e.AlertType).HasMaxLength(50);
                entity.Property(e => e.Message).HasMaxLength(500);
                entity.Property(e => e.ResolutionNotes).HasMaxLength(1000);

                entity.HasOne(e => e.Product)
                    .WithMany()
                    .HasForeignKey(e => e.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}