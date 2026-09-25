using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // How long a product stays visible: Short term / Long term
    [Table("Product_Exp")]
    public class ProductExp
    {
        [Key]
        [Column("exp_id")]
        public int ExpId { get; set; }

        // SHORT | LONG
        [Required]
        [StringLength(10)]
        [Column("exp_code")]
        public string ExpCode { get; set; } = "";

        [Required]
        [StringLength(50)]
        [Column("exp_name")]
        public string ExpName { get; set; } = "";

        // Days a product stays visible, e.g. 3 or 30
        [Column("duration_days")]
        public int DurationDays { get; set; }

        [StringLength(255)]
        [Column("description")]
        public string? Description { get; set; }

        public List<Product> Products { get; set; } = new List<Product>();
    }
}
