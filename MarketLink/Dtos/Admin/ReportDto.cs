namespace MarketLink.Dtos.Admin
{
    // System report for a date range. Only completed orders count as revenue.
    public class ReportDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }

        public int CompletedOrders { get; set; }
        public decimal TotalRevenue { get; set; }

        // Number of orders per status in the range: placed, accepted, completed...
        public Dictionary<string, int> OrdersByStatus { get; set; } = new Dictionary<string, int>();

        public List<MarketRevenueRow> RevenueByMarket { get; set; } = new List<MarketRevenueRow>();
        public List<FarmerRevenueRow> TopFarmers { get; set; } = new List<FarmerRevenueRow>();
    }

    public class MarketRevenueRow
    {
        public string MarketName { get; set; } = "";
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }

    public class FarmerRevenueRow
    {
        public string BrandName { get; set; } = "";
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }
}
