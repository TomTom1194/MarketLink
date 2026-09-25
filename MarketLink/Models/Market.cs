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

        [Required]
        [StringLength(255)]
        [Column("address")]
        public string Address { get; set; } = "";

        [Column("latitude", TypeName = "decimal(9,6)")]
        public decimal? Latitude { get; set; }

        [Column("longitude", TypeName = "decimal(9,6)")]
        public decimal? Longitude { get; set; }

        [StringLength(500)]
        [Column("map_url")]
        public string? MapUrl { get; set; }

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

        [ForeignKey("DistrictId")]
        public District? District { get; set; }

        public List<Stall> Stalls { get; set; } = new List<Stall>();
    }
}
