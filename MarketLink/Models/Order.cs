using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    // Pre-order; each order belongs to one stall
    [Table("Orders")]
    public class Order
    {
        [Key]
        [Column("order_id")]
        public int OrderId { get; set; }

        // e.g. ML-260925-0042
        [Required]
        [StringLength(20)]
        [Column("order_code")]
        public string OrderCode { get; set; } = "";

        [Column("customer_id")]
        public int CustomerId { get; set; }

        [Column("stall_id")]
        public int StallId { get; set; }

        [Column("pickup_date", TypeName = "date")]
        public DateTime PickupDate { get; set; }

        [Column("pickup_from")]
        public TimeSpan PickupFrom { get; set; }

        [Column("pickup_to")]
        public TimeSpan PickupTo { get; set; }

        [Required]
        [StringLength(100)]
        [Column("pickup_name")]
        public string PickupName { get; set; } = "";

        // Receiver's phone, used to look up the order at the stall
        [Required]
        [StringLength(15)]
        [Column("pickup_phone")]
        public string PickupPhone { get; set; } = "";

        // placed | accepted | rejected | cancelled | completed | no_show
        [Required]
        [StringLength(20)]
        [Column("status")]
        public string Status { get; set; } = "placed";

        [Column("total_amount", TypeName = "decimal(12,2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(500)]
        [Column("customer_note")]
        public string? CustomerNote { get; set; }

        // Required when the farmer rejects the order
        [StringLength(500)]
        [Column("reject_reason")]
        public string? RejectReason { get; set; }

        // Required when the customer cancels the order
        [StringLength(500)]
        [Column("cancel_reason")]
        public string? CancelReason { get; set; }

        [Column("placed_at")]
        public DateTime PlacedAt { get; set; } = DateTime.Now;

        [Column("accepted_at")]
        public DateTime? AcceptedAt { get; set; }

        [Column("rejected_at")]
        public DateTime? RejectedAt { get; set; }

        [Column("cancelled_at")]
        public DateTime? CancelledAt { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        // Farmer who confirmed completion (UserId)
        [Column("completed_by")]
        public int? CompletedBy { get; set; }

        [ForeignKey("CustomerId")]
        public CustomerProfile? Customer { get; set; }

        [ForeignKey("StallId")]
        public Stall? Stall { get; set; }

        [ForeignKey("CompletedBy")]
        public User? CompletedByUser { get; set; }

        public List<OrderSnapshot> Items { get; set; } = new List<OrderSnapshot>();
    }
}
