using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace MarketLink.Models
{
    // Link table: which product categories a farmer is allowed to sell
    [Table("Farmer_Category")]
    [PrimaryKey(nameof(FarmerId), nameof(CategoryId))]
    public class FarmerCategory
    {
        [Column("farmer_id")]
        public int FarmerId { get; set; }

        [Column("category_id")]
        public int CategoryId { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }

        [ForeignKey("CategoryId")]
        public ProductCategory? Category { get; set; }
    }
}
