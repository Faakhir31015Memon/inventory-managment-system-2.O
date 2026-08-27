using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding; 

namespace SIOMS.Models
{
    public class SalesOrder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string OrderNumber { get; set; }

        [Required]
        public int CustomerId { get; set; }

        // Supplier is OPTIONAL, but if provided, SupplierPhone is required
        [Display(Name = "Supplier")]
        public int? SupplierId { get; set; }

        // ✅ Supplier Contact Number (required if SupplierId has value)
        [Display(Name = "Supplier Contact")]
        public string? SupplierPhone { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string Status { get; set; } = "Processing";

        [StringLength(500)]
        public string Notes { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal DiscountPercentage { get; set; } = 0;

        public decimal TaxAmount { get; set; } = 0;

        public decimal GrandTotal { get; set; }

        public bool StockUpdated { get; set; } = false;

        // Phase 6 - Partial Payment / Credit System
        public decimal PaidAmount { get; set; } = 0;

        public decimal RemainingAmount { get; set; } = 0;

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Unpaid";

        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public virtual Customer Customer { get; set; }
        public virtual Supplier Supplier { get; set; }
        
        [BindNever]
        public virtual ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();

        [BindNever]
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}