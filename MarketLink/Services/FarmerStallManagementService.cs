using MarketLink.Data;
using MarketLink.Dtos;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services.Farmer
{
    // Quản lý các sạp thuộc farmer đang đăng nhập
    public class FarmerStallManagementService : IFarmerStallManagementService
    {
        private readonly MarketLinkDbContext _context;

        public FarmerStallManagementService(MarketLinkDbContext context)
        {
            _context = context;
        }

        // Lấy danh sách sạp và thông tin chợ từ DB
        public async Task<List<FarmerStallResponseDto>> GetStallsAsync(int farmerId)
        {
            return await _context.Stalls
                .Where(stall => stall.FarmerId == farmerId)
                .OrderByDescending(stall => stall.IsActive)
                .ThenBy(stall => stall.StallId)
                .Take(1)
                .Select(stall => new FarmerStallResponseDto
                {
                    StallId = stall.StallId,
                    MarketId = stall.MarketId,
                    MarketName = stall.Market != null ? stall.Market.MarketName : "",
                    MarketMapUrl = stall.Market != null ? stall.Market.MapUrl : "",
                    MarketImageUrl = stall.Market != null ? stall.Market.ImageUrl : null,
                    StallCode = stall.StallCode,
                    LocationNote = stall.LocationNote,
                    SellingDays = stall.SellingDays,
                    IsActive = stall.IsActive
                })
                .ToListAsync();
        }

        // Chỉ cập nhật ngày bán, đồng thời xác nhận sạp thuộc farmer hiện tại
        public async Task<FarmerStallResponseDto?> UpdateSellingDaysAsync(
            int farmerId,
            int stallId,
            UpdateStallSellingDaysDto model)
        {
            // Farmer chỉ được quản lý một sạp; không nhận ID của sạp phụ nếu dữ liệu cũ bị trùng.
            var stall = await _context.Stalls
                .Where(item => item.FarmerId == farmerId)
                .OrderByDescending(item => item.IsActive)
                .ThenBy(item => item.StallId)
                .FirstOrDefaultAsync();

            if (stall == null || stall.StallId != stallId)
            {
                return null;
            }

            var days = model.SellingDays.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (days.Length == 0 ||
                days.Any(day => !int.TryParse(day, out var number) || number < 1 || number > 7) ||
                days.Distinct().Count() != days.Length)
            {
                return null;
            }

            stall.SellingDays = string.Join(',', days);
            await _context.SaveChangesAsync();

            return await _context.Stalls
                .Where(item => item.StallId == stallId && item.FarmerId == farmerId)
                .Select(item => new FarmerStallResponseDto
                {
                    StallId = item.StallId,
                    MarketId = item.MarketId,
                    MarketName = item.Market != null ? item.Market.MarketName : "",
                    MarketMapUrl = item.Market != null ? item.Market.MapUrl : "",
                    MarketImageUrl = item.Market != null ? item.Market.ImageUrl : null,
                    StallCode = item.StallCode,
                    LocationNote = item.LocationNote,
                    SellingDays = item.SellingDays,
                    IsActive = item.IsActive
                })
                .FirstOrDefaultAsync();
        }
    }
}
