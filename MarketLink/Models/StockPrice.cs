using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Each row is one price + quantity listing. EffectiveTo = null means it is currently on sale
    [Table("Stock_Price")]
    public class StockPrice
    {
        [Key]
        [Column("stock_price_id")]
        public int StockPriceId { get; set; }

        [Column("product_id")]
        public int ProductId { get; set; }

        [Column("stall_id")]
        public int StallId { get; set; }

        // Price per unit (Product.Unit)
        [Column("price", TypeName = "decimal(12,2)")]
        public decimal Price { get; set; }

        // Quantity listed
        [Column("quantity_in", TypeName = "decimal(10,2)")]
        public decimal QuantityIn { get; set; }

        // Held for orders not picked up yet
        [Column("quantity_reserved", TypeName = "decimal(10,2)")]
        public decimal QuantityReserved { get; set; }

        // Handed over to customers
        [Column("quantity_sold", TypeName = "decimal(10,2)")]
        public decimal QuantitySold { get; set; }

        // new | price_change | restock
        [Required]
        [StringLength(20)]
        [Column("change_type")]
        public string ChangeType { get; set; } = "new";

        [Column("effective_from")]
        public DateTime EffectiveFrom { get; set; } = DateTime.Now;

        // null = currently on sale
        [Column("effective_to")]
        public DateTime? EffectiveTo { get; set; }

        // Farmer who created this row (UserId)
        [Column("created_by")]
        public int CreatedBy { get; set; }

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        [ForeignKey("StallId")]
        public Stall? Stall { get; set; }

        [ForeignKey("CreatedBy")]
        public User? CreatedByUser { get; set; }

        // Quantity still available (not a database column)
        [NotMapped]
        public decimal QuantityAvailable => QuantityIn - QuantityReserved - QuantitySold;
    }
}
