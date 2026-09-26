namespace MarketLink.Dtos;

public class FarmerDashboardDto
{
    public string BrandName { get; set; } = "";
    public int ProductCount { get; set; }
    public int ActiveProductCount { get; set; }
    public int HiddenProductCount { get; set; }
    public int ActiveStallCount { get; set; }
    public int PendingOrderCount { get; set; }
    public List<FarmerRecentOrderDto> RecentOrders { get; set; } = new();
}

public class FarmerRecentOrderDto
{
    public string OrderCode { get; set; } = "";
    public string PickupName { get; set; } = "";
    public DateTime PickupDate { get; set; }
    public string Status { get; set; } = "";
}

public class FarmerOrderNotificationsDto
{
    public int Count { get; set; }
    public List<FarmerRecentOrderDto> Orders { get; set; } = new();
}
