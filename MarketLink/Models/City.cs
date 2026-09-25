using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // City
    [Table("Cities")]
    public class City
    {
        [Key]
        [Column("city_id")]
        public int CityId { get; set; }

        [Required]
        [StringLength(100)]
        [Column("city_name")]
        public string CityName { get; set; } = "";

        // HCM, HN
        [Required]
        [StringLength(10)]
        [Column("city_code")]
        public string CityCode { get; set; } = "";

        public List<District> Districts { get; set; } = new List<District>();
    }
}
