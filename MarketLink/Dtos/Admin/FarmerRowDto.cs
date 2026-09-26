namespace MarketLink.Dtos.Admin
{
    // One row in the farmer list
    public class FarmerRowDto
    {
        public int FarmerId { get; set; }
        public string BrandName { get; set; } = "";
        public string ContactPerson { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string DistrictName { get; set; } = "";
        public string CityName { get; set; } = "";
        public string? Description { get; set; }

        // pending | approved | rejected | suspended
        public string ApprovalStatus { get; set; } = "";

        public DateTime CreatedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public int StallCount { get; set; }
        public int ProductCount { get; set; }

        // First stall of the farmer (the one from the application)
        public FarmerStallDto? FirstStall { get; set; }
    }

    public class FarmerStallDto
    {
        public string StallCode { get; set; } = "";
        public string? GoogleUrl { get; set; }
        public string? LocationNote { get; set; }
        public string SellingDays { get; set; } = "";

        public string MarketName { get; set; } = "";
        public string MarketDistrictName { get; set; } = "";
        public string MarketCityName { get; set; } = "";
        public string MarketMapUrl { get; set; } = "";
        public string MarketOpenDays { get; set; } = "";
        public TimeSpan MarketOpenTime { get; set; }
        public TimeSpan MarketCloseTime { get; set; }

        // true = new market suggested by this farmer, shown to customers after approval
        public bool MarketIsNew { get; set; }
    }
}
