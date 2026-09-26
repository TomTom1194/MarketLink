using MarketLink.Data;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class FavoriteService : IFavoriteService
    {
        private readonly MarketLinkDbContext _context;

        public FavoriteService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<List<FavoriteFarmer>> GetFavoriteFarmersAsync(int customerId)
        {
            return await _context.FavoriteFarmers
                .Include(f => f.Farmer)
                .ThenInclude(fp => fp!.Stalls)
                .ThenInclude(s => s.Market)
                .Include(f => f.Farmer)
                .ThenInclude(fp => fp!.District)
                .Where(f => f.CustomerId == customerId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
        }

        public async Task<Dictionary<int, int>> CountProductsOnSaleAsync(List<int> farmerIds)
        {
            DateTime now = DateTime.Now;

            var listings = await _context.StockPrices
                .Where(sp => sp.EffectiveTo == null
                    && farmerIds.Contains(sp.Product!.FarmerId)
                    && sp.Product.Status == "active"
                    && sp.Product.ExpiresAt > now
                    && sp.Stall!.IsActive)
                .Select(sp => new { sp.Product!.FarmerId, sp.ProductId })
                .Distinct()
                .ToListAsync();

            var result = new Dictionary<int, int>();
            foreach (int farmerId in farmerIds)
            {
                result[farmerId] = 0;
            }
            foreach (var listing in listings)
            {
                result[listing.FarmerId] = result[listing.FarmerId] + 1;
            }
            return result;
        }

        public async Task<bool> IsFavoriteAsync(int customerId, int farmerId)
        {
            return await _context.FavoriteFarmers.AnyAsync(f => f.CustomerId == customerId && f.FarmerId == farmerId);
        }

        public async Task<List<int>> GetFavoriteFarmerIdsAsync(int customerId)
        {
            return await _context.FavoriteFarmers
                .Where(f => f.CustomerId == customerId)
                .Select(f => f.FarmerId)
                .ToListAsync();
        }

        public async Task<string> AddFavoriteAsync(int customerId, int farmerId)
        {
            var farmer = await _context.FarmerProfiles.FirstOrDefaultAsync(f => f.FarmerId == farmerId);
            if (farmer == null || farmer.ApprovalStatus != "approved")
            {
                return "This farmer does not exist.";
            }

            bool alreadySaved = await IsFavoriteAsync(customerId, farmerId);
            if (alreadySaved)
            {
                return "";
            }

            _context.FavoriteFarmers.Add(new FavoriteFarmer
            {
                CustomerId = customerId,
                FarmerId = farmerId
            });

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return "";
            }
            return "";
        }

        public async Task<bool> RemoveFavoriteAsync(int customerId, int farmerId)
        {
            var favorite = await _context.FavoriteFarmers
                .FirstOrDefaultAsync(f => f.CustomerId == customerId && f.FarmerId == farmerId);

            if (favorite == null)
            {
                return false;
            }

            _context.FavoriteFarmers.Remove(favorite);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
