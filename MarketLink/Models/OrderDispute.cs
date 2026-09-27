using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MarketLink.Models
{
    [Table("Order_Disputes")]
    public class OrderDispute
    {
        [Key]
        [Column("dispute_id")]
        public int DisputeId { get; set; }

        [Column("order_id")]
        public int OrderId { get; set; }

        [Required]
        [StringLength(20)]
        [Column("reported_by")]
        public string ReportedBy { get; set; } = "";

        [StringLength(500)]
        [Column("customer_reason")]
        public string? CustomerReason { get; set; }

        [Column("customer_reason_at")]
        public DateTime? CustomerReasonAt { get; set; }

        [StringLength(500)]
        [Column("farmer_reason")]
        public string? FarmerReason { get; set; }

        [Column("farmer_reason_at")]
        public DateTime? FarmerReasonAt { get; set; }

        [Required]
        [StringLength(20)]
        [Column("status")]
        public string Status { get; set; } = "open";

        [StringLength(20)]
        [Column("at_fault")]
        public string? AtFault { get; set; }

        [StringLength(500)]
        [Column("admin_note")]
        public string? AdminNote { get; set; }

        [Column("resolved_by")]
        public int? ResolvedBy { get; set; }

        [Column("resolved_at")]
        public DateTime? ResolvedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("OrderId")]
        public Order? Order { get; set; }

        [ForeignKey("ResolvedBy")]
        public User? ResolvedByUser { get; set; }
    }
}
