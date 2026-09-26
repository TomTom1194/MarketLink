namespace MarketLink.Dtos
{
    public class QuickReserveDto
    {
        public int StockPriceId { get; set; }

        public decimal Quantity { get; set; }

        public DateTime? PickupDate { get; set; }

        public string? PickupSlot { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
