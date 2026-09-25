using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // A farmer's stall in a market, where customers pick up orders
    [Table("Stalls")]
    public class Stall
    {
        [Key]
        [Column("stall_id")]
        public int StallId { get; set; }

        [Column("market_id")]
        public int MarketId { get; set; }

        [Column("farmer_id")]
        public int FarmerId { get; set; }

        // e.g. B-12
        [Required]
        [StringLength(20)]
        [Column("stall_code")]
        public string StallCode { get; set; } = "";

        // e.g. Row B, next to gate 2
        [StringLength(255)]
        [Column("location_note")]
        public string? LocationNote { get; set; }

        [Column("latitude", TypeName = "decimal(9,6)")]
        public decimal? Latitude { get; set; }

        [Column("longitude", TypeName = "decimal(9,6)")]
        public decimal? Longitude { get; set; }

        // Days the stall sells, e.g. "4,7"
        [Required]
        [StringLength(20)]
        [Column("selling_days")]
        public string SellingDays { get; set; } = "";

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [ForeignKey("MarketId")]
        public Market? Market { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }

        public List<StockPrice> StockPrices { get; set; } = new List<StockPrice>();

        public List<Order> Orders { get; set; } = new List<Order>();
    }
}
