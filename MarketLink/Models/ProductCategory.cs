using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Product category, may have a parent category
    [Table("Product_Category")]
    public class ProductCategory
    {
        [Key]
        [Column("category_id")]
        public int CategoryId { get; set; }

        // null = top-level category
        [Column("parent_id")]
        public int? ParentId { get; set; }

        [Required]
        [StringLength(100)]
        [Column("category_name")]
        public string CategoryName { get; set; } = "";

        // e.g. vegetables
        [Required]
        [StringLength(100)]
        [Column("slug")]
        public string Slug { get; set; } = "";

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [ForeignKey("ParentId")]
        public ProductCategory? Parent { get; set; }

        public List<Product> Products { get; set; } = new List<Product>();
    }
}
