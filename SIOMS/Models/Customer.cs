using System.ComponentModel.DataAnnotations;

namespace SIOMS.Models
{
    public class Customer
    {
        [Key]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Customer name is required")]
        [StringLength(200)]
        [Display(Name = "Customer Name")]
        public string Name { get; set; } = string.Empty;
        
        [Phone]
        [StringLength(20)]
        public string? Phone { get; set; }
        
        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }
        
        [StringLength(500)]
        public string? Address { get; set; }
        
        [StringLength(100)]
        public string? City { get; set; }
        
        [Display(Name = "Customer Type")]
        public string CustomerType { get; set; } = "Regular";
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        
        // Navigation
        public virtual ICollection<SalesOrder>? SalesOrders { get; set; }
    }
}
