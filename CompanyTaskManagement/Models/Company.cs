using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class Company
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Company name is required")]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public ICollection<TaskCompany> TaskCompanies { get; set; }
            = new List<TaskCompany>();
    }
}