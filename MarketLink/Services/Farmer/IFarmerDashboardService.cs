using MarketLink.Dtos;

namespace MarketLink.Services.Farmer;

public interface IFarmerDashboardService
{
    Task<FarmerDashboardDto> GetDashboardAsync(int farmerId);
    Task<FarmerOrderNotificationsDto> GetOrderNotificationsAsync(int farmerId);
}
