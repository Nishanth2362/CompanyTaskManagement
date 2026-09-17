using CompanyTaskManagement.Models;

namespace CompanyTaskManagement.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? ccEmail = null, string? bccEmail = null);
        Task<bool> SendOverdueTasksNotificationToHrAsync(IEnumerable<TaskItem> overdueTasks, string? customToEmail = null, string? customCcEmail = null, string? customBccEmail = null);
        Task<bool> SendGrievanceNotificationToHrAsync(HrComplaint complaint);
    }
}
