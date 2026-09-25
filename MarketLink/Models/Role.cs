using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Role: customer, farmer, admin
    [Table("Role")]
    public class Role
    {
        [Key]
        [Column("role_id")]
        public int RoleId { get; set; }

        // customer | farmer | admin
        [Required]
        [StringLength(20)]
        [Column("role_name")]
        public string RoleName { get; set; } = "";

        public List<User> Users { get; set; } = new List<User>();
    }
}
