using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // In-app notification for customers and farmers
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        [Column("notification_id")]
        public int NotificationId { get; set; }

        // Recipient
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("order_id")]
        public int? OrderId { get; set; }

        // new_order | order_accepted | order_rejected | order_cancelled | order_completed
        [Required]
        [StringLength(30)]
        [Column("type")]
        public string Type { get; set; } = "";

        [Required]
        [StringLength(150)]
        [Column("title")]
        public string Title { get; set; } = "";

        [StringLength(500)]
        [Column("body")]
        public string? Body { get; set; }

        [Column("is_read")]
        public bool IsRead { get; set; } = false;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("read_at")]
        public DateTime? ReadAt { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }
    }
}
