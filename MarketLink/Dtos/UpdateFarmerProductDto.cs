using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MarketLink.Dtos
{
    public class UpdateFarmerProductDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Invalid product")]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Enter a product name")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Product name must contain 3 to 150 characters")]
        [ValidProductName]
        public string ProductName { get; set; } = "";

        public string? Description { get; set; }

        [Required(ErrorMessage = "Select a unit")]
        [StringLength(20, ErrorMessage = "Unit must be 20 characters or fewer")]
        public string Unit { get; set; } = "";

        public IFormFile? Image { get; set; }
    }
}
