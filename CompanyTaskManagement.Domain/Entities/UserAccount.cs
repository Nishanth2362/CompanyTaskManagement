using System;
using System.ComponentModel.DataAnnotations;
using CompanyTaskManagement.Domain.Contract;

namespace CompanyTaskManagement.Domain.Entities
{
    public class UserAccount : IEntity<int>
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Role { get; set; } = "Employee"; // Admin, HR, Employee

        public int? EmployeeId { get; set; }
        public virtual Employee? Employee { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastLoginAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
