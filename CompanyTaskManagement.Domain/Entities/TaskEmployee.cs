using CompanyTaskManagement.Domain.Contract;
using CompanyTaskManagement.Domain.Enums;
namespace CompanyTaskManagement.Domain.Entities
{
    public class TaskEmployee : IEntity
    {
        public int TaskId { get; set; }

        public TaskItem Task { get; set; } = null!;

        public int EmployeeId { get; set; }

        public Employee Employee { get; set; } = null!;
    }
}

