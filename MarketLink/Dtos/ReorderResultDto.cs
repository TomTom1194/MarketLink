namespace MarketLink.Dtos
{
    public class ReorderResultDto
    {
        public string Error { get; set; } = "";

        public string OrderCode { get; set; } = "";

        public int MarketId { get; set; }

        public int AddedCount { get; set; }

        public List<string> Notices { get; set; } = new List<string>();
    }
}
