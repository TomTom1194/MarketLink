using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos.Admin
{
    // Create / edit market form
    public class MarketFormDto
    {
        // null when creating a new market
        public int? MarketId { get; set; }

        [Required(ErrorMessage = "Please choose a city")]
        [Display(Name = "City")]
        public int? CityId { get; set; }

        [Required(ErrorMessage = "Please choose a district")]
        [Display(Name = "District")]
        public int? DistrictId { get; set; }

        [Required(ErrorMessage = "Please enter the market name")]
        [StringLength(150)]
        [Display(Name = "Market name")]
        public string MarketName { get; set; } = "";

        [Required(ErrorMessage = "Please paste the Google Maps link")]
        [StringLength(500)]
        [Url(ErrorMessage = "Please enter a full link, starting with https://")]
        [Display(Name = "Google Maps link")]
        public string MapUrl { get; set; } = "";

        // New photo chosen in the form (optional)
        [Display(Name = "Market photo")]
        public IFormFile? ImageFile { get; set; }

        // Current photo, shown on the edit page
        public string? ImageUrl { get; set; }

        // Ticked market days, e.g. [2, 4, 7]
        [Display(Name = "Market days")]
        public List<int> OpenDays { get; set; } = new List<int>();

        [Required(ErrorMessage = "Please enter the opening time")]
        [Display(Name = "Opens at")]
        public TimeSpan? OpenTime { get; set; }

        [Required(ErrorMessage = "Please enter the closing time")]
        [Display(Name = "Closes at")]
        public TimeSpan? CloseTime { get; set; }

        [Display(Name = "Active (shown to customers)")]
        public bool IsActive { get; set; } = true;
    }

    // Market days are stored as "2,4,7": 2 = Monday ... 7 = Saturday, 8 = Sunday
    public static class MarketDays
    {
        public static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            { 2, "Mon" }, { 3, "Tue" }, { 4, "Wed" }, { 5, "Thu" },
            { 6, "Fri" }, { 7, "Sat" }, { 8, "Sun" }
        };

        // [2, 4, 7] -> "2,4,7"
        public static string ToText(List<int> days)
        {
            return string.Join(",", days.Distinct().OrderBy(d => d));
        }

        // "2,4,7" -> [2, 4, 7]
        public static List<int> FromText(string text)
        {
            var days = new List<int>();
            foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out int day) && Names.ContainsKey(day))
                {
                    days.Add(day);
                }
            }
            return days;
        }

        // "2,4,7" -> "Mon, Wed, Sat"
        public static string ToDisplay(string text)
        {
            return string.Join(", ", FromText(text).Select(d => Names[d]));
        }
    }
}
