using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class InternDoubt
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Please enter a summary of your doubt.")]
        [StringLength(250)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please describe what issue or concept you need clarification on.")]
        public string Description { get; set; } = string.Empty;

        public string? CodeSnippet { get; set; }

        [Required]
        [StringLength(100)]
        public string Domain { get; set; } = ".NET / C#"; 
        // ".NET / C#", "ASP.NET MVC", "SQL Server / EF Core", "JavaScript / Frontend", "Git / GitHub Workflow", "System Architecture", "Debugging & Error"

        [Required]
        [StringLength(50)]
        public string Urgency { get; set; } = "Normal"; 
        // "Normal", "Help Needed Today", "Blocker / Critical"

        [Required(ErrorMessage = "Please enter your name or intern handle.")]
        [StringLength(150)]
        public string InternName { get; set; } = string.Empty;

        public int? InternEmployeeId { get; set; }
        public Employee? InternEmployee { get; set; }

        [StringLength(500)]
        public string? ScreenshotPath { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Open"; 
        // "Open", "Clarified", "Resolved"

        public int Upvotes { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<InternDoubtClarification> Clarifications { get; set; } = new List<InternDoubtClarification>();
    }
}
