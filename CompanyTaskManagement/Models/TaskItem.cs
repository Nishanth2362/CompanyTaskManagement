using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompanyTaskManagement.Models
{
    public class TaskItem
    {
        public int Id { get; set; }

        [Required]
        public string TaskName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public TaskStatus Status { get; set; } = TaskStatus.ToDo;

        public int? ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? EndDate { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Range(0, 100)]
        public int Progress { get; set; } = 0;

        public string? DelayReason { get; set; }

        public string? ErrorDetails { get; set; }

        public string? ErrorScreenshotPath { get; set; }

        public ICollection<TaskCompany> TaskCompanies { get; set; }
            = new List<TaskCompany>();

        public ICollection<TaskEmployee> TaskEmployees { get; set; }
            = new List<TaskEmployee>();

        public virtual ICollection<TaskActivityLog> TaskActivityLogs { get; set; }
            = new List<TaskActivityLog>();
    }
}