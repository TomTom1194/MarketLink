using MarketLink.Models;

namespace MarketLink.Services
{
    public interface IFavoriteService
    {
        Task<List<FavoriteFarmer>> GetFavoriteFarmersAsync(int customerId);

        Task<Dictionary<int, int>> CountProductsOnSaleAsync(List<int> farmerIds);

        Task<bool> IsFavoriteAsync(int customerId, int farmerId);

        Task<List<int>> GetFavoriteFarmerIdsAsync(int customerId);

        Task<string> AddFavoriteAsync(int customerId, int farmerId);

        Task<bool> RemoveFavoriteAsync(int customerId, int farmerId);
    }
}
