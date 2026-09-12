using System.ComponentModel.DataAnnotations;

namespace CompanyTaskManagement.Models
{
    public class InternDoubtClarification
    {
        public int Id { get; set; }

        public int InternDoubtId { get; set; }
        public InternDoubt? InternDoubt { get; set; }

        [Required(ErrorMessage = "Mentor / Employee Name is required")]
        [StringLength(150)]
        public string ClarifiedByEmployeeName { get; set; } = string.Empty;

        public int? EmployeeId { get; set; }
        public Employee? Employee { get; set; }

        [Required(ErrorMessage = "Please provide the solution or clarification.")]
        public string ClarificationText { get; set; } = string.Empty;

        public string? CodeSolution { get; set; }

        [StringLength(500)]
        public string? HelpfulLink { get; set; }

        public bool IsAcceptedSolution { get; set; } = false;

        public DateTime AnsweredAt { get; set; } = DateTime.Now;
    }
}
