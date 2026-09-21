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

        [Phone(ErrorMessage = "Please enter a valid phone number")]
        [StringLength(10, ErrorMessage = "Phone number cannot exceed 10 digits")]
        [RegularExpression(@"^$|^[0-9]{10}$", ErrorMessage = "Phone number must be exactly 10 digits")]
        public string? Phone { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        public ICollection<TaskEmployee> TaskEmployees { get; set; }
            = new List<TaskEmployee>();
    }
}