using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Order line: a copy of the item details at the time of ordering
    [Table("Order_Snapshot")]
    public class OrderSnapshot
    {
        [Key]
        [Column("snapshot_id")]
        public int SnapshotId { get; set; }

        [Column("order_id")]
        public int OrderId { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("stock_price_id")]
        public int StockPriceId { get; set; }

        [Required]
        [StringLength(150)]
        [Column("product_name")]
        public string ProductName { get; set; } = "";

        [Required]
        [StringLength(100)]
        [Column("category_name")]
        public string CategoryName { get; set; } = "";

        [Required]
        [StringLength(20)]
        [Column("unit")]
        public string Unit { get; set; } = "";

        [Required]
        [StringLength(500)]
        [Column("image_url")]
        public string ImageUrl { get; set; } = "";

        [Column("unit_price", TypeName = "decimal(12,2)")]
        public decimal UnitPrice { get; set; }

        [Column("quantity", TypeName = "decimal(10,2)")]
        public decimal Quantity { get; set; }

        // Computed by the database = unit_price * quantity
        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("line_total", TypeName = "decimal(12,2)")]
        public decimal LineTotal { get; set; }

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [ForeignKey("StockPriceId")]
        public StockPrice? StockPrice { get; set; }
    }
}
