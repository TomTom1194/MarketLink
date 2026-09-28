using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos.Admin
{
    // "Add template" form on Admin > Product templates
    public class ProductTemplateFormDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Choose a category")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Enter a product name")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Name must be 2 to 150 characters")]
        [Display(Name = "Product name")]
        public string ProductName { get; set; } = "";

        [Required(ErrorMessage = "Choose a unit")]
        [Display(Name = "Unit")]
        public string Unit { get; set; } = "";
    }
}
