using MarketLink.Dtos;
using MarketLink.Models;
namespace MarketLink.Services
{
    public interface IFarmerProfileService
    {
        Task<FarmerProfile?> GetFarmerProfileAsync(int farmerId);
        Task<Dictionary<string,string>> ValidateUpdateAsync(int farmerId, UpdateFarmerProfileDto model);
        Task UpdateProfileAsync(int farmerId, UpdateFarmerProfileDto model);
    }
}
