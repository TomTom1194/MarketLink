namespace MarketLink.Dtos.Admin
{
    // One row in the market list
    public class MarketRowDto
    {
        public int MarketId { get; set; }
        public string MarketName { get; set; } = "";
        public string DistrictName { get; set; } = "";
        public string CityName { get; set; } = "";
        public string OpenDays { get; set; } = "";
        public TimeSpan OpenTime { get; set; }
        public TimeSpan CloseTime { get; set; }
        public string MapUrl { get; set; } = "";
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; }

        // Suggested by a farmer and waiting for that farmer to be approved
        public bool IsSuggested { get; set; }
        public int StallCount { get; set; }
    }
}
