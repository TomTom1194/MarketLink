using System.Net;
using MarketLink.Data;
using MarketLink.Dtos.Admin;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services.Admin
{
    // Admin: approve, reject and suspend farmers.
    // Only approved farmers may open stalls, post products and receive orders.
    public class AdminFarmerService : IAdminFarmerService
    {
        private readonly MarketLinkDbContext _context;
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AdminFarmerService> _logger;

        public AdminFarmerService(
            MarketLinkDbContext context,
            IAuthService authService,
            IEmailService emailService,
            ILogger<AdminFarmerService> logger)
        {
            _context = context;
            _authService = authService;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<List<FarmerRowDto>> GetFarmersAsync(string? status, string? search)
        {
            var query = _context.FarmerProfiles.AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(f => f.ApprovalStatus == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                string keyword = search.Trim();
                query = query.Where(f =>
                    f.BrandName.Contains(keyword) ||
                    f.ContactPerson.Contains(keyword) ||
                    f.User!.Email.Contains(keyword) ||
                    f.User.Phone.Contains(keyword));
            }

            return await query
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new FarmerRowDto
                {
                    FarmerId = f.FarmerId,
                    BrandName = f.BrandName,
                    ContactPerson = f.ContactPerson,
                    Email = f.User!.Email,
                    Phone = f.User.Phone,
                    Address = f.Address,
                    DistrictName = f.District!.DistrictName,
                    CityName = f.District.City!.CityName,
                    Description = f.Description,
                    ApprovalStatus = f.ApprovalStatus,
                    CreatedAt = f.CreatedAt,
                    ApprovedAt = f.ApprovedAt,
                    StallCount = f.Stalls.Count,
                    ProductCount = f.Products.Count,
                    FirstStall = f.Stalls
                        .OrderBy(s => s.StallId)
                        .Select(s => new FarmerStallDto
                        {
                            StallCode = s.StallCode,
                            GoogleUrl = s.GoogleUrl,
                            LocationNote = s.LocationNote,
                            SellingDays = s.SellingDays,
                            MarketName = s.Market!.MarketName,
                            MarketDistrictName = s.Market.District!.DistrictName,
                            MarketCityName = s.Market.District.City!.CityName,
                            MarketMapUrl = s.Market.MapUrl,
                            MarketOpenDays = s.Market.OpenDays,
                            MarketOpenTime = s.Market.OpenTime,
                            MarketCloseTime = s.Market.CloseTime,
                            MarketIsNew = s.Market.RequestedBy == f.FarmerId && !s.Market.IsActive
                        })
                        .FirstOrDefault()
                })
                .ToListAsync();
        }

        public async Task<Dictionary<string, int>> CountByStatusAsync()
        {
            return await _context.FarmerProfiles
                .GroupBy(f => f.ApprovalStatus)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Status, x => x.Count);
        }

        public async Task<FarmerApplicationDetailDto?> GetDetailAsync(int farmerId)
        {
            return await _context.FarmerProfiles
                .Where(f => f.FarmerId == farmerId)
                .Select(f => new FarmerApplicationDetailDto
                {
                    FarmerId = f.FarmerId,
                    BrandName = f.BrandName,
                    ContactPerson = f.ContactPerson,
                    Email = f.User!.Email,
                    Phone = f.User.Phone,
                    Address = f.Address,
                    DistrictName = f.District!.DistrictName,
                    CityName = f.District.City!.CityName,
                    Description = f.Description,
                    CreatedAt = f.CreatedAt,
                    ApprovalStatus = f.ApprovalStatus,
                    ApprovedAt = f.ApprovedAt,
                    ApprovedByEmail = f.ApprovedByUser != null ? f.ApprovedByUser.Email : null,
                    ProductCount = f.Products.Count,
                    Stalls = f.Stalls
                        .OrderBy(s => s.StallId)
                        .Select(s => new FarmerStallDto
                        {
                            StallCode = s.StallCode,
                            GoogleUrl = s.GoogleUrl,
                            LocationNote = s.LocationNote,
                            SellingDays = s.SellingDays,
                            MarketName = s.Market!.MarketName,
                            MarketDistrictName = s.Market.District!.DistrictName,
                            MarketCityName = s.Market.District.City!.CityName,
                            MarketMapUrl = s.Market.MapUrl,
                            MarketOpenDays = s.Market.OpenDays,
                            MarketOpenTime = s.Market.OpenTime,
                            MarketCloseTime = s.Market.CloseTime,
                            MarketIsNew = s.Market.RequestedBy == f.FarmerId && !s.Market.IsActive
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();
        }

        public async Task<FarmerApprovalResult> ApproveAsync(int farmerId, int adminId, string loginUrl)
        {
            var result = new FarmerApprovalResult();

            var farmer = await _context.FarmerProfiles
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.FarmerId == farmerId);

            if (farmer == null || farmer.ApprovalStatus == "approved")
            {
                return result;   // Success = false
            }

            result.Success = true;
            result.Email = farmer.User!.Email;

            // A suspended farmer already has a password: just let them sell again
            if (farmer.ApprovalStatus == "suspended")
            {
                farmer.ApprovalStatus = "approved";
                await _context.SaveChangesAsync();

                result.Reactivated = true;
                return result;
            }

            // First approval (pending or rejected): create a password for the farmer
            string password = PasswordGenerator.Generate();

            farmer.User.PasswordHash = _authService.HashPassword(password);
            farmer.User.Status = "active";
            farmer.User.UpdatedAt = DateTime.Now;

            farmer.ApprovalStatus = "approved";
            farmer.ApprovedBy = adminId;
            farmer.ApprovedAt = DateTime.Now;

            // Markets this farmer suggested become visible to customers
            var suggestedMarkets = await _context.Markets
                .Where(m => m.RequestedBy == farmer.FarmerId && !m.IsActive)
                .ToListAsync();

            foreach (var market in suggestedMarkets)
            {
                market.IsActive = true;
            }

            await _context.SaveChangesAsync();

            // Email the login link and password.
            // The approval is already saved, so if the email fails the admin sees the password instead.
            try
            {
                await _emailService.SendAsync(
                    farmer.User.Email,
                    farmer.ContactPerson,
                    "Your MarketLink farmer account is approved",
                    BuildApprovalEmail(farmer.ContactPerson, farmer.BrandName, farmer.User.Email, password, loginUrl));

                result.EmailSent = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send the approval email to {Email}", farmer.User.Email);

                result.EmailSent = false;
                result.TemporaryPassword = password;
                result.EmailError = ex.Message;
            }

            return result;
        }

        public async Task<bool> RejectAsync(int farmerId)
        {
            var farmer = await _context.FarmerProfiles
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.FarmerId == farmerId);

            if (farmer == null || farmer.ApprovalStatus != "pending")
            {
                return false;
            }

            farmer.ApprovalStatus = "rejected";
            await _context.SaveChangesAsync();

            // Let the farmer know. If the email fails, the rejection still counts.
            try
            {
                await _emailService.SendAsync(
                    farmer.User!.Email,
                    farmer.ContactPerson,
                    "About your MarketLink farmer application",
                    BuildRejectionEmail(farmer.ContactPerson, farmer.BrandName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not send the rejection email to {Email}", farmer.User!.Email);
            }

            return true;
        }

        public async Task<bool> SuspendAsync(int farmerId)
        {
            var farmer = await _context.FarmerProfiles.FindAsync(farmerId);
            if (farmer == null || farmer.ApprovalStatus != "approved")
            {
                return false;
            }

            farmer.ApprovalStatus = "suspended";
            await _context.SaveChangesAsync();
            return true;
        }

        // ===== Email content =====
        // HtmlEncode stops names like "<b>Farm</b>" from breaking the email layout

        private static string BuildApprovalEmail(string contactPerson, string brandName, string email, string password, string loginUrl)
        {
            string name = WebUtility.HtmlEncode(contactPerson);
            string brand = WebUtility.HtmlEncode(brandName);
            string safeEmail = WebUtility.HtmlEncode(email);
            string safePassword = WebUtility.HtmlEncode(password);
            string safeUrl = WebUtility.HtmlEncode(loginUrl);

            return $@"
<div style=""font-family:Arial,sans-serif;font-size:15px;color:#1d2620;line-height:1.6"">
  <p>Hello {name},</p>
  <p>Good news: <strong>{brand}</strong> has been approved to sell on MarketLink.</p>
  <p>You can now log in, open your stall and post your products.</p>
  <table style=""border-collapse:collapse;margin:16px 0"">
    <tr><td style=""padding:4px 16px 4px 0;color:#5f6b63"">Login page</td><td><a href=""{safeUrl}"">{safeUrl}</a></td></tr>
    <tr><td style=""padding:4px 16px 4px 0;color:#5f6b63"">Email</td><td>{safeEmail}</td></tr>
    <tr><td style=""padding:4px 16px 4px 0;color:#5f6b63"">Password</td><td style=""font-family:monospace;font-size:17px""><strong>{safePassword}</strong></td></tr>
  </table>
  <p>Please keep this password private and change it after your first login.</p>
  <p>The MarketLink team</p>
</div>";
        }

        private static string BuildRejectionEmail(string contactPerson, string brandName)
        {
            string name = WebUtility.HtmlEncode(contactPerson);
            string brand = WebUtility.HtmlEncode(brandName);

            return $@"
<div style=""font-family:Arial,sans-serif;font-size:15px;color:#1d2620;line-height:1.6"">
  <p>Hello {name},</p>
  <p>Thank you for applying to sell on MarketLink. We are sorry, but we cannot approve the application for <strong>{brand}</strong> at this time.</p>
  <p>If you have questions, please reply to this email.</p>
  <p>The MarketLink team</p>
</div>";
        }
    }
}
