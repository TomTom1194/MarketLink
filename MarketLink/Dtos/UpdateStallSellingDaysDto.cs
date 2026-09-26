using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    // Only the stall's selling days can be updated.
    public class UpdateStallSellingDaysDto
    {
        [Required(ErrorMessage = "Please enter the selling days")]
        [RegularExpression( @"^[1-7](,[1-7])*$", ErrorMessage = "Selling days must be numbers from 1 to 7, separated by commas")]
        public string SellingDays { get; set; } = "";
    }
}
