using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class FarmerProductService : IFarmerProductService
    {
        private readonly MarketLinkDbContext _context;
        private readonly IWebHostEnvironment _environment;

        private const long MaxImageSize = 5 * 1024 * 1024;

        private static readonly string[] AllowedImageExtensions =
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp"
        };

        public FarmerProductService(
            MarketLinkDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // Categories this farmer ticked on the application form (only active ones) - used by the product form.
        // keepCategoryId: the category an existing product already has, so it still shows on the edit form
        public async Task<List<ProductCategory>> GetCategoriesAsync(int farmerId, int? keepCategoryId = null)
        {
            return await _context.ProductCategories
                .Where(category =>
                    (category.IsActive && _context.FarmerCategories.Any(fc => fc.FarmerId == farmerId && fc.CategoryId == category.CategoryId))
                    || category.CategoryId == keepCategoryId)
                .OrderBy(category => category.CategoryName)
                .ToListAsync();
        }

        // Is the farmer allowed to post in this category?
        private Task<bool> FarmerSellsCategoryAsync(int farmerId, int categoryId)
        {
            return _context.FarmerCategories.AnyAsync(fc =>
                fc.FarmerId == farmerId && fc.CategoryId == categoryId && fc.Category!.IsActive);
        }

        // The list of units is kept in the Product_Unit table in SQL Server.
        public Task<List<string>> GetUnitsAsync()
        {
            return _context.Database.SqlQueryRaw<string>(
                "SELECT [unit] AS [Value] FROM [dbo].[Product_Unit] WHERE [is_active] = 1 ORDER BY [sort_order]")
                .ToListAsync();
        }

        // Load the valid display periods from Product_Exp.
        public async Task<List<ProductExp>> GetExpiryOptionsAsync()
        {
            var expiryOptions = await _context.ProductExps
                .OrderBy(expiry => expiry.DurationDays)
                .ToListAsync();

            return expiryOptions
                .Where(expiry =>
                    expiry.ExpCode.Equals(
                        "SHORT",
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    (
                        expiry.ExpCode.Equals(
                            "LONG",
                            StringComparison.OrdinalIgnoreCase)
                        && expiry.DurationDays > 1
                    ))
                .ToList();
        }

        // A farmer only sees their own products.
        // Soft-deleted products are not listed.
        public async Task<List<Product>> GetProductsAsync(int farmerId)
        {
            await EnsureFarmerCanManageAsync(farmerId);

            var products = await _context.Products
                .Include(product => product.Category)
                .Include(product => product.Exp)
                .Include(product => product.StockPrices.Where(stock => stock.EffectiveTo == null))
                .Where(product =>
                    product.FarmerId == farmerId &&
                    product.Status != "removed")
                .OrderByDescending(product => product.CreatedAt)
                .ToListAsync();

            await HideUnavailableProductsAsync(products);
            return products;
        }

        public async Task<Product?> GetProductAsync(
            int farmerId,
            int productId)
        {
            await EnsureFarmerCanManageAsync(farmerId);

            var product = await _context.Products
                .Include(product => product.Category)
                .Include(product => product.Exp)
                .Include(product => product.StockPrices.Where(stock => stock.EffectiveTo == null))
                .FirstOrDefaultAsync(product =>
                    product.ProductId == productId &&
                    product.FarmerId == farmerId &&
                    product.Status != "removed");

            if (product != null) await HideUnavailableProductsAsync(new List<Product> { product });
            return product;
        }

        // Create a new product.
        // FarmerId comes from the logged-in account; the expiry date comes from Product_Exp.
        public async Task<Product> CreateProductAsync(
            int farmerId,
            CreateFarmerProductDto model)
        {
            await EnsureFarmerCanManageAsync(farmerId);

            var category = await _context.ProductCategories
                .FirstOrDefaultAsync(item =>
                    item.CategoryId == model.CategoryId &&
                    item.IsActive);

            if (category == null)
            {
                throw new InvalidOperationException("The category does not exist or is inactive.");
            }

            if (!await FarmerSellsCategoryAsync(farmerId, model.CategoryId))
            {
                throw new InvalidOperationException("You can only post products in the categories you registered for.");
            }

            if (!(await GetUnitsAsync()).Contains(model.Unit.Trim()))
                throw new InvalidOperationException("The selected unit does not exist or is inactive.");

            var normalizedName = model.ProductName.Trim().ToUpperInvariant();
            var nameExists = await _context.Products.AnyAsync(item => item.FarmerId == farmerId && item.Status != "removed" && item.ProductName.Trim().ToUpper() == normalizedName);
            if (nameExists) throw new ValidationException("You already have a product with this name.");

            var expiry = await GetValidExpiryOptionAsync(model.ExpId);
            var currentTime = DateTime.Now;
            var imageUrl = await SaveImageAsync(model.Image);

            var product = new Product
            {
                FarmerId = farmerId,
                CategoryId = category.CategoryId,
                ExpId = expiry.ExpId,
                ProductName = model.ProductName.Trim(),
                Description = model.Description?.Trim(),
                Unit = model.Unit.Trim(),
                ImageUrl = imageUrl,
                PublishedAt = currentTime,
                ExpiresAt = CalculateExpiryDate(currentTime, expiry),
                Status = "hidden", // Shown only after a price and stock are posted.
                CreatedAt = currentTime
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return product;
        }

        // Edit the product details.
        // This does not change the posted date or the expiry date.
        public async Task<bool> UpdateProductAsync(
            int farmerId,
            UpdateFarmerProductDto model)
        {
            await EnsureFarmerCanManageAsync(farmerId);

            var product = await _context.Products
                .FirstOrDefaultAsync(item =>
                    item.ProductId == model.ProductId &&
                    item.FarmerId == farmerId &&
                    item.Status != "removed");

            if (product == null)
            {
                return false;
            }

            var categoryExists = await _context.ProductCategories
                .AnyAsync(item =>
                    item.CategoryId == model.CategoryId &&
                    item.IsActive);

            if (!categoryExists)
            {
                throw new InvalidOperationException("The category does not exist or is inactive.");
            }

            // Changing to another category: it must be one the farmer registered for
            if (model.CategoryId != product.CategoryId && !await FarmerSellsCategoryAsync(farmerId, model.CategoryId))
            {
                throw new InvalidOperationException("You can only post products in the categories you registered for.");
            }

            if (!(await GetUnitsAsync()).Contains(model.Unit.Trim()))
                throw new InvalidOperationException("The selected unit does not exist or is inactive.");

            var normalizedName = model.ProductName.Trim().ToUpperInvariant();
            var nameExists = await _context.Products.AnyAsync(item => item.FarmerId == farmerId && item.ProductId != model.ProductId && item.Status != "removed" && item.ProductName.Trim().ToUpper() == normalizedName);
            if (nameExists) throw new ValidationException("You already have a product with this name.");

            product.CategoryId = model.CategoryId;
            product.ProductName = model.ProductName.Trim();
            product.Description = model.Description?.Trim();
            product.Unit = model.Unit.Trim();
            product.UpdatedAt = DateTime.Now;

            // Only replace the image if the farmer chose a new one.
            if (model.Image != null)
            {
                product.ImageUrl = await SaveImageAsync(model.Image);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // Hide or show the product.
        public async Task<bool> UpdateProductStatusAsync(
            int farmerId,
            UpdateFarmerProductStatusDto model)
        {
            await EnsureFarmerCanManageAsync(farmerId);

            var allowedStatuses = new[] { "active", "hidden" };

            var newStatus = model.Status.Trim().ToLowerInvariant();

            if (!allowedStatuses.Contains(newStatus))
            {
                throw new InvalidOperationException("Invalid product status.");
            }

            var product = await _context.Products
                .FirstOrDefaultAsync(item =>
                    item.ProductId == model.ProductId &&
                    item.FarmerId == farmerId &&
                    item.Status != "removed");

            if (product == null)
            {
                return false;
            }

            if (newStatus == "active" &&
                product.ExpiresAt <= DateTime.Now)
            {
                throw new InvalidOperationException("This listing has expired. Re-up before showing it again.");
            }

            if (newStatus == "active")
            {
                var hasStock = await _context.StockPrices.AnyAsync(stock => stock.ProductId == product.ProductId && stock.EffectiveTo == null && stock.QuantityIn > stock.QuantityReserved + stock.QuantitySold);
                if (!hasStock) throw new InvalidOperationException("This product is out of stock or has no price. Add stock before showing it again.");
            }

            product.Status = newStatus;
            product.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return true;
        }

        // Post the first price for a product at the farmer's stall.
        public async Task<StockPrice> CreateInitialStockPriceAsync(
            int farmerId,
            CreateInitialStockPriceDto model)
        {
            if (model.Price < 1m) throw new InvalidOperationException("Price must be at least $1.00.");
            await EnsureFarmerCanManageAsync(farmerId);

            var stallId = await GetFarmerActiveStallIdAsync(farmerId);
            var product = await GetOwnedProductAsync(farmerId, model.ProductId);

            var hasPriceHistory = await _context.StockPrices
                .AnyAsync(stock =>
                    stock.ProductId == product.ProductId &&
                    stock.StallId == stallId);

            if (hasPriceHistory)
            {
                throw new InvalidOperationException("This product already has a price history. Change its price or re-up instead.");
            }

            var currentTime = DateTime.Now;
            if (product.ExpiresAt <= currentTime)
            {
                var expiry = await GetValidExpiryOptionAsync(product.ExpId);
                product.PublishedAt = currentTime;
                product.ExpiresAt = CalculateExpiryDate(currentTime, expiry);
            }
            product.Status = "active";
            product.UpdatedAt = currentTime;

            var stockPrice = new StockPrice
            {
                ProductId = product.ProductId,
                StallId = stallId,
                Price = model.Price,
                QuantityIn = model.QuantityIn,
                QuantityReserved = 0,
                QuantitySold = 0,
                ChangeType = "new",
                EffectiveFrom = currentTime,
                EffectiveTo = null,
                CreatedBy = farmerId
            };

            _context.StockPrices.Add(stockPrice);
            await _context.SaveChangesAsync();

            return stockPrice;
        }

        // Edit product: update price and / or add stock while the listing is running.
        // A sold-out or expired product must be re-upped instead (new price, stock and listing period).
        // The current price row is closed and a new row is opened with:
        //   price    = the new price
        //   quantity = what was still for sale + the added quantity
        // Quantities already reserved or sold stay on the old row.
        public async Task<bool> ChangePriceAsync(
            int farmerId,
            ChangeFarmerProductPriceDto model)
        {
            if (model.NewPrice < 1m) throw new InvalidOperationException("New price must be at least $1.00.");
            if (model.AddedQuantity < 0) throw new InvalidOperationException("Added quantity cannot be negative.");
            await EnsureFarmerCanManageAsync(farmerId);

            var stallId = await GetFarmerActiveStallIdAsync(farmerId);
            var product = await GetOwnedProductAsync(farmerId, model.ProductId);

            if (product.ExpiresAt <= DateTime.Now)
            {
                throw new InvalidOperationException("This listing has expired. Use re-up to extend it.");
            }

            var currentStockPrice = await _context.StockPrices
                .Where(stock =>
                    stock.ProductId == product.ProductId &&
                    stock.StallId == stallId &&
                    stock.EffectiveTo == null)
                .OrderByDescending(stock => stock.EffectiveFrom)
                .FirstOrDefaultAsync();

            if (currentStockPrice == null)
            {
                throw new InvalidOperationException("This product has no price yet. Set the initial price first.");
            }

            bool priceChanged = model.NewPrice != currentStockPrice.Price;
            if (!priceChanged && model.AddedQuantity == 0)
            {
                throw new InvalidOperationException("Nothing to update. Enter a new price or a quantity to add.");
            }

            var remainingQuantity = currentStockPrice.QuantityAvailable;
            if (remainingQuantity <= 0)
            {
                throw new InvalidOperationException("This product is sold out. Use re-up to add stock, a new price and a new listing period.");
            }
            var newQuantity = remainingQuantity + model.AddedQuantity;
            if (newQuantity > 99999999.99m) throw new InvalidOperationException("Total quantity exceeds the storage limit.");

            var currentTime = DateTime.Now;
            currentStockPrice.EffectiveTo = currentTime;

            _context.StockPrices.Add(new StockPrice
            {
                ProductId = product.ProductId,
                StallId = stallId,
                Price = model.NewPrice,
                QuantityIn = newQuantity,
                QuantityReserved = 0,
                QuantitySold = 0,
                ChangeType = priceChanged ? "price_change" : "restock",
                EffectiveFrom = currentTime,
                EffectiveTo = null,
                CreatedBy = farmerId
            });

            product.UpdatedAt = currentTime;

            await _context.SaveChangesAsync();
            return true;
        }

        // Re-up is only allowed when the product has expired or sold out.
        // Close the old stock row, add the new stock and open a new restock row.
        public async Task<bool> ReupAsync(
            int farmerId,
            ReupFarmerProductDto model)
        {
            await EnsureFarmerCanManageAsync(farmerId);

            var stallId = await GetFarmerActiveStallIdAsync(farmerId);
            var product = await GetOwnedProductAsync(farmerId, model.ProductId);
            var currentTime = DateTime.Now;

            var currentStockPrice = await _context.StockPrices
                .Where(stock =>
                    stock.ProductId == product.ProductId &&
                    stock.StallId == stallId &&
                    stock.EffectiveTo == null)
                .OrderByDescending(stock => stock.EffectiveFrom)
                .FirstOrDefaultAsync();

            var hasExpired = product.ExpiresAt <= currentTime;
            var hasNoAvailableStock = currentStockPrice == null || currentStockPrice.QuantityAvailable <= 0;

            if (!hasExpired && !hasNoAvailableStock)
            {
                throw new InvalidOperationException("Re-up is available only after the listing expires or stock runs out.");
            }

            var latestStockPrice = currentStockPrice ?? await _context.StockPrices
                    .Where(stock =>
                        stock.ProductId == product.ProductId &&
                        stock.StallId == stallId)
                    .OrderByDescending(stock => stock.EffectiveFrom)
                    .FirstOrDefaultAsync();

            if (latestStockPrice == null)
            {
                throw new InvalidOperationException("Set the initial price before re-upping this product.");
            }

            if (model.NewPrice < 1m) throw new InvalidOperationException("Price must be at least $1.00.");
            if (model.ExtendHours < 0 || model.ExtendHours > 24) throw new InvalidOperationException("Extra time must be between 0 and 24 hours.");

            var remainingQuantity = currentStockPrice?.QuantityAvailable ?? 0;
            if (remainingQuantity + model.AddedQuantity > 99999999.99m) throw new InvalidOperationException("Total quantity exceeds the storage limit.");

            if (currentStockPrice != null)
            {
                currentStockPrice.EffectiveTo = currentTime;
            }

            if (hasExpired)
            {
                // Case 1 - expired: a new 24-hour listing starts now (a SHORT listing)
                var shortExpiry = await _context.ProductExps.FirstOrDefaultAsync(e => e.ExpCode == "SHORT");
                if (shortExpiry != null) product.ExpId = shortExpiry.ExpId;
                product.PublishedAt = currentTime;
                product.ExpiresAt = currentTime.AddHours(24);
            }
            else
            {
                // Case 2 - sold out while the listing is still running:
                // keep the listing and add 0 - 24 hours to it
                product.ExpiresAt = product.ExpiresAt.AddHours(model.ExtendHours);
            }
            product.Status = "active";
            product.UpdatedAt = currentTime;

            var restock = new StockPrice
            {
                ProductId = product.ProductId,
                StallId = stallId,
                Price = model.NewPrice,
                QuantityIn = remainingQuantity + model.AddedQuantity,
                QuantityReserved = 0,
                QuantitySold = 0,
                ChangeType = "restock",
                EffectiveFrom = currentTime,
                EffectiveTo = null,
                CreatedBy = farmerId
            };

            _context.StockPrices.Add(restock);
            await _context.SaveChangesAsync();

            return true;
        }

        // Price and stock history of a product owned by the logged-in farmer.
        public async Task<List<StockPrice>> GetStockHistoryAsync(
            int farmerId,
            int productId)
        {
            await EnsureFarmerCanManageAsync(farmerId);
            await GetOwnedProductAsync(farmerId, productId);

            return await _context.StockPrices
                .Where(stock => stock.ProductId == productId)
                .OrderByDescending(stock => stock.EffectiveFrom)
                .ToListAsync();
        }

        // Called by the order code after the stock is updated.
        public async Task HideIfUnavailableAsync(int productId)
        {
            var product = await _context.Products
                .Include(item => item.StockPrices.Where(stock => stock.EffectiveTo == null))
                .FirstOrDefaultAsync(item => item.ProductId == productId && item.Status != "removed");
            if (product != null) await HideUnavailableProductsAsync(new List<Product> { product });
        }

        private async Task HideUnavailableProductsAsync(List<Product> products)
        {
            var currentTime = DateTime.Now;
            var hasChanges = false;
            foreach (var product in products)
            {
                var hasStock = product.StockPrices.Any(stock => stock.QuantityAvailable > 0);
                if (product.Status != "active" || (product.ExpiresAt > currentTime && hasStock)) continue;
                product.Status = "hidden";
                product.UpdatedAt = currentTime;
                hasChanges = true;
            }
            if (hasChanges) await _context.SaveChangesAsync();
        }

        private async Task EnsureFarmerCanManageAsync(int farmerId)
        {
            var farmerProfile = await _context.FarmerProfiles
                .Include(profile => profile.User)
                .FirstOrDefaultAsync(profile =>
                    profile.FarmerId == farmerId);

            if (farmerProfile == null)
            {
                throw new InvalidOperationException("Farmer profile not found.");
            }

            if (farmerProfile.ApprovalStatus != "approved")
            {
                throw new InvalidOperationException("This farmer account has not been approved.");
            }

            if (farmerProfile.User == null ||
                farmerProfile.User.Status != "active")
            {
                throw new InvalidOperationException("This account is disabled.");
            }
        }

        private async Task<int> GetFarmerActiveStallIdAsync(int farmerId)
        {
            var stallId = await _context.Stalls
                .Where(stall =>
                    stall.FarmerId == farmerId &&
                    stall.IsActive)
                .Select(stall => (int?)stall.StallId)
                .FirstOrDefaultAsync();

            if (!stallId.HasValue)
            {
                throw new InvalidOperationException("This account has no active stall.");
            }

            return stallId.Value;
        }

        private async Task<Product> GetOwnedProductAsync(
            int farmerId,
            int productId)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(item =>
                    item.ProductId == productId &&
                    item.FarmerId == farmerId &&
                    item.Status != "removed");

            if (product == null)
            {
                throw new InvalidOperationException("No product belongs to your account.");
            }

            return product;
        }

        private async Task<ProductExp> GetValidExpiryOptionAsync(int expId)
        {
            var expiry = await _context.ProductExps
                .FirstOrDefaultAsync(item => item.ExpId == expId);

            if (expiry == null)
            {
                throw new InvalidOperationException("The listing period does not exist in the database.");
            }

            if (expiry.ExpCode.Equals(
                    "SHORT",
                    StringComparison.OrdinalIgnoreCase))
            {
                return expiry;
            }

            if (expiry.ExpCode.Equals(
                    "LONG",
                    StringComparison.OrdinalIgnoreCase) &&
                expiry.DurationDays > 1)
            {
                return expiry;
            }

            throw new InvalidOperationException("Invalid Product_Exp configuration: SHORT is 24 hours and LONG must exceed 24 hours.");
        }

        private static DateTime CalculateExpiryDate(
            DateTime publishedAt,
            ProductExp expiry)
        {
            if (expiry.ExpCode.Equals(
                    "SHORT",
                    StringComparison.OrdinalIgnoreCase))
            {
                return publishedAt.AddHours(24);
            }

            return publishedAt.AddDays(expiry.DurationDays);
        }

        private async Task<string> SaveImageAsync(IFormFile? image)
        {
            if (image == null || image.Length == 0)
            {
                throw new InvalidOperationException("Select a product image.");
            }

            if (image.Length > MaxImageSize)
            {
                throw new InvalidOperationException("The product image must be 5 MB or smaller.");
            }

            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

            if (!AllowedImageExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Only JPG, JPEG, PNG, and WEBP images are allowed.");
            }

            var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

            // Saved in wwwroot/images/products (the same folder as the web path returned below)
            var uploadFolder = Path.Combine(webRootPath, "images", "products");

            Directory.CreateDirectory(uploadFolder);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            await using var fileStream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write);

            await image.CopyToAsync(fileStream);

            return $"/images/products/{fileName}";
        }
    }
}
