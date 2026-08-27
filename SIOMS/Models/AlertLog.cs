using System.ComponentModel.DataAnnotations;

namespace SIOMS.Models
{
    public class AlertLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        [StringLength(50)]
        public string AlertType { get; set; } = "LowStock";

        public DateTime AlertDate { get; set; } = DateTime.Now;

        [Display(Name = "Is Resolved")]
        public bool IsResolved { get; set; } = false;

        public DateTime? ResolvedDate { get; set; }

        [StringLength(500)]
        public string? ResolutionNotes { get; set; }

        // Navigation
        public virtual Product? Product { get; set; }
    }
}