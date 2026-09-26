using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    [Table("Favorite_Farmers")]
    public class FavoriteFarmer
    {
        [Key]
        [Column("favorite_id")]
        public int FavoriteId { get; set; }

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("farmer_id")]
        public int FarmerId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("CustomerId")]
        public CustomerProfile? Customer { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }
    }
}
