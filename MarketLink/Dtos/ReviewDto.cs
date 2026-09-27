namespace MarketLink.Dtos
{
    public class ReviewDto
    {
        public string CustomerName { get; set; } = "";

        public int Rating { get; set; }

        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<ReviewProductDto> Products { get; set; } = new List<ReviewProductDto>();
    }

    public class ReviewProductDto
    {
        public string ProductName { get; set; } = "";

        public string ImageUrl { get; set; } = "";
    }
}
