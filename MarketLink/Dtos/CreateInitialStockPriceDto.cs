using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    public class CreateInitialStockPriceDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Invalid product.")]
        public int ProductId { get; set; }

        [Range(typeof(decimal), "1.00", "9999999999.99", ErrorMessage = "Price must be at least $1.00 and within the allowed limit.")]
        public decimal Price { get; set; }

        [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "Quantity must be greater than zero and within the allowed limit.")]
        public decimal QuantityIn { get; set; }
    }
}
