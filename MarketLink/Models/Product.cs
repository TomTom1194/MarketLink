using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Fixed product details. Price and quantity live in Stock_Price
    [Table("Products")]
    public class Product
    {
        [Key]
        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("farmer_id")]
        public int FarmerId { get; set; }

        [Column("category_id")]
        public int CategoryId { get; set; }

        [Column("exp_id")]
        public int ExpId { get; set; }

        [Required]
        [StringLength(150)]
        [Column("product_name")]
        public string ProductName { get; set; } = "";

        [Column("description")]
        public string? Description { get; set; }

        // Unit: kg, bunch, box...
        [Required]
        [StringLength(20)]
        [Column("unit")]
        public string Unit { get; set; } = "";

        [Required]
        [StringLength(500)]
        [Column("image_url")]
        public string ImageUrl { get; set; } = "";

        // Last time the product was posted / reposted
        [Column("published_at")]
        public DateTime PublishedAt { get; set; } = DateTime.Now;

        // = PublishedAt + Product_Exp days; hidden once expired
        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        // active | hidden | removed
        [Required]
        [StringLength(20)]
        [Column("status")]
        public string Status { get; set; } = "active";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }

        [ForeignKey("CategoryId")]
        public ProductCategory? Category { get; set; }

        [ForeignKey("ExpId")]
        public ProductExp? Exp { get; set; }

        public List<StockPrice> StockPrices { get; set; } = new List<StockPrice>();
    }
}
