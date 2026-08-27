using System.ComponentModel.DataAnnotations;

namespace SIOMS.Models
{
    public class Settings
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Site Name")]
        public string SiteName { get; set; } = "SIOMS";

        [Required]
        [EmailAddress]
        [StringLength(100)]
        [Display(Name = "Site Email")]
        public string SiteEmail { get; set; } = "admin@sioms.com";

        [Required]
        [StringLength(10)]
        [Display(Name = "Currency")]
        public string Currency { get; set; } = "PKR";

        [StringLength(20)]
        [Display(Name = "Currency Symbol")]
        public string CurrencySymbol { get; set; } = "Rs.";

        [StringLength(200)]
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = "SIOMS Inventory System";

        [Display(Name = "Low Stock Alert Threshold")]
        [Range(1, 100)]
        public int LowStockThreshold { get; set; } = 10;

        [Display(Name = "Enable Email Notifications")]
        public bool EnableEmailNotifications { get; set; } = true;

        [Display(Name = "Enable SMS Alerts")]
        public bool EnableSMSAlerts { get; set; } = false;

        [StringLength(500)]
        [Display(Name = "Company Address")]
        public string CompanyAddress { get; set; } = string.Empty;

        [StringLength(50)]
        [Display(Name = "Company Phone")]
        public string CompanyPhone { get; set; } = string.Empty;


        [StringLength(100)]
        [Display(Name = "Recovery Email (optional)")]
        public string? RecoveryEmail { get; set; }

        [StringLength(20)]
        [Display(Name = "Recovery Phone (optional)")]
        public string? RecoveryPhone { get; set; }

        // Used only during forgot-password flow
        public string? OtpCodeHash { get; set; }
        public DateTime? OtpExpiry { get; set; }

        [StringLength(100)]
        public string Username { get; set; } = "admin";

        public string PasswordHash { get; set; } = string.Empty;
        public string? CachedExchangeRatesJson { get; set; }

        public DateTime? ExchangeRatesUpdatedAt { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}