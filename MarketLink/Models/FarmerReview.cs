using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    [Table("Farmer_Reviews")]
    public class FarmerReview
    {
        [Key]
        [Column("review_id")]
        public int ReviewId { get; set; }

        [Column("order_id")]
        public int OrderId { get; set; }

        [Column("farmer_id")]
        public int FarmerId { get; set; }

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("rating")]
        public int Rating { get; set; }

        [StringLength(500)]
        [Column("comment")]
        public string? Comment { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }

        [ForeignKey("CustomerId")]
        public CustomerProfile? Customer { get; set; }
    }
}
