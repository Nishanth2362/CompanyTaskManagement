using CompanyTaskManagement.Domain.Contract;
using CompanyTaskManagement.Domain.Enums;
namespace CompanyTaskManagement.Domain.Entities
{
    public class TaskCompany : IEntity
    {
        public int TaskId { get; set; }

        public TaskItem Task { get; set; } = null!;

        public int CompanyId { get; set; }

        public Company Company { get; set; } = null!;
    }
}

