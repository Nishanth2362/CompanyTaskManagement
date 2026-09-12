using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Employee name is required")]
        [StringLength(100, ErrorMessage = "Employee name cannot exceed 100 characters")]
        public string Name { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Please enter a valid email address")]
        [StringLength(150)]
        public string? Email { get; set; }

        [StringLength(100)]
        public string? Designation { get; set; } = "Software Engineer";

        [StringLength(100)]
        public string? Department { get; set; } = "Engineering";

        [Phone]
        [StringLength(50)]
        public string? Phone { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        public ICollection<TaskEmployee> TaskEmployees { get; set; }
            = new List<TaskEmployee>();
    }
}