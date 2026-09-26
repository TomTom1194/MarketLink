using MarketLink.Dtos;

namespace MarketLink.Services.Farmer
{
    // View the stall and update its selling days
    public interface IFarmerStallManagementService
    {
        Task<List<FarmerStallResponseDto>> GetStallsAsync(int farmerId);

        Task<FarmerStallResponseDto?> UpdateSellingDaysAsync(
            int farmerId,
            int stallId,
            UpdateStallSellingDaysDto model);
    }
}
