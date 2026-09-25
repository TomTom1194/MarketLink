using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // District
    [Table("Districts")]
    public class District
    {
        [Key]
        [Column("district_id")]
        public int DistrictId { get; set; }

        [Column("city_id")]
        public int CityId { get; set; }

        [Required]
        [StringLength(100)]
        [Column("district_name")]
        public string DistrictName { get; set; } = "";

        // e.g. HCM-Q7
        [Required]
        [StringLength(20)]
        [Column("district_code")]
        public string DistrictCode { get; set; } = "";

        [ForeignKey("CityId")]
        public City? City { get; set; }

        public List<Market> Markets { get; set; } = new List<Market>();
    }
}
