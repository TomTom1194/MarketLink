using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class ReviewService : IReviewService
    {
        private readonly MarketLinkDbContext _context;

        public ReviewService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<RatingSummaryDto> GetFarmerRatingAsync(int farmerId)
        {
            var ratings = await _context.FarmerReviews
                .Where(r => r.FarmerId == farmerId)
                .Select(r => r.Rating)
                .ToListAsync();

            var summary = new RatingSummaryDto();
            int total = 0;
            foreach (int rating in ratings)
            {
                if (rating < 1 || rating > 5)
                {
                    continue;
                }
                summary.StarCounts[rating] = summary.StarCounts[rating] + 1;
                summary.Count = summary.Count + 1;
                total = total + rating;
            }

            if (summary.Count > 0)
            {
                summary.Average = Math.Round((double)total / summary.Count, 1);
            }
            return summary;
        }

        public async Task<Dictionary<int, RatingSummaryDto>> GetFarmerRatingsAsync(List<int> farmerIds)
        {
            var rows = await _context.FarmerReviews
                .Where(r => farmerIds.Contains(r.FarmerId))
                .GroupBy(r => r.FarmerId)
                .Select(g => new { Id = g.Key, Count = g.Count(), Total = g.Sum(r => r.Rating) })
                .ToListAsync();

            var result = new Dictionary<int, RatingSummaryDto>();
            foreach (var row in rows)
            {
                result[row.Id] = new RatingSummaryDto
                {
                    Count = row.Count,
                    Average = Math.Round((double)row.Total / row.Count, 1)
                };
            }
            return result;
        }

        public async Task<List<ReviewDto>> GetFarmerReviewsAsync(int farmerId, int skip, int take)
        {
            var reviews = await _context.FarmerReviews
                .Include(r => r.Customer)
                .Include(r => r.Order)
                .ThenInclude(o => o!.Items)
                .Where(r => r.FarmerId == farmerId)
                .OrderByDescending(r => r.CreatedAt)
                .ThenByDescending(r => r.ReviewId)
                .Skip(skip)
                .Take(take)
                .ToListAsync();

            var result = new List<ReviewDto>();
            foreach (var review in reviews)
            {
                var dto = new ReviewDto
                {
                    CustomerName = review.Customer != null ? review.Customer.FullName : "",
                    Rating = review.Rating,
                    Comment = review.Comment,
                    CreatedAt = review.CreatedAt
                };

                if (review.Order != null)
                {
                    foreach (var item in review.Order.Items)
                    {
                        dto.Products.Add(new ReviewProductDto
                        {
                            ProductName = item.ProductName,
                            ImageUrl = item.ImageUrl
                        });
                    }
                }

                result.Add(dto);
            }
            return result;
        }

        public async Task<FarmerReview?> GetOrderFarmerReviewAsync(int orderId)
        {
            return await _context.FarmerReviews.FirstOrDefaultAsync(r => r.OrderId == orderId);
        }

        public async Task<string> AddFarmerReviewAsync(int customerId, int orderId, int rating, string? comment)
        {
            if (rating < 1 || rating > 5)
            {
                return "Please choose from 1 to 5 stars.";
            }
            if (comment != null && comment.Trim().Length > 500)
            {
                return "Your comment must be 500 characters or less.";
            }

            var order = await _context.Orders
                .Include(o => o.Stall)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);
            if (order == null)
            {
                return "Order not found.";
            }
            if (order.Status != "completed")
            {
                return "You can only review an order after you have picked it up.";
            }

            bool alreadyReviewed = await _context.FarmerReviews.AnyAsync(r => r.OrderId == orderId);
            if (alreadyReviewed)
            {
                return "You have already reviewed this order.";
            }

            string? cleanComment = null;
            if (!string.IsNullOrWhiteSpace(comment))
            {
                cleanComment = comment.Trim();
            }

            _context.FarmerReviews.Add(new FarmerReview
            {
                OrderId = orderId,
                FarmerId = order.Stall!.FarmerId,
                CustomerId = customerId,
                Rating = rating,
                Comment = cleanComment
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return "You have already reviewed this order.";
            }
            return "";
        }
    }
}
