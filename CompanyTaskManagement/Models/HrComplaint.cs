using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class HrComplaint
    {
        public int Id { get; set; }

        public string TicketNumber { get; set; } = $"HRG-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

        [Required(ErrorMessage = "Please provide a subject for the grievance.")]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please provide detailed description of the complaint.")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = "Workplace Environment"; 
        // e.g., "Workplace Environment", "Workload & Deadlines", "Interpersonal Conflict", "Compensation & Payroll", "Technical & Hardware Issue", "Harassment & Conduct", "Process & Management", "Other"

        [Required]
        [StringLength(50)]
        public string Priority { get; set; } = "Medium"; 
        // "Low", "Medium", "High", "Urgent"

        public bool IsAnonymous { get; set; } = false;

        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        [StringLength(150)]
        public string? SubmitterName { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string? SubmitterEmail { get; set; }

        [StringLength(100)]
        public string? Department { get; set; }

        [StringLength(500)]
        public string? AttachmentPath { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Submitted"; 
        // "Submitted", "Under Review", "In Investigation", "Resolved", "Dismissed"

        public string? HrResponseNotes { get; set; }

        [StringLength(150)]
        public string? HrInvestigatorName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ResolvedAt { get; set; }
    }
}
