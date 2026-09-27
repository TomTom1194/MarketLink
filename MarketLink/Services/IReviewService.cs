using MarketLink.Dtos;
using MarketLink.Models;

namespace MarketLink.Services
{
    public interface IReviewService
    {
        Task<RatingSummaryDto> GetFarmerRatingAsync(int farmerId);

        Task<Dictionary<int, RatingSummaryDto>> GetFarmerRatingsAsync(List<int> farmerIds);

        Task<List<ReviewDto>> GetFarmerReviewsAsync(int farmerId, int skip, int take);

        Task<FarmerReview?> GetOrderFarmerReviewAsync(int orderId);

        Task<string> AddFarmerReviewAsync(int customerId, int orderId, int rating, string? comment);
    }
}
