using MarketLink.Models;

namespace MarketLink.Dtos
{
    public class MarketDistanceDto
    {
        public Market Market { get; set; } = null!;

        public double? DistanceKm { get; set; }
    }
}
