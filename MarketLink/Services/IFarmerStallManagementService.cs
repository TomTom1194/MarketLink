using MarketLink.Dtos;

namespace MarketLink.Services.Farmer
{
    // Khai báo chức năng xem sạp và cập nhật ngày bán
    public interface IFarmerStallManagementService
    {
        Task<List<FarmerStallResponseDto>> GetStallsAsync(int farmerId);

        Task<FarmerStallResponseDto?> UpdateSellingDaysAsync(
            int farmerId,
            int stallId,
            UpdateStallSellingDaysDto model);
    }
}
