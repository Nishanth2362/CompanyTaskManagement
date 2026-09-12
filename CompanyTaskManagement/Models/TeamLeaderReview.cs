using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompanyTaskManagement.Models
{
    public class TeamLeaderReview
    {
        [Key]
        public int Id { get; set; }

        public int? TeamLeaderId { get; set; }

        [ForeignKey("TeamLeaderId")]
        public virtual Employee? TeamLeader { get; set; }

        [StringLength(100)]
        public string LeaderName { get; set; } = string.Empty;

        public int? EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public virtual Employee? Employee { get; set; }

        [StringLength(100)]
        public string EmployeeName { get; set; } = string.Empty;

        [StringLength(100)]
        public string ReviewerName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ReviewerRole { get; set; } = "Team Leader";

        [StringLength(100)]
        public string Category { get; set; } = "🌟 Outstanding Performance";

        [Range(1, 5)]
        public int Rating { get; set; } = 5;

        [StringLength(2000)]
        public string Comments { get; set; } = string.Empty;

        public string Feedback { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
