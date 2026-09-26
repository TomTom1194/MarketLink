using MarketLink.Data;
using MarketLink.Dtos.Admin;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services.Admin
{
    // Admin: create, edit and hide markets
    public class AdminMarketService : IAdminMarketService
    {
        private readonly MarketLinkDbContext _context;
        private readonly IWebHostEnvironment _environment;

        // Photos are saved in wwwroot/uploads/markets
        private const string ImageFolder = "uploads/markets";
        private const long MaxImageSize = 5 * 1024 * 1024;   // 5 MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        public AdminMarketService(MarketLinkDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<List<MarketRowDto>> GetMarketsAsync(string? search, int? cityId)
        {
            var query = _context.Markets.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();
                query = query.Where(m => m.MarketName.Contains(keyword));
            }

            if (cityId != null)
            {
                query = query.Where(m => m.District!.CityId == cityId);
            }

            return await query
                .OrderBy(m => m.District!.City!.CityName)
                .ThenBy(m => m.District!.DistrictName)
                .ThenBy(m => m.MarketName)
                .Select(m => new MarketRowDto
                {
                    MarketId = m.MarketId,
                    MarketName = m.MarketName,
                    DistrictName = m.District!.DistrictName,
                    CityName = m.District.City!.CityName,
                    OpenDays = m.OpenDays,
                    OpenTime = m.OpenTime,
                    CloseTime = m.CloseTime,
                    MapUrl = m.MapUrl,
                    ImageUrl = m.ImageUrl,
                    IsActive = m.IsActive,
                    IsSuggested = m.RequestedBy != null && !m.IsActive,
                    StallCount = m.Stalls.Count
                })
                .ToListAsync();
        }

        public async Task<MarketFormDto?> GetFormAsync(int marketId)
        {
            var market = await _context.Markets
                .Include(m => m.District)
                .FirstOrDefaultAsync(m => m.MarketId == marketId);

            if (market == null)
            {
                return null;
            }

            return new MarketFormDto
            {
                MarketId = market.MarketId,
                CityId = market.District!.CityId,
                DistrictId = market.DistrictId,
                MarketName = market.MarketName,
                MapUrl = market.MapUrl,
                ImageUrl = market.ImageUrl,
                OpenDays = MarketDays.FromText(market.OpenDays),
                OpenTime = market.OpenTime,
                CloseTime = market.CloseTime,
                IsActive = market.IsActive
            };
        }

        public async Task<Dictionary<string, string>> ValidateAsync(MarketFormDto model)
        {
            var errors = new Dictionary<string, string>();

            if (model.ImageFile != null)
            {
                string extension = Path.GetExtension(model.ImageFile.FileName).ToLower();
                if (!AllowedExtensions.Contains(extension))
                {
                    errors["ImageFile"] = "Please choose a .jpg, .png or .webp image.";
                }
                else if (model.ImageFile.Length > MaxImageSize)
                {
                    errors["ImageFile"] = "The photo must be 5 MB or smaller.";
                }
            }

            if (model.DistrictId != null &&
                !await _context.Districts.AnyAsync(d => d.DistrictId == model.DistrictId && d.CityId == model.CityId))
            {
                errors["DistrictId"] = "The district does not belong to the selected city.";
            }

            if (model.OpenDays.Count == 0)
            {
                errors["OpenDays"] = "Please tick at least one market day.";
            }

            if (model.OpenTime != null && model.CloseTime != null && model.CloseTime <= model.OpenTime)
            {
                errors["CloseTime"] = "Closing time must be later than opening time.";
            }

            // Two markets in the same district cannot have the same name
            string name = model.MarketName.Trim();
            bool duplicate = await _context.Markets.AnyAsync(m =>
                m.DistrictId == model.DistrictId &&
                m.MarketName == name &&
                m.MarketId != (model.MarketId ?? 0));

            if (duplicate)
            {
                errors["MarketName"] = "This district already has a market with this name.";
            }

            return errors;
        }

        public async Task CreateAsync(MarketFormDto model)
        {
            var market = new Market();
            CopyFormToMarket(model, market);

            if (model.ImageFile != null)
            {
                market.ImageUrl = await SaveImageAsync(model.ImageFile);
            }

            _context.Markets.Add(market);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(MarketFormDto model)
        {
            var market = await _context.Markets.FindAsync(model.MarketId);
            if (market == null)
            {
                return false;
            }

            CopyFormToMarket(model, market);

            // A new photo replaces the old one
            if (model.ImageFile != null)
            {
                DeleteImage(market.ImageUrl);
                market.ImageUrl = await SaveImageAsync(model.ImageFile);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleActiveAsync(int marketId)
        {
            var market = await _context.Markets.FindAsync(marketId);
            if (market == null)
            {
                return false;
            }

            market.IsActive = !market.IsActive;
            await _context.SaveChangesAsync();
            return true;
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

        // Saves the file with a random name and returns its web path, e.g. /uploads/markets/3f2a....jpg
        private async Task<string> SaveImageAsync(IFormFile file)
        {
            string folder = Path.Combine(_environment.WebRootPath, ImageFolder);
            Directory.CreateDirectory(folder);

            string fileName = Guid.NewGuid() + Path.GetExtension(file.FileName).ToLower();
            string fullPath = Path.Combine(folder, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return "/" + ImageFolder + "/" + fileName;
        }

        // Deletes an uploaded photo from wwwroot (does nothing for empty or outside paths)
        private void DeleteImage(string? imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl) || !imageUrl.StartsWith("/" + ImageFolder + "/"))
            {
                return;
            }

            string fullPath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/'));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        private static void CopyFormToMarket(MarketFormDto model, Market market)
        {
            market.DistrictId = model.DistrictId!.Value;
            market.MarketName = model.MarketName.Trim();
            market.MapUrl = model.MapUrl.Trim();
            market.OpenDays = MarketDays.ToText(model.OpenDays);
            market.OpenTime = model.OpenTime!.Value;
            market.CloseTime = model.CloseTime!.Value;
            market.IsActive = model.IsActive;
        }
    }
}
