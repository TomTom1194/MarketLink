using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos.Admin
{
    // Create / edit display period form (Short term / Long term)
    public class ProductExpFormDto
    {
        // null when creating a new period
        public int? ExpId { get; set; }

        [Required(ErrorMessage = "Please enter a code")]
        [StringLength(10)]
        [RegularExpression("^[A-Z_]+$", ErrorMessage = "Use capital letters and _ only, e.g. SHORT")]
        [Display(Name = "Code")]
        public string ExpCode { get; set; } = "";

        [Required(ErrorMessage = "Please enter a name")]
        [StringLength(50)]
        [Display(Name = "Name")]
        public string ExpName { get; set; } = "";

        [Range(1, 365, ErrorMessage = "Days must be between 1 and 365")]
        [Display(Name = "Days shown")]
        public int DurationDays { get; set; } = 3;

        [StringLength(255)]
        [Display(Name = "Description")]
        public string? Description { get; set; }
    }
}
