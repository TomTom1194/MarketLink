using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    [Table("User_Warnings")]
    public class UserWarning
    {
        [Key]
        [Column("warning_id")]
        public int WarningId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("dispute_id")]
        public int DisputeId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("DisputeId")]
        public OrderDispute? Dispute { get; set; }
    }
}
