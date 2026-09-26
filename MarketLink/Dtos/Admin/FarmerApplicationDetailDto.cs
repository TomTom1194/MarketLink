namespace MarketLink.Dtos.Admin
{
    // Everything the admin needs to review one farmer application
    public class FarmerApplicationDetailDto
    {
        public int FarmerId { get; set; }

        // ----- Applicant -----
        public string BrandName { get; set; } = "";
        public string ContactPerson { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Address { get; set; } = "";
        public string DistrictName { get; set; } = "";
        public string CityName { get; set; } = "";
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }

        // ----- Review -----
        // pending | approved | rejected | suspended
        public string ApprovalStatus { get; set; } = "";
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovedByEmail { get; set; }

        // ----- Selling -----
        public int ProductCount { get; set; }
        public List<FarmerStallDto> Stalls { get; set; } = new List<FarmerStallDto>();
    }
}
