using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    public class ChangeFarmerProductPriceDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Invalid product.")]
        public int ProductId { get; set; }

        [Range(typeof(decimal), "1.00", "9999999999.99", ErrorMessage = "New price must be at least $1.00 and within the allowed limit.")]
        public decimal NewPrice { get; set; }
    }
}
