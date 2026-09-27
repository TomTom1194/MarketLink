namespace MarketLink.Dtos
{
    public class RatingSummaryDto
    {
        public double Average { get; set; }

        public int Count { get; set; }

        public int[] StarCounts { get; set; } = new int[6];

        public int Percent(int star)
        {
            if (Count == 0)
            {
                return 0;
            }
            return StarCounts[star] * 100 / Count;
        }
    }
}
