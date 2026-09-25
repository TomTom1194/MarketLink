using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Item in a cart; stock is not held yet
    [Table("Cart_Items")]
    public class CartItem
    {
        [Key]
        [Column("cart_item_id")]
        public int CartItemId { get; set; }

        [Column("cart_id")]
        public int CartId { get; set; }

        [Column("stock_price_id")]
        public int StockPriceId { get; set; }

        [Column("quantity", TypeName = "decimal(10,2)")]
        public decimal Quantity { get; set; }

        [Column("added_at")]
        public DateTime AddedAt { get; set; } = DateTime.Now;

        [ForeignKey("CartId")]
        public Cart? Cart { get; set; }

        [ForeignKey("StockPriceId")]
        public StockPrice? StockPrice { get; set; }
    }
}
