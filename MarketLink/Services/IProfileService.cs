using MarketLink.Dtos;
using MarketLink.Models;

namespace MarketLink.Services
{
    public interface IProfileService
    {
        Task<CustomerProfile?> GetProfileAsync(int customerId);

        Task<Dictionary<string, string>> ValidateAsync(int customerId, ProfileDto model);

        Task<User?> UpdateAsync(int customerId, ProfileDto model);
    }
}
