// SIOMS/Models/Category.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIOMS.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "Category name is required")]
        [StringLength(100, ErrorMessage = "Category name cannot be longer than 100 characters")]
        [Display(Name = "Category Name")]
        public string Name { get; set; }
        
        [StringLength(500, ErrorMessage = "Description cannot be longer than 500 characters")]
        [Display(Name = "Description")]
        public string? Description { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation
        public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
