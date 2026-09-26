using MarketLink.Data;
using MarketLink.Dtos.Admin;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services.Admin
{
    // Admin: display periods (Short term = 3 days, Long term = 30 days...).
    // Farmers pick one of these when they post a product.
    public class AdminProductExpService : IAdminProductExpService
    {
        private readonly MarketLinkDbContext _context;

        public AdminProductExpService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<List<ProductExp>> GetAllAsync()
        {
            return await _context.ProductExps.OrderBy(e => e.DurationDays).ToListAsync();
        }

        public async Task<Dictionary<int, int>> CountProductsAsync()
        {
            return await _context.Products
                .GroupBy(p => p.ExpId)
                .Select(g => new { ExpId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ExpId, x => x.Count);
        }

        public async Task<ProductExpFormDto?> GetFormAsync(int expId)
        {
            var exp = await _context.ProductExps.FindAsync(expId);
            if (exp == null)
            {
                return null;
            }

            return new ProductExpFormDto
            {
                ExpId = exp.ExpId,
                ExpCode = exp.ExpCode,
                ExpName = exp.ExpName,
                DurationDays = exp.DurationDays,
                Description = exp.Description
            };
        }

        public async Task<Dictionary<string, string>> ValidateAsync(ProductExpFormDto model)
        {
            var errors = new Dictionary<string, string>();
            string code = model.ExpCode.Trim().ToUpper();

            if (await _context.ProductExps.AnyAsync(e => e.ExpCode == code && e.ExpId != (model.ExpId ?? 0)))
            {
                errors["ExpCode"] = "This code is already used.";
            }

            return errors;
        }

        public async Task CreateAsync(ProductExpFormDto model)
        {
            var exp = new ProductExp();
            CopyFormToEntity(model, exp);

            _context.ProductExps.Add(exp);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(ProductExpFormDto model)
        {
            var exp = await _context.ProductExps.FindAsync(model.ExpId);
            if (exp == null)
            {
                return false;
            }

            CopyFormToEntity(model, exp);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int expId)
        {
            var exp = await _context.ProductExps.FindAsync(expId);
            if (exp == null || await _context.Products.AnyAsync(p => p.ExpId == expId))
            {
                return false;
            }

            _context.ProductExps.Remove(exp);
            await _context.SaveChangesAsync();
            return true;
        }

        private static void CopyFormToEntity(ProductExpFormDto model, ProductExp exp)
        {
            exp.ExpCode = model.ExpCode.Trim().ToUpper();
            exp.ExpName = model.ExpName.Trim();
            exp.DurationDays = model.DurationDays;
            exp.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
        }
    }
}
