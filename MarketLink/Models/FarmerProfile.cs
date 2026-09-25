using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Farmer profile, 1-1 with Users (FarmerId = UserId)
    [Table("Farmer_Profile")]
    public class FarmerProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Column("farmer_id")]
        public int FarmerId { get; set; }

        [Required]
        [StringLength(150)]
        [Column("brand_name")]
        public string BrandName { get; set; } = "";

        [Required]
        [StringLength(100)]
        [Column("contact_person")]
        public string ContactPerson { get; set; } = "";

        [Required]
        [StringLength(255)]
        [Column("address")]
        public string Address { get; set; } = "";

        [Column("district_id")]
        public int DistrictId { get; set; }

        [Column("description")]
        public string? Description { get; set; }

        // pending | approved | rejected | suspended
        [Required]
        [StringLength(20)]
        [Column("approval_status")]
        public string ApprovalStatus { get; set; } = "pending";

        // Admin who approved (UserId)
        [Column("approved_by")]
        public int? ApprovedBy { get; set; }

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // This table has 2 foreign keys to Users, so mark which one pairs with User.FarmerProfile (1-1)
        [ForeignKey("FarmerId")]
        [InverseProperty("FarmerProfile")]
        public User? User { get; set; }

        [ForeignKey("ApprovedBy")]
        public User? ApprovedByUser { get; set; }

        [ForeignKey("DistrictId")]
        public District? District { get; set; }

        public List<Stall> Stalls { get; set; } = new List<Stall>();

        public List<Product> Products { get; set; } = new List<Product>();
    }
}
