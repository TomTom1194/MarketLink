using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Helpers;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class ShopService : IShopService
    {
        private readonly MarketLinkDbContext _context;

        public ShopService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<List<City>> GetCitiesAsync()
        {
            return await _context.Cities.OrderBy(c => c.CityName).ToListAsync();
        }

        public async Task<List<District>> GetDistrictsAsync(int cityId)
        {
            return await _context.Districts
                .Where(d => d.CityId == cityId)
                .OrderBy(d => d.DistrictName)
                .ToListAsync();
        }

        public async Task<List<Market>> GetMarketsAsync(int districtId)
        {
            return await _context.Markets
                .Where(m => m.DistrictId == districtId && m.IsActive)
                .OrderBy(m => m.MarketName)
                .ToListAsync();
        }

        public async Task<FarmerProfile?> GetFarmerAsync(int farmerId)
        {
            return await _context.FarmerProfiles.FirstOrDefaultAsync(f => f.FarmerId == farmerId);
        }

        public async Task<Market?> GetMarketAsync(int marketId)
        {
            return await _context.Markets
                .Include(m => m.District)
                .ThenInclude(d => d!.City)
                .FirstOrDefaultAsync(m => m.MarketId == marketId && m.IsActive);
        }

        public async Task<List<ProductCategory>> GetCategoriesAsync()
        {
            return await _context.ProductCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .ToListAsync();
        }

        private IQueryable<StockPrice> FilterProducts(int marketId, string? keyword, int? categoryId, int? farmerId)
        {
            DateTime now = DateTime.Now;

            var query = _context.StockPrices
                .Include(sp => sp.Product)
                .ThenInclude(p => p!.Category)
                .Include(sp => sp.Stall)
                .ThenInclude(s => s!.Farmer)
                .Include(sp => sp.Stall)
                .ThenInclude(s => s!.Market)
                .Where(sp => sp.EffectiveTo == null
                    && sp.Stall!.MarketId == marketId
                    && sp.Stall.IsActive
                    && sp.Stall.Farmer!.ApprovalStatus == "approved"
                    && sp.Product!.Status == "active"
                    && sp.Product.ExpiresAt > now);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                string searchText = keyword.Trim();
                query = query.Where(sp => sp.Product!.ProductName.Contains(searchText)
                    || sp.Stall!.Farmer!.BrandName.Contains(searchText));
            }

            if (categoryId != null)
            {
                query = query.Where(sp => sp.Product!.CategoryId == categoryId
                    || sp.Product.Category!.ParentId == categoryId);
            }

            if (farmerId != null)
            {
                query = query.Where(sp => sp.Stall!.FarmerId == farmerId);
            }

            return query;
        }

        public async Task<int> CountProductsAsync(int marketId, string? keyword, int? categoryId, int? farmerId)
        {
            return await FilterProducts(marketId, keyword, categoryId, farmerId).CountAsync();
        }

        public async Task<List<StockPrice>> SearchProductsAsync(int marketId, string? keyword, int? categoryId, int? farmerId, string? sort, PagerDto pager)
        {
            var query = FilterProducts(marketId, keyword, categoryId, farmerId);
            IOrderedQueryable<StockPrice> sortedQuery;

            if (sort == "price_asc")
            {
                sortedQuery = query.OrderBy(sp => sp.Price);
            }
            else if (sort == "price_desc")
            {
                sortedQuery = query.OrderByDescending(sp => sp.Price);
            }
            else if (sort == "name")
            {
                sortedQuery = query.OrderBy(sp => sp.Product!.ProductName);
            }
            else
            {
                sortedQuery = query.OrderByDescending(sp => sp.Product!.PublishedAt);
            }

            return await sortedQuery
                .ThenBy(sp => sp.StockPriceId)
                .Skip(pager.Skip())
                .Take(pager.PageSize)
                .ToListAsync();
        }

        public async Task<StockPrice?> GetProductDetailAsync(int stockPriceId)
        {
            return await _context.StockPrices
                .Include(sp => sp.Product)
                .ThenInclude(p => p!.Category)
                .Include(sp => sp.Stall)
                .ThenInclude(s => s!.Market)
                .Include(sp => sp.Stall)
                .ThenInclude(s => s!.Farmer)
                .FirstOrDefaultAsync(sp => sp.StockPriceId == stockPriceId
                    && sp.EffectiveTo == null
                    && sp.Product!.Status == "active"
                    && sp.Product.ExpiresAt > DateTime.Now);
        }

        public async Task<List<StockPrice>> GetOtherProductsOfStallAsync(int stallId, int stockPriceId)
        {
            DateTime now = DateTime.Now;

            return await _context.StockPrices
                .Include(sp => sp.Product)
                .Where(sp => sp.StallId == stallId
                    && sp.StockPriceId != stockPriceId
                    && sp.EffectiveTo == null
                    && sp.Product!.Status == "active"
                    && sp.Product.ExpiresAt > now)
                .OrderByDescending(sp => sp.Product!.PublishedAt)
                .Take(8)
                .ToListAsync();
        }

        public async Task<List<Market>> GetActiveMarketsAsync(int take)
        {
            return await _context.Markets
                .Include(m => m.District)
                .ThenInclude(d => d!.City)
                .Include(m => m.Stalls)
                .Where(m => m.IsActive)
                .OrderBy(m => m.MarketName)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<StockPrice>> GetNewestProductsAsync(int? marketId, int take)
        {
            DateTime now = DateTime.Now;

            var query = _context.StockPrices
                .Include(sp => sp.Product)
                .Include(sp => sp.Stall)
                .ThenInclude(s => s!.Farmer)
                .Include(sp => sp.Stall)
                .ThenInclude(s => s!.Market)
                .Where(sp => sp.EffectiveTo == null
                    && sp.Stall!.IsActive
                    && sp.Stall.Market!.IsActive
                    && sp.Stall.Farmer!.ApprovalStatus == "approved"
                    && sp.Product!.Status == "active"
                    && sp.Product.ExpiresAt > now);

            if (marketId != null)
            {
                query = query.Where(sp => sp.Stall!.MarketId == marketId);
            }

            return await query
                .OrderByDescending(sp => sp.Product!.PublishedAt)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<FarmerProfile>> GetFeaturedFarmersAsync(int take)
        {
            return await _context.FarmerProfiles
                .Include(f => f.Stalls)
                .ThenInclude(s => s.Market)
                .Where(f => f.ApprovalStatus == "approved" && f.Stalls.Any(s => s.IsActive))
                .OrderBy(f => f.BrandName)
                .Take(take)
                .ToListAsync();
        }

        public async Task<List<MarketSearchResultDto>> SearchAllMarketsAsync(string keyword, double? latitude, double? longitude, int? currentMarketId)
        {
            string searchText = keyword.Trim();
            DateTime now = DateTime.Now;

            var products = await _context.StockPrices
                .Include(sp => sp.Product)
                .ThenInclude(p => p!.Category)
                .Include(sp => sp.Stall)
                .ThenInclude(s => s!.Farmer)
                .Include(sp => sp.Stall)
                .ThenInclude(s => s!.Market)
                .ThenInclude(m => m!.District)
                .ThenInclude(d => d!.City)
                .Where(sp => sp.EffectiveTo == null
                    && sp.Stall!.IsActive
                    && sp.Stall.Market!.IsActive
                    && sp.Stall.Farmer!.ApprovalStatus == "approved"
                    && sp.Product!.Status == "active"
                    && sp.Product.ExpiresAt > now
                    && sp.Product.ProductName.Contains(searchText))
                .ToListAsync();

            var results = new List<MarketSearchResultDto>();
            foreach (var sp in products)
            {
                var market = sp.Stall!.Market!;
                MarketSearchResultDto? group = null;
                foreach (var r in results)
                {
                    if (r.Market.MarketId == market.MarketId)
                    {
                        group = r;
                    }
                }
                if (group == null)
                {
                    group = new MarketSearchResultDto { Market = market };
                    double marketLat;
                    double marketLng;
                    if (latitude != null && longitude != null && ShopHelper.CoordinatesFromMapUrl(market.MapUrl, out marketLat, out marketLng))
                    {
                        group.DistanceKm = CalculateDistanceKm(latitude.Value, longitude.Value, marketLat, marketLng);
                    }
                    results.Add(group);
                }
                group.Products.Add(sp);
            }

            foreach (var group in results)
            {
                group.Products = group.Products
                    .OrderBy(sp => sp.QuantityAvailable < 1 ? 1 : 0)
                    .ThenBy(sp => sp.Product!.ProductName)
                    .ThenBy(sp => sp.Price)
                    .ToList();
            }

            if (latitude != null && longitude != null)
            {
                return results
                    .OrderBy(r => r.DistanceKm ?? double.MaxValue)
                    .ThenBy(r => r.Market.MarketName)
                    .ToList();
            }

            return results
                .OrderBy(r => r.Market.MarketId == currentMarketId ? 0 : 1)
                .ThenByDescending(r => r.Products.Count)
                .ThenBy(r => r.Market.MarketName)
                .ToList();
        }

        public async Task<List<MarketDistanceDto>> GetNearestMarketsAsync(double latitude, double longitude)
        {
            var markets = await _context.Markets
                .Include(m => m.District)
                .ThenInclude(d => d!.City)
                .Include(m => m.Stalls)
                .Where(m => m.IsActive)
                .ToListAsync();

            var result = new List<MarketDistanceDto>();
            foreach (var m in markets)
            {
                var item = new MarketDistanceDto { Market = m };
                double marketLat;
                double marketLng;
                if (ShopHelper.CoordinatesFromMapUrl(m.MapUrl, out marketLat, out marketLng))
                {
                    item.DistanceKm = CalculateDistanceKm(latitude, longitude, marketLat, marketLng);
                }
                result.Add(item);
            }

            for (int i = 0; i < result.Count - 1; i++)
            {
                for (int j = i + 1; j < result.Count; j++)
                {
                    double a = result[i].DistanceKm ?? double.MaxValue;
                    double b = result[j].DistanceKm ?? double.MaxValue;
                    if (b < a)
                    {
                        var temp = result[i];
                        result[i] = result[j];
                        result[j] = temp;
                    }
                }
            }

            return result;
        }

        private double CalculateDistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            double earthRadiusKm = 6371;
            double dLat = (lat2 - lat1) * Math.PI / 180;
            double dLng = (lng2 - lng1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return earthRadiusKm * c;
        }
    }
}
