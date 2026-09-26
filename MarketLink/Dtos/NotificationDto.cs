namespace MarketLink.Dtos
{
    public class NotificationDto
    {
        public int Id { get; set; }

        public int? OrderId { get; set; }

        public string? OrderCode { get; set; }

        public string Type { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public bool IsRead { get; set; }
    }
}
