namespace MarketLink.Dtos.Admin
{
    // One row in the category list
    public class CategoryRowDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public string Slug { get; set; } = "";
        public int? ParentId { get; set; }
        public string? ParentName { get; set; }
        public bool IsActive { get; set; }
        public int ProductCount { get; set; }
    }
}
