using MarketLink.Data;
using MarketLink.Helpers;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class DisputeService : IDisputeService
    {
        public const int MaxWarnings = 3;
        public const int ReplyHours = 48;
        public const int CustomerReportHours = AutoCancelService.WaitHours;

        private readonly MarketLinkDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<DisputeService> _logger;

        public DisputeService(MarketLinkDbContext context, IEmailService emailService, ILogger<DisputeService> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<OrderDispute?> GetByOrderAsync(int orderId)
        {
            return await _context.OrderDisputes.FirstOrDefaultAsync(d => d.OrderId == orderId);
        }

        public bool CustomerCanReport(Order order)
        {
            if (order.Status != "accepted")
            {
                return false;
            }
            DateTime pickupStart = order.PickupDate.Date.Add(order.PickupFrom);
            DateTime lastTime = order.PickupDate.Date.Add(order.PickupTo).AddHours(CustomerReportHours);
            return DateTime.Now >= pickupStart && DateTime.Now <= lastTime;
        }

        public bool FarmerCanReport(Order order)
        {
            if (order.Status != "accepted")
            {
                return false;
            }
            DateTime pickupEnd = order.PickupDate.Date.Add(order.PickupTo);
            return DateTime.Now >= pickupEnd && DateTime.Now <= pickupEnd.AddHours(AutoCancelService.WaitHours);
        }

        public async Task<string> SendCustomerReasonAsync(int customerId, int orderId, string? reason)
        {
            string error = CheckReason(reason);
            if (error != "")
            {
                return error;
            }

            var order = await _context.Orders
                .Include(o => o.Stall)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.CustomerId == customerId);
            if (order == null)
            {
                return "Order not found.";
            }

            var dispute = await GetByOrderAsync(orderId);

            if (dispute == null)
            {
                if (!CustomerCanReport(order))
                {
                    return "You can report a problem only from the start of your pickup time until " + CustomerReportHours + " hours after it ends, and only for an accepted order.";
                }

                dispute = new OrderDispute
                {
                    OrderId = order.OrderId,
                    ReportedBy = "customer",
                    CustomerReason = reason!.Trim(),
                    CustomerReasonAt = DateTime.Now
                };
                _context.OrderDisputes.Add(dispute);
                order.Status = "disputed";

                AddNotification(order.Stall!.FarmerId, order.OrderId, "order_disputed",
                    "Order #" + order.OrderCode + " was reported by the customer",
                    "The customer reported a problem with this pickup. Please open the order and send your side within " + ReplyHours + " hours. MarketLink will review both sides.");
            }
            else
            {
                if (dispute.Status != "open")
                {
                    return "This case has already been closed.";
                }
                if (dispute.CustomerReason != null)
                {
                    return "You have already sent your side for this order.";
                }

                dispute.CustomerReason = reason!.Trim();
                dispute.CustomerReasonAt = DateTime.Now;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return "This order was just reported. Please reload the page and send your side.";
            }
            return "";
        }

        public async Task<string> SendFarmerReasonAsync(int farmerId, int orderId, string? reason)
        {
            string error = CheckReason(reason);
            if (error != "")
            {
                return error;
            }

            var order = await _context.Orders
                .Include(o => o.Stall)
                .FirstOrDefaultAsync(o => o.OrderId == orderId && o.Stall != null && o.Stall.FarmerId == farmerId);
            if (order == null)
            {
                return "Order not found.";
            }

            var dispute = await GetByOrderAsync(orderId);

            if (dispute == null)
            {
                if (!FarmerCanReport(order))
                {
                    return "You can report a no-show only for an accepted order, from the end of the pickup time until " + AutoCancelService.WaitHours + " hours after it.";
                }

                dispute = new OrderDispute
                {
                    OrderId = order.OrderId,
                    ReportedBy = "farmer",
                    FarmerReason = reason!.Trim(),
                    FarmerReasonAt = DateTime.Now
                };
                _context.OrderDisputes.Add(dispute);
                order.Status = "disputed";

                AddNotification(order.CustomerId, order.OrderId, "order_disputed",
                    "Order #" + order.OrderCode + " was reported as not picked up",
                    "The farmer reported that you did not come to pick up this order. Please open the order and tell us what happened within " + ReplyHours + " hours. MarketLink will review both sides.");
            }
            else
            {
                if (dispute.Status != "open")
                {
                    return "This case has already been closed.";
                }
                if (dispute.FarmerReason != null)
                {
                    return "You have already sent your side for this order.";
                }

                dispute.FarmerReason = reason!.Trim();
                dispute.FarmerReasonAt = DateTime.Now;
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return "This order was just reported. Please reload the page and send your side.";
            }
            return "";
        }

        public async Task<List<OrderDispute>> GetDisputesAsync(string status)
        {
            return await _context.OrderDisputes
                .Include(d => d.Order)
                .ThenInclude(o => o!.Customer)
                .Include(d => d.Order)
                .ThenInclude(o => o!.Stall)
                .ThenInclude(s => s!.Farmer)
                .Where(d => d.Status == status)
                .OrderBy(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<OrderDispute?> GetDisputeDetailAsync(int disputeId)
        {
            return await _context.OrderDisputes
                .Include(d => d.Order)
                .ThenInclude(o => o!.Items)
                .Include(d => d.Order)
                .ThenInclude(o => o!.Customer)
                .ThenInclude(c => c!.User)
                .Include(d => d.Order)
                .ThenInclude(o => o!.Stall)
                .ThenInclude(s => s!.Farmer)
                .ThenInclude(f => f!.User)
                .Include(d => d.Order)
                .ThenInclude(o => o!.Stall)
                .ThenInclude(s => s!.Market)
                .FirstOrDefaultAsync(d => d.DisputeId == disputeId);
        }

        public bool CanResolve(OrderDispute dispute)
        {
            if (dispute.Status != "open")
            {
                return false;
            }
            if (dispute.CustomerReason != null && dispute.FarmerReason != null)
            {
                return true;
            }
            return DateTime.Now >= dispute.CreatedAt.AddHours(ReplyHours);
        }

        public async Task<int> CountWarningsAsync(int userId)
        {
            return await _context.UserWarnings.CountAsync(w => w.UserId == userId);
        }

        public async Task<string> ResolveAsync(int disputeId, int adminId, string? atFault, string? adminNote)
        {
            if (atFault != "customer" && atFault != "farmer" && atFault != "none")
            {
                return "Please choose who is at fault.";
            }
            if (adminNote != null && adminNote.Trim().Length > 500)
            {
                return "The note must be 500 characters or less.";
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            var dispute = await _context.OrderDisputes
                .Include(d => d.Order)
                .ThenInclude(o => o!.Items)
                .Include(d => d.Order)
                .ThenInclude(o => o!.Stall)
                .FirstOrDefaultAsync(d => d.DisputeId == disputeId);
            if (dispute == null || dispute.Order == null)
            {
                return "Case not found.";
            }
            if (!CanResolve(dispute))
            {
                return "This case cannot be closed yet. Wait for both sides or until " + ReplyHours + " hours have passed.";
            }

            var order = dispute.Order;
            int customerUserId = order.CustomerId;
            int farmerUserId = order.Stall!.FarmerId;

            dispute.Status = "resolved";
            dispute.AtFault = atFault;
            dispute.AdminNote = string.IsNullOrWhiteSpace(adminNote) ? null : adminNote.Trim();
            dispute.ResolvedBy = adminId;
            dispute.ResolvedAt = DateTime.Now;

            order.Status = "cancelled";
            order.CancelReason = "Closed by MarketLink after a pickup report.";
            order.CancelledAt = DateTime.Now;

            foreach (var item in order.Items)
            {
                var stock = await _context.StockPrices.FirstOrDefaultAsync(sp => sp.StockPriceId == item.StockPriceId);
                if (stock != null)
                {
                    stock.QuantityReserved = Math.Max(0, stock.QuantityReserved - item.Quantity);
                }
            }

            User? lockedUser = null;
            string lockedName = "";
            string customerResult = "We found no fault on your side.";
            string farmerResult = "We found no fault on your side.";

            if (atFault == "customer")
            {
                int warnings = await GiveWarningAsync(customerUserId, dispute);
                customerResult = "You received a warning (" + warnings + "/" + MaxWarnings + ").";
                if (warnings >= MaxWarnings)
                {
                    var user = await _context.Users.FirstAsync(u => u.UserId == customerUserId);
                    user.Status = "disabled";
                    user.UpdatedAt = DateTime.Now;
                    customerResult = customerResult + " Your account has been locked.";
                    var profile = await _context.CustomerProfiles.FirstOrDefaultAsync(c => c.CustomerId == customerUserId);
                    lockedUser = user;
                    lockedName = profile != null ? profile.FullName : user.Email;
                }
            }
            else if (atFault == "farmer")
            {
                int warnings = await GiveWarningAsync(farmerUserId, dispute);
                farmerResult = "You received a warning (" + warnings + "/" + MaxWarnings + ").";
                if (warnings >= MaxWarnings)
                {
                    var user = await _context.Users.FirstAsync(u => u.UserId == farmerUserId);
                    user.Status = "disabled";
                    user.UpdatedAt = DateTime.Now;
                    var farmer = await _context.FarmerProfiles.FirstOrDefaultAsync(f => f.FarmerId == farmerUserId);
                    if (farmer != null)
                    {
                        farmer.ApprovalStatus = "suspended";
                    }
                    farmerResult = farmerResult + " Your account has been locked.";
                    lockedUser = user;
                    lockedName = farmer != null ? farmer.ContactPerson : user.Email;
                }
            }

            string note = dispute.AdminNote != null ? " Note from MarketLink: " + dispute.AdminNote : "";
            AddNotification(customerUserId, order.OrderId, "dispute_resolved",
                "Report on order #" + order.OrderCode + " is closed",
                "The order was cancelled. " + customerResult + note);
            AddNotification(farmerUserId, order.OrderId, "dispute_resolved",
                "Report on order #" + order.OrderCode + " is closed",
                "The order was cancelled and the reserved quantity is back on sale. " + farmerResult + note);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (lockedUser != null)
            {
                try
                {
                    string reason = "You received " + MaxWarnings + " warnings after pickup reports (last one on order #" + order.OrderCode + ").";
                    await _emailService.SendAsync(lockedUser.Email, lockedName, AccountLockHelper.Subject, AccountLockHelper.BuildEmail(lockedName, reason));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not send the account locked email to {Email}", lockedUser.Email);
                }
            }
            return "";
        }

        private async Task<int> GiveWarningAsync(int userId, OrderDispute dispute)
        {
            int oldCount = await CountWarningsAsync(userId);
            _context.UserWarnings.Add(new UserWarning
            {
                UserId = userId,
                Dispute = dispute
            });
            return oldCount + 1;
        }

        private string CheckReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return "Please enter a reason.";
            }
            if (reason.Trim().Length > 500)
            {
                return "The reason must be 500 characters or less.";
            }
            return "";
        }

        private void AddNotification(int userId, int orderId, string type, string title, string body)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                OrderId = orderId,
                Type = type,
                Title = title,
                Body = body,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
        }
    }
}
