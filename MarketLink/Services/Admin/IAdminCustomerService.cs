using MarketLink.Dtos.Admin;

namespace MarketLink.Services.Admin
{
    public interface IAdminCustomerService
    {
        // status: active | disabled, or null for all
        Task<List<CustomerRowDto>> GetCustomersAsync(string? status, string? search);

        // Each returns false when the customer does not exist
        Task<bool> LockAsync(int userId);

        Task<bool> UnlockAsync(int userId);
    }
}
