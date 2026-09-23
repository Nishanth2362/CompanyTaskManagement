using System.ComponentModel.DataAnnotations;
using CompanyTaskManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using TaskStatus = CompanyTaskManagement.Models.TaskStatus;

namespace CompanyTaskManagement.ViewModels
{
    public class TaskViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Task name is required")]
        [StringLength(100, ErrorMessage = "Task name cannot exceed 100 characters")]
        public string TaskName { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string? Description { get; set; }

        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        public TaskStatus Status { get; set; } = TaskStatus.ToDo;

        public int? ProjectId { get; set; }

        public List<SelectListItem> Projects { get; set; }
            = new List<SelectListItem>();

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime? StartDate { get; set; }

        [DataType(DataType.DateTime)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime? EndDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DueDate { get; set; }

        [Range(0, 100, ErrorMessage = "Progress must be between 0% and 100%")]
        public int Progress { get; set; } = 0;

        [StringLength(500, ErrorMessage = "Delay reason cannot exceed 500 characters")]
        public string? DelayReason { get; set; }

        [StringLength(1000, ErrorMessage = "Error details cannot exceed 1000 characters")]
        public string? ErrorDetails { get; set; }

        public string? ErrorScreenshotPath { get; set; }

        public Microsoft.AspNetCore.Http.IFormFile? ErrorScreenshotFile { get; set; }

        public List<SelectListItem> Companies { get; set; }
            = new List<SelectListItem>();

        [Required(ErrorMessage = "Please select at least one company")]
        public List<int> SelectedCompanyIds { get; set; }
            = new List<int>();

        public List<SelectListItem> Employees { get; set; }
            = new List<SelectListItem>();

        [Required(ErrorMessage = "Please select at least one employee")]
        public List<int> SelectedEmployeeIds { get; set; }
            = new List<int>();

        public List<SelectListItem> PriorityList { get; set; }
            = new List<SelectListItem>();

        public List<SelectListItem> StatusList { get; set; }
            = new List<SelectListItem>();
    }
}