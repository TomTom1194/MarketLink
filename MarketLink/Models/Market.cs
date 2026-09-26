using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Farmers market
    [Table("Markets")]
    public class Market
    {
        [Key]
        [Column("market_id")]
        public int MarketId { get; set; }

        [Column("district_id")]
        public int DistrictId { get; set; }

        [Required]
        [StringLength(150)]
        [Column("market_name")]
        public string MarketName { get; set; } = "";

        // Google Maps link to the market
        [Required]
        [StringLength(500)]
        [Column("map_url")]
        public string MapUrl { get; set; } = "";

        // Market photo, e.g. /uploads/markets/abc.jpg
        [StringLength(500)]
        [Column("image_url")]
        public string? ImageUrl { get; set; }

        // Market days, e.g. "2,4,7" (2 = Monday)
        [Required]
        [StringLength(20)]
        [Column("open_days")]
        public string OpenDays { get; set; } = "";

        [Column("open_time")]
        public TimeSpan OpenTime { get; set; }

        [Column("close_time")]
        public TimeSpan CloseTime { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        // Farmer who suggested this market in their application (null = created by admin).
        // A suggested market stays hidden until the admin approves that farmer.
        [Column("requested_by")]
        public int? RequestedBy { get; set; }

        [ForeignKey("DistrictId")]
        public District? District { get; set; }

        [ForeignKey("RequestedBy")]
        public User? RequestedByUser { get; set; }

        public List<Stall> Stalls { get; set; } = new List<Stall>();
    }
}
