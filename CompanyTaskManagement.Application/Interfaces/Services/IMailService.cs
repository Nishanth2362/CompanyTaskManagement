using System.Threading.Tasks;

namespace CompanyTaskManagement.Application.Interfaces.Services
{
    public interface IMailService
    {
        Task SendTaskAssignedEmailAsync(string toEmail, string employeeName, string taskTitle, string priority, string? dueDate, string? description);
        Task SendOverdueNotificationEmailAsync(string toEmail, string employeeName, string taskTitle, string? dueDate, string status, string? delayReason);
    }
}
