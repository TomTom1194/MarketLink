using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    public class UpdateFarmerProductStatusDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Invalid product")]
        public int ProductId { get; set; }

        [Required(ErrorMessage = "Invalid status")]
        [RegularExpression("^(active|hidden)$", ErrorMessage = "Product status must be active or hidden")]
        public string Status { get; set; } = "";
    }
}
