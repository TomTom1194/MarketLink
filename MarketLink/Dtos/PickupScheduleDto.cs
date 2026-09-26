using MarketLink.Models;

namespace MarketLink.Dtos
{
    public class PickupDayDto
    {
        public DateTime Date { get; set; }

        public List<PickupMarketDto> Markets { get; set; } = new List<PickupMarketDto>();
    }

    public class PickupMarketDto
    {
        public Market Market { get; set; } = null!;

        public decimal Total { get; set; }

        public List<PickupSlotDto> Slots { get; set; } = new List<PickupSlotDto>();
    }

    public class PickupSlotDto
    {
        public TimeSpan PickupFrom { get; set; }

        public TimeSpan PickupTo { get; set; }

        public decimal Total { get; set; }

        public List<Order> Orders { get; set; } = new List<Order>();
    }
}
