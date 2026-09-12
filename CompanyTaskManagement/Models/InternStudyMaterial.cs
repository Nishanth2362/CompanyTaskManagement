using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class InternStudyMaterial
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Track { get; set; } = "Backend .NET / C#"; 
        // Tracks: "Backend .NET / C#", "Frontend & UI/UX", "SQL & Database Design", "Git, DevOps & Cloud", "Software Architecture", "QA & Testing"

        [Required]
        [StringLength(50)]
        public string Difficulty { get; set; } = "Beginner"; 
        // "Beginner", "Intermediate", "Advanced"

        [Required]
        [StringLength(50)]
        public string ContentType { get; set; } = "Interactive Guide"; 
        // "Interactive Guide", "Video Tutorial", "Official Documentation", "Code Sample", "Architecture Sheet", "Cheat Sheet"

        [Required]
        public string Description { get; set; } = string.Empty;

        [StringLength(500)]
        public string? ResourceUrl { get; set; }

        [StringLength(500)]
        public string? FilePath { get; set; }

        public int EstimatedMinutes { get; set; } = 30;

        [StringLength(200)]
        public string? Tags { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
