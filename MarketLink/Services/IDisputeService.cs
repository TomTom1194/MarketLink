using MarketLink.Models;

namespace MarketLink.Services
{
    public interface IDisputeService
    {
        Task<OrderDispute?> GetByOrderAsync(int orderId);

        bool CustomerCanReport(Order order);

        bool FarmerCanReport(Order order);

        Task<string> SendCustomerReasonAsync(int customerId, int orderId, string? reason);

        Task<string> SendFarmerReasonAsync(int farmerId, int orderId, string? reason);

        Task<List<OrderDispute>> GetDisputesAsync(string status);

        Task<OrderDispute?> GetDisputeDetailAsync(int disputeId);

        bool CanResolve(OrderDispute dispute);

        Task<int> CountWarningsAsync(int userId);

        Task<string> ResolveAsync(int disputeId, int adminId, string? atFault, string? adminNote);
    }
}
