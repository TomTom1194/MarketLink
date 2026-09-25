using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Login account for every role
    [Table("Users")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("role_id")]
        public int RoleId { get; set; }

        [Required]
        [StringLength(255)]
        [Column("email")]
        public string Email { get; set; } = "";

        [Required]
        [StringLength(255)]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = "";

        // Digits only, e.g. 0901234567
        [Required]
        [StringLength(15)]
        [Column("phone")]
        public string Phone { get; set; } = "";

        // active | disabled
        [Required]
        [StringLength(20)]
        [Column("status")]
        public string Status { get; set; } = "active";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey("RoleId")]
        public Role? Role { get; set; }

        public CustomerProfile? CustomerProfile { get; set; }

        public FarmerProfile? FarmerProfile { get; set; }

        public List<Notification> Notifications { get; set; } = new List<Notification>();
    }
}
