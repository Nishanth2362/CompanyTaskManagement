using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CompanyTaskManagement.Models
{
    public enum TeamTaskStatus
    {
        [Display(Name = "Pending")]
        Pending = 0,

        [Display(Name = "In Progress")]
        InProgress = 1,

        [Display(Name = "Completed")]
        Completed = 2,

        [Display(Name = "Not Completed")]
        NotCompleted = 3
    }

    public class TeamTask
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int TeamId { get; set; }

        [ForeignKey("TeamId")]
        public virtual Team? Team { get; set; }

        public int? AssignedByLeaderId { get; set; }

        [ForeignKey("AssignedByLeaderId")]
        public virtual Employee? AssignedByLeader { get; set; }

        [Required]
        public int AssignedToEmployeeId { get; set; }

        [ForeignKey("AssignedToEmployeeId")]
        public virtual Employee? AssignedToEmployee { get; set; }

        [Required(ErrorMessage = "Task title is required")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Task description is required")]
        public string Description { get; set; } = string.Empty;

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public TeamTaskStatus Status { get; set; } = TeamTaskStatus.Pending;

        [StringLength(1000)]
        public string? IncompleteReason { get; set; }

        [StringLength(1000)]
        public string? LeaderSuggestion { get; set; }

        public int? ProjectId { get; set; }

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        [StringLength(50)]
        public string? TaskDurationType { get; set; } = "Full Day";

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public DateTime? DueDate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }
    }
}
