using MarketLink.Data;
using MarketLink.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services.Admin
{
    // Admin: view customers and lock / unlock their accounts.
    // A locked customer (status = disabled) cannot log in.
    public class AdminCustomerService : IAdminCustomerService
    {
        private readonly MarketLinkDbContext _context;

        public AdminCustomerService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<List<CustomerRowDto>> GetCustomersAsync(string? status, string? search)
        {
            var query = _context.CustomerProfiles.AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(c => c.User!.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();
                query = query.Where(c =>
                    c.FullName.Contains(keyword) ||
                    c.User!.Email.Contains(keyword) ||
                    c.User.Phone.Contains(keyword));
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CustomerRowDto
                {
                    UserId = c.CustomerId,
                    FullName = c.FullName,
                    Email = c.User!.Email,
                    Phone = c.User.Phone,
                    DistrictName = c.District!.DistrictName,
                    CityName = c.District.City!.CityName,
                    Status = c.User.Status,
                    CreatedAt = c.CreatedAt,
                    OrderCount = c.Orders.Count
                })
                .ToListAsync();
        }

        public async Task<bool> LockAsync(int userId)
        {
            return await SetStatusAsync(userId, "disabled");
        }

        public async Task<bool> UnlockAsync(int userId)
        {
            return await SetStatusAsync(userId, "active");
        }

        private async Task<bool> SetStatusAsync(int userId, string status)
        {
            // Only customer accounts can be changed here
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId && u.Role!.RoleName == "customer");

            if (user == null)
            {
                return false;
            }

            user.Status = status;
            user.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
