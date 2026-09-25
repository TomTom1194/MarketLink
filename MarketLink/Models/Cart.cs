using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // A customer's cart at one market
    [Table("Cart")]
    public class Cart
    {
        [Key]
        [Column("cart_id")]
        public int CartId { get; set; }

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("market_id")]
        public int MarketId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("CustomerId")]
        public CustomerProfile? Customer { get; set; }

        [ForeignKey("MarketId")]
        public Market? Market { get; set; }

        public List<CartItem> Items { get; set; } = new List<CartItem>();
    }
}
