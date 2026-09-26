namespace MarketLink.Dtos
{
    // Thông tin sạp thuộc farmer đang đăng nhập
    public class FarmerStallResponseDto
    {
        public int StallId { get; set; }
        public int MarketId { get; set; }
        public string MarketName { get; set; } = "";
        public string MarketMapUrl { get; set; } = "";
        public string? MarketImageUrl { get; set; }
        public string StallCode { get; set; } = "";
        public string? LocationNote { get; set; }
        public string SellingDays { get; set; } = "";
        public bool IsActive { get; set; }
    }
}
