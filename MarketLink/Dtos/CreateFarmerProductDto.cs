using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MarketLink.Dtos
{
    public class CreateFarmerProductDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Select a category")]
        public int CategoryId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a listing period")]
        public int ExpId { get; set; }

        [Required(ErrorMessage = "Enter a product name")]
        [StringLength(150, MinimumLength = 3, ErrorMessage = "Product name must contain 3 to 150 characters")]
        [ValidProductName]
        public string ProductName { get; set; } = "";

        public string? Description { get; set; }

        [Required(ErrorMessage = "Select a unit")]
        [StringLength(20, ErrorMessage = "Unit must be 20 characters or fewer")]
        public string Unit { get; set; } = "";

        [Required(ErrorMessage = "Select a product image")]
        public IFormFile? Image { get; set; }
    }
}
