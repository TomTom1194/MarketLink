using MarketLink.Dtos;

namespace MarketLink.Services
{
    public interface INotificationService
    {
        Task<List<NotificationDto>> GetForUserAsync(int userId);

        Task<bool> MarkAllReadAsync(int userId);

        Task CreateAsync(int userId, int? orderId, string type, string title, string message);
    }
}
