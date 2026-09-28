using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // A ready-made product (name + category + unit) a farmer can pick when adding a product
    [Table("Product_Template")]
    public class ProductTemplate
    {
        [Key]
        [Column("template_id")]
        public int TemplateId { get; set; }

        [Column("category_id")]
        public int CategoryId { get; set; }

        [Column("product_name")]
        [StringLength(150)]
        public string ProductName { get; set; } = "";

        [Column("unit")]
        [StringLength(20)]
        public string Unit { get; set; } = "";

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [ForeignKey("CategoryId")]
        public ProductCategory? Category { get; set; }
    }
}
