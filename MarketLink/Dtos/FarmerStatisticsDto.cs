namespace MarketLink.Dtos
{
    public class FarmerStatisticsDto
    {
        public int PendingOrders { get; set; }
        public decimal Revenue { get; set; }
        public List<BestSellingProductDto> BestSellingProducts { get; set; } = new();
    }

    public class BestSellingProductDto
    {
        public string ProductName { get; set; } = string.Empty;
        public decimal QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }
}
