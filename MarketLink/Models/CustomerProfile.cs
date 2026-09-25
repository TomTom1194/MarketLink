using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Customer profile, 1-1 with Users (CustomerId = UserId)
    [Table("Customer_Profile")]
    public class CustomerProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Required]
        [StringLength(100)]
        [Column("full_name")]
        public string FullName { get; set; } = "";

        [Required]
        [StringLength(255)]
        [Column("address")]
        public string Address { get; set; } = "";

        [Column("district_id")]
        public int DistrictId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("CustomerId")]
        public User? User { get; set; }

        [ForeignKey("DistrictId")]
        public District? District { get; set; }

        public List<Cart> Carts { get; set; } = new List<Cart>();

        public List<Order> Orders { get; set; } = new List<Order>();
    }
}
