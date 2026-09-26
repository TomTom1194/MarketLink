using MarketLink.Dtos;
using MarketLink.Models;

namespace MarketLink.Services
{
    public interface ICustomerOrderService
    {
        Task<List<Order>> GetOrdersAsync(int customerId, string? status);

        Task<Order?> GetOrderDetailAsync(int customerId, int orderId);

        Task<string> CancelOrderAsync(int customerId, int orderId, string reason);

        Task<List<PickupDayDto>> GetPickupScheduleAsync(int customerId);

        Task<int> CountWaitingOrdersAsync(int customerId);
    }
}
