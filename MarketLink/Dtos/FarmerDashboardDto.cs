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

// A product the farmer should re-up: it sold out or its listing period is over.
// Worked out from Products + Stock_Price every time (no rows are stored).
public class FarmerProductAlertDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Kind { get; set; } = "";      // "sold_out" or "expired"
    public DateTime Since { get; set; }          // when it expired / when the current price row started
}
