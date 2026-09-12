using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Project name is required")]
        [StringLength(150, ErrorMessage = "Project name cannot exceed 150 characters")]
        public string ProjectName { get; set; } = string.Empty;

        [StringLength(150)]
        public string ClientCompany { get; set; } = string.Empty;

        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "In Progress"; // Planning, In Progress, On Hold, Completed

        [Required]
        [StringLength(50)]
        public string Priority { get; set; } = "High"; // Low, Medium, High, Critical

        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? TargetEndDate { get; set; }

        public decimal? Budget { get; set; }

        [StringLength(100)]
        public string LeadManagerName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
