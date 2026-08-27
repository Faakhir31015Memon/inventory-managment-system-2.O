using System;
using System.ComponentModel.DataAnnotations;

namespace SIOMS.Models
{
    public class PurchaseOrderItem
    {
        [Key]
        public int Id { get; set; }

        public int PurchaseOrderId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
        public decimal UnitPrice { get; set; }

        public decimal TotalPrice { get; set; }

        // ✅ Phase 7: currency the UnitPrice was captured in (copied from
        // Product.OriginalCurrency at order-creation time).
        [StringLength(3)]
        public string ItemCurrency { get; set; } = "PKR";

        // Navigation Properties
        public virtual PurchaseOrder PurchaseOrder { get; set; }
        public virtual Product Product { get; set; }
    }
}