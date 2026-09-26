using MarketLink.Models;

namespace MarketLink.Dtos
{
    public class MarketSearchResultDto
    {
        public Market Market { get; set; } = null!;

        public double? DistanceKm { get; set; }

        public List<StockPrice> Products { get; set; } = new List<StockPrice>();
    }
}
