using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    // Change password form (customer and farmer)
    public class ChangePasswordDto
    {
        [Required(ErrorMessage = "Please enter your current password")]
        [DataType(DataType.Password)]
        [Display(Name = "Current password")]
        public string CurrentPassword { get; set; } = "";

        [Required(ErrorMessage = "Please enter a new password")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your new password")]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm new password")]
        public string ConfirmNewPassword { get; set; } = "";
    }
}
