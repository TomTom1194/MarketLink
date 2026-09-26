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

        // Lấy danh mục đang hoạt động để hiển thị trên form.
        public async Task<List<ProductCategory>> GetCategoriesAsync()
        {
            return await _context.ProductCategories
                .Where(category => category.IsActive)
                .OrderBy(category => category.CategoryName)
                .ToListAsync();
        }

        // Danh sách đơn vị được quản lý trong bảng Product_Unit của SQL Server.
        public Task<List<string>> GetUnitsAsync()
        {
            return _context.Database.SqlQueryRaw<string>(
                "SELECT [unit] AS [Value] FROM [dbo].[Product_Unit] WHERE [is_active] = 1 ORDER BY [sort_order]")
                .ToListAsync();
        }

        // Lấy các loại thời hạn hợp lệ từ Product_Exp.
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

        // Farmer chỉ xem sản phẩm thuộc hồ sơ của mình.
        // Sản phẩm đã xóa mềm sẽ không xuất hiện trong danh sách.
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

        // Tạo sản phẩm mới.
        // FarmerId lấy từ tài khoản đăng nhập; hạn đăng bán tính theo Product_Exp.
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
                Status = "hidden", // Chỉ hiện sau khi đã đăng giá và có hàng.
                CreatedAt = currentTime
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return product;
        }

        // Sửa thông tin sản phẩm.
        // Thao tác này không thay đổi ngày đăng hoặc ngày hết hạn.
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

            // Chỉ thay ảnh nếu farmer đã chọn ảnh mới.
            if (model.Image != null)
            {
                product.ImageUrl = await SaveImageAsync(model.Image);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        // Đổi trạng thái sản phẩm theo thao tác ẩn hoặc hiển thị.
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

        // Đăng giá lần đầu cho sản phẩm tại sạp của farmer.
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

        // Đổi giá: đóng dòng giá hiện tại và mở dòng giá mới.
        // Lượng hàng còn bán được chuyển sang dòng giá mới.
        public async Task<bool> ChangePriceAsync(
            int farmerId,
            ChangeFarmerProductPriceDto model)
        {
            if (model.NewPrice < 1m) throw new InvalidOperationException("New price must be at least $1.00.");
            await EnsureFarmerCanManageAsync(farmerId);

            var stallId = await GetFarmerActiveStallIdAsync(farmerId);
            var product = await GetOwnedProductAsync(farmerId, model.ProductId);

            if (product.Status != "active" ||
                product.ExpiresAt <= DateTime.Now)
            {
                throw new InvalidOperationException("You can change the price only while the product is visible and the listing has not expired.");
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
                throw new InvalidOperationException("This product has no active price.");
            }

            var remainingQuantity = currentStockPrice.QuantityAvailable;

            if (remainingQuantity <= 0)
            {
                throw new InvalidOperationException("This product is out of stock. Use re-up.");
            }

            var currentTime = DateTime.Now;
            currentStockPrice.EffectiveTo = currentTime;

            var newStockPrice = new StockPrice
            {
                ProductId = product.ProductId,
                StallId = stallId,
                Price = model.NewPrice,
                QuantityIn = remainingQuantity,
                QuantityReserved = 0,
                QuantitySold = 0,
                ChangeType = "price_change",
                EffectiveFrom = currentTime,
                EffectiveTo = null,
                CreatedBy = farmerId
            };

            _context.StockPrices.Add(newStockPrice);
            await _context.SaveChangesAsync();

            return true;
        }

        // Re-up chỉ được thực hiện khi sản phẩm hết hạn hoặc hết hàng.
        // Đóng dòng kho cũ, nhập thêm hàng và mở dòng restock mới.
        public async Task<bool> ReupAsync(
            int farmerId,
            ReupFarmerProductDto model)
        {
            await EnsureFarmerCanManageAsync(farmerId);

            var stallId = await GetFarmerActiveStallIdAsync(farmerId);
            var product = await GetOwnedProductAsync(farmerId, model.ProductId);
            var expiry = await GetValidExpiryOptionAsync(model.ExpId);
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

            if (!model.NewExpiresAt.HasValue) throw new InvalidOperationException("Select a new expiry date and time.");
            var newExpiryDate = model.NewExpiresAt.Value;
            currentTime = DateTime.Now;
            ValidateReupDate(newExpiryDate, expiry, currentTime);

            var remainingQuantity = currentStockPrice?.QuantityAvailable ?? 0;
            if (remainingQuantity + model.AddedQuantity > 99999999.99m) throw new InvalidOperationException("Total quantity exceeds the storage limit.");

            if (currentStockPrice != null)
            {
                currentStockPrice.EffectiveTo = currentTime;
            }

            product.ExpId = expiry.ExpId;
            product.PublishedAt = currentTime;
            product.ExpiresAt = newExpiryDate;
            product.Status = "active";
            product.UpdatedAt = currentTime;

            var restock = new StockPrice
            {
                ProductId = product.ProductId,
                StallId = stallId,
                Price = latestStockPrice.Price,
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

        // Xem lịch sử giá và kho của sản phẩm thuộc farmer đang đăng nhập.
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

        // Điểm gọi cho phần đơn hàng sau khi cập nhật kho.
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

        private static void ValidateReupDate(
            DateTime newExpiryDate,
            ProductExp expiry,
            DateTime currentTime)
        {
            if (newExpiryDate.Date < currentTime.Date)
            {
                throw new InvalidOperationException("The expiry date cannot be in the past.");
            }

            if (newExpiryDate <= currentTime)
            {
                throw new InvalidOperationException("The expiry time must be later than the current server time.");
            }

            var maximumExpiryDate = CalculateExpiryDate(currentTime, expiry);

            if (newExpiryDate > maximumExpiryDate)
            {
                var errorMessage = expiry.ExpCode.Equals("SHORT", StringComparison.OrdinalIgnoreCase)
                    ? "SHORT cannot exceed 24 hours."
                    : $"Expiry cannot exceed {expiry.DurationDays} days from re-up.";

                throw new InvalidOperationException(errorMessage);
            }

            if (expiry.ExpCode.Equals(
                    "LONG",
                    StringComparison.OrdinalIgnoreCase) &&
                newExpiryDate <= currentTime.AddHours(24))
            {
                throw new InvalidOperationException("LONG must exceed 24 hours.");
            }
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

            var uploadFolder = Path.Combine(webRootPath, "uploads", "products");

            Directory.CreateDirectory(uploadFolder);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            await using var fileStream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write);

            await image.CopyToAsync(fileStream);

            return $"/uploads/products/{fileName}";
        }
    }
}
