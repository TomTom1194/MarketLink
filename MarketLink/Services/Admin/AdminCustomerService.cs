using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Dtos.Admin;
using MarketLink.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services.Admin
{
    // Admin: view customers and lock / unlock their accounts.
    // A locked customer (status = disabled) cannot log in.
    public class AdminCustomerService : IAdminCustomerService
    {
        private readonly MarketLinkDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<AdminCustomerService> _logger;

        public AdminCustomerService(MarketLinkDbContext context, IEmailService emailService, ILogger<AdminCustomerService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public Task<int> CountCustomersAsync(string? status, string? search)
        {
            return FilterCustomers(status, search).CountAsync();
        }

        public async Task<List<CustomerRowDto>> GetCustomersAsync(string? status, string? search, PagerDto pager)
        {
            return await FilterCustomers(status, search)
                .OrderByDescending(c => c.CreatedAt)
                .Skip(pager.Skip())
                .Take(pager.PageSize)
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
                    OrderCount = c.Orders.Count,
                    WarningCount = _context.UserWarnings.Count(w => w.UserId == c.CustomerId),
                    NoShowCount = c.Orders.Count(o => o.Status == "no_show")
                })
                .ToListAsync();
        }

        // Customers matching the status tab and the search box
        private IQueryable<MarketLink.Models.CustomerProfile> FilterCustomers(string? status, string? search)
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

            return query;
        }

        public async Task<bool> LockAsync(int userId)
        {
            bool changed = await SetStatusAsync(userId, "disabled");
            if (!changed)
            {
                return false;
            }

            var customer = await _context.CustomerProfiles
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CustomerId == userId);
            if (customer != null && customer.User != null)
            {
                try
                {
                    await _emailService.SendAsync(customer.User.Email, customer.FullName, AccountLockHelper.Subject,
                        AccountLockHelper.BuildEmail(customer.FullName, "Your account was locked by a MarketLink administrator."));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not send the account locked email to {Email}", customer.User.Email);
                }
            }
            return true;
        }

        public async Task<bool> UnlockAsync(int userId, string loginUrl)
        {
            bool changed = await SetStatusAsync(userId, "active");
            if (!changed)
            {
                return false;
            }

            var customer = await _context.CustomerProfiles
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CustomerId == userId);
            if (customer != null && customer.User != null)
            {
                try
                {
                    await _emailService.SendAsync(customer.User.Email, customer.FullName, AccountLockHelper.UnlockSubject,
                        AccountLockHelper.BuildUnlockEmail(customer.FullName, loginUrl));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not send the account unlocked email to {Email}", customer.User.Email);
                }
            }
            return true;
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
