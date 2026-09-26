using MarketLink.Data;
using MarketLink.Dtos;
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
            string quantityError = ShopHelper.CheckQuantity(quantity);
            if (quantityError != "")
            {
                return quantityError;
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

            if (newQuantity > ShopHelper.MaxQuantity)
            {
                return "You can order at most " + ShopHelper.MaxQuantity + " of one product.";
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
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return "Could not add this product to your basket. Please try again.";
            }
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

            string quantityError = ShopHelper.CheckQuantity(quantity);
            if (quantityError != "")
            {
                return quantityError;
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

        public async Task<ReorderResultDto> ReorderAsync(int customerId, int orderId)
        {
            var result = new ReorderResultDto();

            var order = await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Stall)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);

            if (order == null)
            {
                result.Error = "Order not found.";
                return result;
            }

            if (order.Status != "completed")
            {
                result.Error = "Only completed orders can be reordered.";
                return result;
            }

            result.OrderCode = order.OrderCode;
            result.MarketId = order.Stall!.MarketId;

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.MarketId == result.MarketId);

            if (cart == null)
            {
                cart = new Cart
                {
                    CustomerId = customerId,
                    MarketId = result.MarketId
                };
                _context.Carts.Add(cart);
            }

            foreach (var item in order.Items)
            {
                var sp = await _context.StockPrices
                    .Include(x => x.Product)
                    .Include(x => x.Stall)
                    .ThenInclude(st => st!.Farmer)
                    .FirstOrDefaultAsync(x => x.ProductId == item.ProductId && x.StallId == order.StallId && x.EffectiveTo == null);

                string status = sp == null ? "unavailable" : ShopHelper.ItemStatus(sp);
                if (status == "unavailable")
                {
                    result.Notices.Add(item.ProductName + " is no longer on sale – not added.");
                    continue;
                }
                if (status == "sold_out")
                {
                    result.Notices.Add(item.ProductName + " is sold out – not added.");
                    continue;
                }

                var stockPrice = sp!;
                string unit = stockPrice.Product!.Unit;

                if (stockPrice.Price != item.UnitPrice)
                {
                    result.Notices.Add(item.ProductName + ": price changed from " + ShopHelper.Money(item.UnitPrice) + " to " + ShopHelper.Money(stockPrice.Price) + " per " + unit + ".");
                }

                CartItem? existingItem = null;
                foreach (var cartItem in cart.Items)
                {
                    if (cartItem.StockPriceId == stockPrice.StockPriceId)
                    {
                        existingItem = cartItem;
                    }
                }

                decimal inBasket = existingItem != null ? existingItem.Quantity : 0;
                decimal wanted = Math.Floor(item.Quantity);
                if (wanted < 1)
                {
                    wanted = 1;
                }

                decimal maxQuantity = Math.Floor(stockPrice.QuantityAvailable);
                if (maxQuantity > ShopHelper.MaxQuantity)
                {
                    maxQuantity = ShopHelper.MaxQuantity;
                }

                decimal newQuantity = inBasket + wanted;
                if (newQuantity > maxQuantity)
                {
                    if (maxQuantity <= inBasket)
                    {
                        result.Notices.Add(item.ProductName + ": your basket already has " + ShopHelper.Qty(inBasket) + " " + unit + ", which is all that is left – not added.");
                        continue;
                    }

                    result.Notices.Add("Only " + ShopHelper.Qty(maxQuantity) + " " + unit + " of " + item.ProductName + " left – basket quantity set to "
                        + ShopHelper.Qty(maxQuantity) + " instead of " + ShopHelper.Qty(newQuantity) + ".");
                    newQuantity = maxQuantity;
                }

                if (existingItem != null)
                {
                    existingItem.Quantity = newQuantity;
                }
                else
                {
                    cart.Items.Add(new CartItem
                    {
                        StockPriceId = stockPrice.StockPriceId,
                        Quantity = newQuantity
                    });
                }
                result.AddedCount++;
            }

            if (result.AddedCount == 0)
            {
                return result;
            }

            cart.UpdatedAt = DateTime.Now;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                result.Error = "Could not add these products to your basket. Please try again.";
                result.AddedCount = 0;
            }

            return result;
        }
    }
}
