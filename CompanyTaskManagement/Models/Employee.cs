using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
        [RegularExpression(@"^[a-zA-Z\s\.'-]+$", ErrorMessage = "Full Name can only contain letters, spaces, dots, hyphens, and apostrophes.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Corporate Email Address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address (e.g., alex.henderson@auxinz.io).")]
        [RegularExpression(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", ErrorMessage = "Please enter a valid email address format (e.g., alex.henderson@auxinz.io).")]
        [StringLength(150, ErrorMessage = "Corporate Email Address cannot exceed 150 characters.")]
        public string? Email { get; set; }

        [StringLength(100, ErrorMessage = "Company Name cannot exceed 100 characters.")]
        public string? CompanyName { get; set; } = "Auxinzio";

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