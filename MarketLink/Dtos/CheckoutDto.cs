using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    public class CheckoutDto
    {
        [Required]
        public int MarketId { get; set; }

        [Required(ErrorMessage = "Please enter the receiver's name")]
        [StringLength(100, ErrorMessage = "Name must be at most 100 characters")]
        [Display(Name = "Receiver name")]
        public string PickupName { get; set; } = "";

        [Required(ErrorMessage = "Please enter the receiver's phone number")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Phone number must be 10 digits starting with 0")]
        [Display(Name = "Receiver phone")]
        public string PickupPhone { get; set; } = "";

        [StringLength(500, ErrorMessage = "Note must be at most 500 characters")]
        [Display(Name = "Note for the farmer")]
        public string? CustomerNote { get; set; }

        public DateTime? PickupDate { get; set; }

        public string? PickupSlot { get; set; }

        public List<int> SkippedItemIds { get; set; } = new List<int>();
    }
}
