using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SIOMS.Models
{
    // ✅ Phase 6 - Partial Payment / Credit System
    // One row per payment recorded against a Sales Order OR a Purchase Order
    // (exactly one of the two FKs is set). This is the full payment history —
    // SalesOrder/PurchaseOrder.PaidAmount is just the running total for fast reads.
    public class Payment
    {
        [Key]
        public int Id { get; set; }

        public int? SalesOrderId { get; set; }

        public int? PurchaseOrderId { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Payment amount must be greater than 0")]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [StringLength(300)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation Properties
        [BindNever]
        public virtual SalesOrder? SalesOrder { get; set; }

        [BindNever]
        public virtual PurchaseOrder? PurchaseOrder { get; set; }
    }
}
