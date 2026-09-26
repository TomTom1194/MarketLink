using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    // Dữ liệu farmer được phép cập nhật trong hồ sơ
    public class UpdateFarmerProfileDto
    {
        [Required(ErrorMessage = "Enter a farm name")]
        [StringLength(150, ErrorMessage = "Farm name must be 150 characters or fewer")]
        [RegularExpression( @"^\p{Lu}[\p{L}\p{M}0-9]*(?:[ .'-][\p{L}\p{M}0-9]+)*$",ErrorMessage = "Farm name must start with an uppercase letter and contain no extra spaces")]
        public string BrandName { get; set; } = "";

        [Required(ErrorMessage = "Enter a contact person")]
        [StringLength(100, ErrorMessage = "Contact person name must be 100 characters or fewer")]
        [RegularExpression(@"^\p{Lu}[\p{L}\p{M}0-9]*(?:[ .'-][\p{L}\p{M}0-9]+)*$",ErrorMessage = "Contact person name must start with an uppercase letter and contain no extra spaces")]
        public string ContactPerson { get; set; } = "";

        [Required(ErrorMessage = "Enter an address")]
        [StringLength(255, ErrorMessage = "Address must be 255 characters or fewer")]
        public string Address { get; set; } = "";

        // Chỉ dùng để kiểm tra/lọc; Farmer_Profile lưu DistrictId.
        [Required(ErrorMessage = "Select a province or city")]
        public int? CityId { get; set; }

        [Required(ErrorMessage = "Select a ward or commune")]
        [Range(1, int.MaxValue, ErrorMessage = "Select a valid ward or commune")]
        public int? DistrictId { get; set; }

        public string? Description { get; set; }
    }
}
