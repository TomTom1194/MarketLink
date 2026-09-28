using MarketLink.Dtos;
using MarketLink.Dtos.Admin;

namespace MarketLink.Services.Admin
{
    public interface IAdminCustomerService
    {
        // status: active | disabled, or null for all
        Task<List<CustomerRowDto>> GetCustomersAsync(string? status, string? search, PagerDto pager);

        // How many customers match (for the page numbers)
        Task<int> CountCustomersAsync(string? status, string? search);

        // Each returns false when the customer does not exist
        Task<bool> LockAsync(int userId);

        Task<bool> UnlockAsync(int userId, string loginUrl);
    }
}
