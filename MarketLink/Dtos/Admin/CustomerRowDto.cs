namespace MarketLink.Dtos.Admin
{
    // One row in the customer list
    public class CustomerRowDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string DistrictName { get; set; } = "";
        public string CityName { get; set; } = "";

        // active | disabled
        public string Status { get; set; } = "";

        public DateTime CreatedAt { get; set; }
        public int OrderCount { get; set; }
    }
}
