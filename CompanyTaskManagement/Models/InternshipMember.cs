using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class InternshipMember
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [EmailAddress]
        [StringLength(150)]
        public string? Email { get; set; }

        [Required]
        [StringLength(100)]
        public string Role { get; set; } = "Intern"; 
        // "Intern", "Graduate Trainee", "Technical Mentor", "Senior Lead"

        [Required]
        [StringLength(100)]
        public string Domain { get; set; } = "Backend .NET / C#"; 
        // "Backend .NET / C#", "Frontend & UI/UX", "Full Stack", "SQL & Database Design", "Git & DevOps", "QA & Testing"

        public int? MentorId { get; set; }

        [StringLength(150)]
        public string? MentorName { get; set; }

        public DateTime JoinedDate { get; set; } = DateTime.Today;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Active"; 
        // "Active", "On Track", "Completed / Graduated"

        [StringLength(500)]
        public string? Notes { get; set; }

        public int CompletedModulesCount { get; set; } = 0;
    }
}
