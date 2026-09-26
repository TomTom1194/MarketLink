using System.ComponentModel.DataAnnotations;

namespace MarketLink.Dtos.Admin
{
    // Create / edit product category form
    public class CategoryFormDto
    {
        // null when creating a new category
        public int? CategoryId { get; set; }

        [Display(Name = "Parent category")]
        public int? ParentId { get; set; }

        [Required(ErrorMessage = "Please enter the category name")]
        [StringLength(100)]
        [Display(Name = "Category name")]
        public string CategoryName { get; set; } = "";

        // Leave empty to build it from the name, e.g. "Leafy greens" -> "leafy-greens"
        [StringLength(100)]
        [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "Use lowercase letters, numbers and dashes only")]
        [Display(Name = "Slug")]
        public string? Slug { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;
    }
}
