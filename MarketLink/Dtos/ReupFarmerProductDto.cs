using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    public class ReupFarmerProductDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Invalid product")]
        public int ProductId { get; set; }

        // Sold out only: how many hours to add to the current listing period (0 - 24).
        // Expired products always get 24 hours from now, so this is ignored for them.
        [Range(0, 24, ErrorMessage = "Extra time must be between 0 and 24 hours")]
        public int ExtendHours { get; set; }

        [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "Additional quantity must be greater than zero and within the allowed limit")]
        public decimal AddedQuantity { get; set; }

        // New price per unit for the re-upped listing
        [Range(typeof(decimal), "1.00", "9999999999.99", ErrorMessage = "Price must be at least $1.00.")]
        public decimal NewPrice { get; set; }
    }
}
