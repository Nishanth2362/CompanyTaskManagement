using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Interfaces.Services;
using CompanyTaskManagement.Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompanyTaskManagement.Infrastructure.Services
{
    public class MailService : IMailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<MailService> _logger;

        public MailService(IOptions<EmailSettings> emailSettings, ILogger<MailService> logger)
        {
            _emailSettings = emailSettings?.Value ?? new EmailSettings();
            _logger = logger;
        }

        public async Task SendTaskAssignedEmailAsync(string toEmail, string employeeName, string taskTitle, string priority, string? dueDate, string? description)
        {
            var subject = $"[TASK ASSIGNED] {taskTitle} ({priority})";
            var body = $"<h3>Hello {employeeName},</h3><p>You have been assigned the following task:</p><p><b>{taskTitle}</b></p><p>Due: {dueDate ?? "Not specified"}</p><p>{description}</p>";
            await SendAsync(toEmail, subject, body);
        }

        public async Task SendOverdueNotificationEmailAsync(string toEmail, string employeeName, string taskTitle, string? dueDate, string status, string? delayReason)
        {
            var subject = $"[OVERDUE ALERT] Task Overdue: {taskTitle}";
            var body = $"<h3>Attention {employeeName},</h3><p>Task <b>{taskTitle}</b> is currently overdue.</p><p>Status: {status}</p><p>Reason: {delayReason ?? "None provided"}</p>";
            await SendAsync(toEmail, subject, body);
        }

        private async Task<bool> SendAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                if (_emailSettings.UseSimulationMode || string.IsNullOrWhiteSpace(_emailSettings.SenderPassword))
                {
                    _logger.LogInformation("[SIMULATED EMAIL SENT] To: {ToEmail} | Subject: {Subject}", toEmail, subject);
                    return true;
                }

                using var message = new MailMessage();
                message.From = new MailAddress(_emailSettings.SenderEmail, "Auxinz Enterprise System");
                message.To.Add(toEmail);
                message.Subject = subject;
                message.Body = htmlBody;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort);
                client.Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
                client.EnableSsl = _emailSettings.EnableSsl;
                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail}: {Message}", toEmail, ex.Message);
                return false;
            }
        }
    }
}
