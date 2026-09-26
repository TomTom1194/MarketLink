using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos
{
    // Farmer application form. No password here:
    // the system creates one and emails it when an admin approves the application.
    public class FarmerApplicationDto
    {
        // Value of the "Other – my market is not listed" option in the market dropdown
        public const int OtherMarket = 0;

        // ===== 1. About the farm =====

        [Required(ErrorMessage = "Please enter your farm or brand name")]
        [StringLength(150)]
        [Display(Name = "Farm / brand name")]
        public string BrandName { get; set; } = "";

        [Required(ErrorMessage = "Please enter the contact person")]
        [StringLength(100)]
        [Display(Name = "Contact person")]
        public string ContactPerson { get; set; } = "";

        [Required(ErrorMessage = "Please enter your email")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        [StringLength(255)]
        [Display(Name = "Email (login details will be sent here)")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Please enter your phone number")]
        [RegularExpression(@"^0\d{9}$", ErrorMessage = "Phone number must be 10 digits starting with 0")]
        [Display(Name = "Phone number")]
        public string Phone { get; set; } = "";

        [Required(ErrorMessage = "Please choose a city")]
        [Display(Name = "City")]
        public int? CityId { get; set; }

        [Required(ErrorMessage = "Please choose a district")]
        [Display(Name = "District")]
        public int? DistrictId { get; set; }

        [Required(ErrorMessage = "Please enter your address")]
        [StringLength(255)]
        [Display(Name = "Address")]
        public string Address { get; set; } = "";

        [StringLength(1000)]
        [Display(Name = "What do you grow or sell?")]
        public string? Description { get; set; }

        // ===== 2. Where do you sell? =====

        // A market id, or OtherMarket (0) when the market is not in the list
        [Required(ErrorMessage = "Please choose your market")]
        [Display(Name = "Market")]
        public int? MarketId { get; set; }

        public bool IsNewMarket => MarketId == OtherMarket;

        // Only used when "Other" is chosen
        [StringLength(150)]
        [Display(Name = "Market name")]
        public string? NewMarketName { get; set; }

        [Display(Name = "City")]
        public int? NewMarketCityId { get; set; }

        [Display(Name = "District")]
        public int? NewMarketDistrictId { get; set; }

        [StringLength(500)]
        [Url(ErrorMessage = "Please enter a full link, starting with https://")]
        [Display(Name = "Google Maps link of the market")]
        public string? NewMarketMapUrl { get; set; }

        [Display(Name = "Market days")]
        public List<int> NewMarketOpenDays { get; set; } = new List<int>();

        [Display(Name = "Market opens at")]
        public TimeSpan? NewMarketOpenTime { get; set; }

        [Display(Name = "Market closes at")]
        public TimeSpan? NewMarketCloseTime { get; set; }

        // ===== 3. Your stall =====

        [Required(ErrorMessage = "Please enter your stall code")]
        [StringLength(20)]
        [Display(Name = "Stall code")]
        public string StallCode { get; set; } = "";

        [StringLength(500)]
        [Url(ErrorMessage = "Please enter a full link, starting with https://")]
        [Display(Name = "Google Maps link of the stall")]
        public string? StallGoogleUrl { get; set; }

        [StringLength(255)]
        [Display(Name = "How to find the stall")]
        public string? StallLocationNote { get; set; }

        [Display(Name = "Days you sell")]
        public List<int> SellingDays { get; set; } = new List<int>();
    }
}
