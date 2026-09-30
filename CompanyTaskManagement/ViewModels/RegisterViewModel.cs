using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.ViewModels
{
    public class RegisterViewModel
    {
        [Display(Name = "First Name")]
        public string? FirstName { get; set; }

        [Display(Name = "Last Name")]
        public string? LastName { get; set; }

        [Display(Name = "Full Name")]
        public string? FullName { get; set; }

        public string GetEffectiveFullName()
        {
            if (!string.IsNullOrWhiteSpace(FullName))
                return FullName.Trim();

            var combined = $"{FirstName} {LastName}".Trim();
            return !string.IsNullOrWhiteSpace(combined) ? combined : "Team Member";
        }

        [Required(ErrorMessage = "Corporate Email Address is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid corporate email address.")]
        [StringLength(150)]
        [Display(Name = "Corporate Email")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Please enter a valid phone number.")]
        [Display(Name = "Mobile Number")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters long.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm Password is required.")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Password and confirmation do not match.")]
        [Display(Name = "Confirm Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Account Role")]
        public string Role { get; set; } = "Employee"; // Admin, HR, Employee

        [Display(Name = "Department")]
        public string? Department { get; set; } = "Engineering";

        [Display(Name = "Designation")]
        public string? Designation { get; set; } = "Software Engineer";

        [Display(Name = "Link to Existing Team Member")]
        public int? EmployeeId { get; set; }
    }
}
