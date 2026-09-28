using MarketLink.Dtos;

namespace MarketLink.Services
{
    public interface IOrderService
    {
        Task<List<OrderDto>> GetOrdersAsync(int farmerId, string? phone = null);

        Task<OrderDetailDto?> GetOrderDetailAsync(int id, int farmerId);

        Task<bool> AcceptOrderAsync(int id, int farmerId);

        Task<bool> AutoAcceptOrderAsync(int id);

        Task<bool> GetAutoAcceptEnabledAsync(int farmerId);

        Task<bool> SetAutoAcceptEnabledAsync(int farmerId, bool enabled);

        Task<bool> RejectOrderAsync(int id, int farmerId, string reason);

        Task<bool> CompleteOrderAsync(int id, int farmerId);


    }
}
