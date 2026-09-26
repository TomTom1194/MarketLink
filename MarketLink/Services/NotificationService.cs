using MarketLink.Data;
using MarketLink.Dtos;
using MarketLink.Models;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Services
{
    public class NotificationService : INotificationService
    {
        private readonly MarketLinkDbContext _context;

        public NotificationService(MarketLinkDbContext context)
        {
            _context = context;
        }

        public async Task<List<NotificationDto>> GetForUserAsync(int userId)
        {
            return await _context.Notifications
                .AsNoTracking()
                .Where(notification => notification.UserId == userId)
                .OrderByDescending(notification => notification.CreatedAt)
                .Select(notification => new NotificationDto
                {
                    Id = notification.NotificationId,
                    OrderId = notification.OrderId,
                    OrderCode = notification.Order != null ? notification.Order.OrderCode : null,
                    Type = notification.Type,
                    Title = notification.Title,
                    Message = notification.Body ?? string.Empty,
                    CreatedAt = notification.CreatedAt,
                    IsRead = notification.IsRead
                })
                .ToListAsync();
        }

        public async Task<bool> MarkAllReadAsync(int userId)
        {
            var unread = await _context.Notifications
                .Where(notification => notification.UserId == userId && !notification.IsRead)
                .ToListAsync();

            if (unread.Count == 0) return false;

            var readAt = DateTime.Now;
            foreach (var notification in unread)
            {
                notification.IsRead = true;
                notification.ReadAt = readAt;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task CreateAsync(int userId, int? orderId, string type, string title, string message)
        {
            _context.Notifications.Add(new Notification
            {
                UserId = userId,
                OrderId = orderId,
                Type = type,
                Title = title,
                Body = message,
                IsRead = false,
                CreatedAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }
    }
}
