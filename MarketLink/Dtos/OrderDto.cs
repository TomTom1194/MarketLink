namespace MarketLink.Dtos
{
    public class OrderDto
    {
        public int Id { get; set; }

        public string OrderCode { get; set; } = string.Empty;

        public string CustomerName { get; set; } = string.Empty;

        public string CustomerPhone { get; set; } = string.Empty;

        public DateTime ReceiveDate { get; set; }

        public decimal TotalAmount { get; set; }

        public string Status { get; set; } = string.Empty;

        public List<OrderItemDto> Items { get; set; } = new();
    }
}
