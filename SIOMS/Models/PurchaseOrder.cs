using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding; 

namespace SIOMS.Models
{
    public class PurchaseOrder
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string OrderNumber { get; set; }

        // ✅ Purchaser is REQUIRED
        [Required(ErrorMessage = "Purchaser is required")]
        [Display(Name = "Purchaser")]
        public int PurchaserId { get; set; }

        // ✅ Supplier is OPTIONAL
        [Display(Name = "Supplier")]
        public int? SupplierId { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        public DateTime? ExpectedDeliveryDate { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "Processing";

        [StringLength(500)]
        public string Notes { get; set; }

        public decimal TotalAmount { get; set; }

        // ✅ Purchase Module Enhancement - Tax & Discount support (mirrors SalesOrder)
        [Range(0, 100, ErrorMessage = "Discount must be between 0 and 100")]
        [Display(Name = "Discount %")]
        public decimal DiscountPercentage { get; set; } = 0;

        [Range(0, double.MaxValue, ErrorMessage = "Tax amount cannot be negative")]
        [Display(Name = "Tax Amount")]
        public decimal TaxAmount { get; set; } = 0;

        public bool StockUpdated { get; set; } = false;

        // ✅ Phase 6 - Partial Payment / Credit System
        public decimal PaidAmount { get; set; } = 0;

        public decimal RemainingAmount { get; set; } = 0;

        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Unpaid"; // Paid / Partial / Unpaid

        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public virtual Purchaser Purchaser { get; set; }
        public virtual Supplier Supplier { get; set; }
        [BindNever]
        public virtual ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();

        [BindNever]
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
