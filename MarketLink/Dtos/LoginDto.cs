using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    
    public class LoginDto
    {
        [Required(ErrorMessage = "Please enter your email or phone number")]
        [Display(Name = "Email or phone number")]
        public string EmailOrPhone { get; set; } = "";

        [Required(ErrorMessage = "Please enter your password")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = "";

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}
