using System.ComponentModel.DataAnnotations;

namespace SIOMS.ViewModels
{
    public class AdminForgotPasswordViewModel
    {
        [Required(ErrorMessage = "Username or Email is required")]
        [Display(Name = "Username or Email")]
        public string Identifier { get; set; } = string.Empty;
    }

    public class AdminVerifyOtpViewModel
    {
        [Required(ErrorMessage = "OTP is required")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be 6 digits")]
        [Display(Name = "OTP Code")]
        public string Otp { get; set; } = string.Empty;
    }

    public class AdminResetPasswordViewModel
    {
        [Required(ErrorMessage = "New password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$",
            ErrorMessage = "Password must include at least one uppercase letter, one lowercase letter, one number, and one special character")]
        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your new password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm New Password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}