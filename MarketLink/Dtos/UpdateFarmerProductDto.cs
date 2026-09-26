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

        // ----- Price & stock (the listing period does not change here) -----

        // Price per unit. Empty = keep the current price.
        [Range(typeof(decimal), "1.00", "9999999999.99", ErrorMessage = "Price must be at least $1.00.")]
        public decimal? Price { get; set; }

        // Quantity to add to the stock (0 = no new stock)
        [Range(typeof(decimal), "0", "99999999.99", ErrorMessage = "Added quantity cannot be negative.")]
        public decimal AddedQuantity { get; set; }
    }
}
