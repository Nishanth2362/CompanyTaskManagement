using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompanyTaskManagement.Models
{
    [Table("TaskActivityLogs")]
    public class TaskActivityLog
    {
        [Key]
        public int Id { get; set; }

        public int TaskId { get; set; }

        [Required]
        [StringLength(200)]
        public string TaskName { get; set; } = string.Empty;

        public int? EmployeeId { get; set; }

        [StringLength(150)]
        public string EmployeeName { get; set; } = "System / Unassigned";

        [Required]
        [StringLength(100)]
        public string ActionType { get; set; } = "Update"; // Created, Status Change, Progress Updated, Delay Logged, Screenshot Uploaded

        [StringLength(50)]
        public string? OldStatus { get; set; }

        [StringLength(50)]
        public string? NewStatus { get; set; }

        public int Progress { get; set; }

        public string? DelayReason { get; set; }

        public string? ErrorDetails { get; set; }

        public string? ErrorScreenshotPath { get; set; }

        public DateTime LoggedAt { get; set; } = DateTime.Now;

        // Navigation Property
        [ForeignKey("TaskId")]
        public virtual TaskItem? Task { get; set; }

        [ForeignKey("EmployeeId")]  
        public virtual Employee? Employee { get; set; }
    }
}
