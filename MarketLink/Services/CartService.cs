using MarketLink.Data;
using MarketLink.Helpers;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class CartService : ICartService
    {
        private readonly MarketLinkDbContext _context;

        public CartService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<Cart?> GetCartAsync(int customerId, int marketId)
        {
            return await _context.Carts
                .Include(c => c.Market)
                .Include(c => c.Items)
                .ThenInclude(i => i.StockPrice)
                .ThenInclude(sp => sp!.Product)
                .Include(c => c.Items)
                .ThenInclude(i => i.StockPrice)
                .ThenInclude(sp => sp!.Stall)
                .ThenInclude(s => s!.Farmer)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.MarketId == marketId);
        }

        public async Task<List<Cart>> GetAllCartsAsync(int customerId)
        {
            return await _context.Carts
                .Include(c => c.Market)
                .Include(c => c.Items)
                .Where(c => c.CustomerId == customerId && c.Items.Count > 0)
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
                .ToListAsync();
        }

        public async Task<int> CountItemsAsync(int customerId)
        {
            return await _context.CartItems.CountAsync(i => i.Cart!.CustomerId == customerId);
        }

        public async Task<List<string>> RefreshCartAsync(int customerId, int marketId)
        {
            var notices = new List<string>();

            var cart = await _context.Carts
                .Include(c => c.Items)
                .ThenInclude(i => i.StockPrice)
                .ThenInclude(sp => sp!.Product)
                .Include(c => c.Items)
                .ThenInclude(i => i.StockPrice)
                .ThenInclude(sp => sp!.Stall)
                .ThenInclude(s => s!.Farmer)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.MarketId == marketId);

            if (cart == null)
            {
                return notices;
            }

            bool changed = false;
            var itemsToRemove = new List<CartItem>();

            foreach (var item in cart.Items)
            {
                var sp = item.StockPrice!;

                if (sp.EffectiveTo != null)
                {
                    var currentRow = await _context.StockPrices
                        .Include(x => x.Product)
                        .Include(x => x.Stall)
                        .ThenInclude(s => s!.Farmer)
                        .FirstOrDefaultAsync(x => x.ProductId == sp.ProductId && x.StallId == sp.StallId && x.EffectiveTo == null);

                    if (currentRow != null)
                    {
                        CartItem? sameRowItem = null;
                        foreach (var other in cart.Items)
                        {
                            if (other.StockPriceId == currentRow.StockPriceId && !itemsToRemove.Contains(other))
                            {
                                sameRowItem = other;
                            }
                        }

                        if (sameRowItem != null)
                        {
                            sameRowItem.Quantity = sameRowItem.Quantity + item.Quantity;
                            itemsToRemove.Add(item);
                        }
                        else
                        {
                            item.StockPriceId = currentRow.StockPriceId;
                            item.StockPrice = currentRow;
                        }

                        if (currentRow.Price != sp.Price)
                        {
                            notices.Add(sp.Product!.ProductName + ": price changed from " + ShopHelper.Money(sp.Price) + " to " + ShopHelper.Money(currentRow.Price) + " per " + sp.Product.Unit + ".");
                        }
                        changed = true;
                    }
                }
            }

            foreach (var item in itemsToRemove)
            {
                _context.CartItems.Remove(item);
            }

            foreach (var item in cart.Items)
            {
                if (itemsToRemove.Contains(item))
                {
                    continue;
                }

                var sp = item.StockPrice!;
                if (ShopHelper.ItemStatus(sp) == "ok" && item.Quantity > sp.QuantityAvailable)
                {
                    decimal newQuantity = Math.Floor(sp.QuantityAvailable);
                    notices.Add("Only " + ShopHelper.Qty(newQuantity) + " " + sp.Product!.Unit + " of " + sp.Product.ProductName + " left – quantity adjusted from " + ShopHelper.Qty(item.Quantity) + " to " + ShopHelper.Qty(newQuantity) + ".");
                    item.Quantity = newQuantity;
                    changed = true;
                }
            }

            if (changed)
            {
                cart.UpdatedAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            return notices;
        }

        public async Task<string> AddToCartAsync(int customerId, int stockPriceId, decimal quantity)
        {
            if (quantity <= 0)
            {
                return "Quantity must be greater than 0.";
            }

            var stockPrice = await _context.StockPrices
                .Include(sp => sp.Stall)
                .Include(sp => sp.Product)
                .FirstOrDefaultAsync(sp => sp.StockPriceId == stockPriceId);

            if (stockPrice == null || stockPrice.EffectiveTo != null
                || stockPrice.Product!.Status != "active" || stockPrice.Product.ExpiresAt <= DateTime.Now)
            {
                return "This product is no longer on sale.";
            }

            int marketId = stockPrice.Stall!.MarketId;

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.MarketId == marketId);

            if (cart == null)
            {
                cart = new Cart
                {
                    CustomerId = customerId,
                    MarketId = marketId
                };
                _context.Carts.Add(cart);
            }

            CartItem? existingItem = null;
            foreach (var item in cart.Items)
            {
                if (item.StockPriceId == stockPriceId)
                {
                    existingItem = item;
                }
            }

            decimal newQuantity = quantity;
            if (existingItem != null)
            {
                newQuantity = existingItem.Quantity + quantity;
            }

            if (newQuantity > stockPrice.QuantityAvailable)
            {
                return "Only " + stockPrice.QuantityAvailable.ToString("0.##") + " " + stockPrice.Product.Unit + " left.";
            }

            if (existingItem != null)
            {
                existingItem.Quantity = newQuantity;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    StockPriceId = stockPriceId,
                    Quantity = quantity
                });
            }

            cart.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return "";
        }

        public async Task<string> UpdateQuantityAsync(int customerId, int cartItemId, decimal quantity)
        {
            var item = await _context.CartItems
                .Include(i => i.Cart)
                .Include(i => i.StockPrice)
                .ThenInclude(sp => sp!.Product)
                .FirstOrDefaultAsync(i => i.CartItemId == cartItemId && i.Cart!.CustomerId == customerId);

            if (item == null)
            {
                return "Item not found in your basket.";
            }

            if (quantity <= 0)
            {
                _context.CartItems.Remove(item);
                await _context.SaveChangesAsync();
                return "";
            }

            if (quantity > item.StockPrice!.QuantityAvailable)
            {
                return "Only " + item.StockPrice.QuantityAvailable.ToString("0.##") + " " + item.StockPrice.Product!.Unit + " left.";
            }

            item.Quantity = quantity;
            item.Cart!.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return "";
        }

        public async Task<bool> RemoveItemAsync(int customerId, int cartItemId)
        {
            var item = await _context.CartItems
                .Include(i => i.Cart)
                .FirstOrDefaultAsync(i => i.CartItemId == cartItemId && i.Cart!.CustomerId == customerId);

            if (item == null)
            {
                return false;
            }

            item.Cart!.UpdatedAt = DateTime.Now;
            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
