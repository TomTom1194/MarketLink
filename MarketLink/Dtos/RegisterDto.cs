using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    // Customer registration form data
    public class RegisterDto
    {
        [Required(ErrorMessage = "Please enter your full name")]
        [StringLength(100, ErrorMessage = "Full name must be at most 100 characters")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Please enter your email")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [StringLength(255)]
        [Display(Name = "Email")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Please enter your phone number")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Phone number must be 10 digits starting with 0")]
        [Display(Name = "Phone number")]
        public string Phone { get; set; } = "";

        [Required(ErrorMessage = "Please enter your password")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = "";

        [Required(ErrorMessage = "Please choose a city")]
        [Display(Name = "City")]
        public int? CityId { get; set; }

        [Required(ErrorMessage = "Please choose a district")]
        [Display(Name = "District")]
        public int? DistrictId { get; set; }

        [Required(ErrorMessage = "Please enter your address")]
        [StringLength(255, ErrorMessage = "Address must be at most 255 characters")]
        [Display(Name = "Address")]
        public string Address { get; set; } = "";
    }
}
