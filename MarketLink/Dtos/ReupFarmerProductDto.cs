using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    public class ReupFarmerProductDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Invalid product")]
        public int ProductId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a listing period")]
        public int ExpId { get; set; }

        [Required(ErrorMessage = "Please choose a new expiration date and time")]
        [DataType(DataType.DateTime)]
        public DateTime? NewExpiresAt { get; set; }

        [Range(typeof(decimal), "0.01", "99999999.99", ErrorMessage = "Additional quantity must be greater than zero and within the allowed limit")]
        public decimal AddedQuantity { get; set; }
    }
}
