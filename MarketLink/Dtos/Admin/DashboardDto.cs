namespace MarketLink.Dtos.Admin
{
    // Numbers and short lists shown on the admin dashboard
    public class DashboardDto
    {
        public int ApprovedFarmers { get; set; }
        public int PendingFarmers { get; set; }
        public int ActiveCustomers { get; set; }
        public int ActiveMarkets { get; set; }
        public int TotalMarkets { get; set; }
        public int TotalOrders { get; set; }
        public int OrdersToday { get; set; }
        public decimal RevenueThisMonth { get; set; }

        // Newest farmers waiting for approval
        public List<FarmerRowDto> LatestPendingFarmers { get; set; } = new List<FarmerRowDto>();
    }
}
