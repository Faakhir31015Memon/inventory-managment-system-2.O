using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SIOMS.Models
{
    public class Purchaser
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Purchaser name is required")]
        [StringLength(200, ErrorMessage = "Purchaser name cannot exceed 200 characters")]
        [Display(Name = "Purchaser Name")]
        public string Name { get; set; }

        [Required(ErrorMessage = "Contact number is required")]
        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(20, ErrorMessage = "Contact number cannot exceed 20 characters")]
        [Display(Name = "Contact Number")]
        public string Phone { get; set; }

        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters")]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [StringLength(500, ErrorMessage = "Address cannot exceed 500 characters")]
        [Display(Name = "Address")]
        public string? Address { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;

        // Navigation
        public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    }
}
