using CompanyTaskManagement.Models;

namespace CompanyTaskManagement.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendOverdueTasksNotificationToHrAsync(IEnumerable<TaskItem> overdueTasks);
        Task<bool> SendGrievanceNotificationToHrAsync(HrComplaint complaint);
    }
}
