using MarketLink.Models;

namespace MarketLink.Services
{
    public interface ICartService
    {
        Task<Cart?> GetCartAsync(int customerId, int marketId);

        Task<List<Cart>> GetAllCartsAsync(int customerId);

        Task<int> CountItemsAsync(int customerId);

        Task<List<string>> RefreshCartAsync(int customerId, int marketId);

        Task<string> AddToCartAsync(int customerId, int stockPriceId, decimal quantity);

        Task<string> UpdateQuantityAsync(int customerId, int cartItemId, decimal quantity);

        Task<bool> RemoveItemAsync(int customerId, int cartItemId);
    }
}
