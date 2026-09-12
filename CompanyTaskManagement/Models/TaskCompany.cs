namespace CompanyTaskManagement.Models
{
    public class TaskCompany
    {
        public int TaskId { get; set; }

        public TaskItem Task { get; set; } = null!;

        public int CompanyId { get; set; }

        public Company Company { get; set; } = null!;
    }
}