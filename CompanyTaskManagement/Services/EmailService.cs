using System.Net;
using System.Net.Mail;
using System.Text;
using CompanyTaskManagement.Models;
using Microsoft.Extensions.Options;

namespace CompanyTaskManagement.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
        {
            _emailSettings = emailSettings?.Value ?? new EmailSettings();
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, string? ccEmail = null, string? bccEmail = null)
        {
            try
            {
                var targetCc = !string.IsNullOrWhiteSpace(ccEmail) ? ccEmail : _emailSettings.CcEmailAddress;
                var targetBcc = !string.IsNullOrWhiteSpace(bccEmail) ? bccEmail : _emailSettings.BccEmailAddress;

                if (_emailSettings.UseSimulationMode || string.IsNullOrWhiteSpace(_emailSettings.SenderPassword))
                {
                    _logger.LogInformation("=================================================");
                    _logger.LogInformation("[SIMULATED EMAIL SENT]");
                    _logger.LogInformation("To: {ToEmail}", toEmail);
                    _logger.LogInformation("Cc: {CcEmail}", targetCc);
                    _logger.LogInformation("Bcc: {BccEmail}", targetBcc);
                    _logger.LogInformation("Subject: {Subject}", subject);
                    _logger.LogInformation("=================================================");
                    return true;
                }

                using var message = new MailMessage();
                message.From = new MailAddress(_emailSettings.SenderEmail, "AUXINZ.io Task Management System");
                message.To.Add(new MailAddress(toEmail));

                if (!string.IsNullOrWhiteSpace(targetCc))
                {
                    foreach (var cc in targetCc.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        message.CC.Add(new MailAddress(cc.Trim()));
                    }
                }

                if (!string.IsNullOrWhiteSpace(targetBcc))
                {
                    foreach (var bcc in targetBcc.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        message.Bcc.Add(new MailAddress(bcc.Trim()));
                    }
                }

                message.Subject = subject;
                message.Body = htmlBody;
                message.IsBodyHtml = true;

                using var client = new SmtpClient(_emailSettings.SmtpServer, _emailSettings.SmtpPort);
                client.Credentials = new NetworkCredential(_emailSettings.SenderEmail, _emailSettings.SenderPassword);
                client.EnableSsl = _emailSettings.EnableSsl;

                await client.SendMailAsync(message);
                _logger.LogInformation("Email sent successfully to {ToEmail} (CC: {Cc}, BCC: {Bcc})", toEmail, targetCc, targetBcc);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToEmail}", toEmail);
                return false;
            }
        }

        public async Task<bool> SendOverdueTasksNotificationToHrAsync(IEnumerable<TaskItem> overdueTasks, string? customToEmail = null, string? customCcEmail = null, string? customBccEmail = null)
        {
            try
            {
                var taskList = overdueTasks?.Where(t => t != null).ToList() ?? new List<TaskItem>();
                if (!taskList.Any())
                {
                    _logger.LogInformation("No overdue tasks provided for HR notification email.");
                    return true;
                }

                var hrEmail = !string.IsNullOrWhiteSpace(customToEmail)
                    ? customToEmail
                    : (!string.IsNullOrWhiteSpace(_emailSettings?.HrEmailAddress)
                        ? _emailSettings.HrEmailAddress
                        : "mukesh@auxinz.io");

                var ccEmail = !string.IsNullOrWhiteSpace(customCcEmail)
                    ? customCcEmail
                    : _emailSettings?.CcEmailAddress;

                var bccEmail = !string.IsNullOrWhiteSpace(customBccEmail)
                    ? customBccEmail
                    : _emailSettings?.BccEmailAddress;

                var subject = $"⚠️ Alert: {taskList.Count} Uncompleted Overdue Task(s) Requiring HR Attention";
                var sb = new StringBuilder();

                sb.AppendLine("<!DOCTYPE html>");
                sb.AppendLine("<html><head><style>");
                sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; background-color: #f4f6f9; color: #333; margin: 0; padding: 20px; }");
                sb.AppendLine(".container { max-width: 680px; background: #ffffff; border-radius: 12px; padding: 24px; box-shadow: 0 4px 12px rgba(0,0,0,0.1); }");
                sb.AppendLine(".header { background: linear-gradient(135deg, #dc2626, #991b1b); color: #ffffff; padding: 18px 24px; border-radius: 8px; margin-bottom: 20px; }");
                sb.AppendLine(".header h2 { margin: 0; font-size: 20px; }");
                sb.AppendLine(".task-card { border: 1px solid #e2e8f0; border-left: 5px solid #dc2626; border-radius: 8px; padding: 16px; margin-bottom: 14px; background: #fafafa; }");
                sb.AppendLine(".badge { display: inline-block; padding: 4px 8px; border-radius: 4px; font-size: 11px; font-weight: bold; text-transform: uppercase; }");
                sb.AppendLine(".badge-urgent { background: #fee2e2; color: #dc2626; }");
                sb.AppendLine(".badge-high { background: #fef3c7; color: #d97706; }");
                sb.AppendLine(".badge-medium { background: #e0e7ff; color: #4f46e5; }");
                sb.AppendLine(".meta { font-size: 13px; color: #64748b; margin-top: 8px; }");
                sb.AppendLine(".footer { margin-top: 24px; font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0; padding-top: 14px; }");
                sb.AppendLine("</style></head><body>");

                sb.AppendLine("<div class='container'>");
                sb.AppendLine("<div class='header'>");
                sb.AppendLine("<h2>⚠️ HR Task Escalation Report</h2>");
                sb.AppendLine("<p style='margin:4px 0 0 0; opacity:0.9;'>The following assigned task(s) have passed their target due date without completion.</p>");
                sb.AppendLine("</div>");

                foreach (var task in taskList)
                {
                    var compNames = task.TaskCompanies != null && task.TaskCompanies.Any(c => c?.Company != null)
                        ? string.Join(", ", task.TaskCompanies.Where(c => c?.Company != null).Select(c => c.Company!.Name))
                        : "None";
                    var empNames = task.TaskEmployees != null && task.TaskEmployees.Any(e => e?.Employee != null)
                        ? string.Join(", ", task.TaskEmployees.Where(e => e?.Employee != null).Select(e => e.Employee!.Name))
                        : "Unassigned";

                    var daysOverdue = task.DueDate.HasValue ? (DateTime.Today - task.DueDate.Value.Date).Days : 0;
                    var overdueTxt = daysOverdue > 0 ? $"{daysOverdue} day(s) overdue" : "Overdue today";
                    var priorityBadgeClass = task.Priority == TaskPriority.Urgent ? "badge-urgent" :
                                             task.Priority == TaskPriority.High ? "badge-high" : "badge-medium";

                    sb.AppendLine("<div class='task-card'>");
                    sb.AppendLine($"<div style='display:flex; justify-content:space-between; align-items:center;'>");
                    sb.AppendLine($"<strong style='font-size:16px; color:#0f172a;'>#{task.Id.ToString("D3")} - {task.TaskName}</strong>");
                    sb.AppendLine($"<span class='badge {priorityBadgeClass}'>{task.Priority} Priority</span>");
                    sb.AppendLine("</div>");

                    if (!string.IsNullOrWhiteSpace(task.Description))
                    {
                        sb.AppendLine($"<p style='font-size:14px; color:#475569; margin:8px 0;'>{task.Description}</p>");
                    }

                    sb.AppendLine("<div class='meta'>");
                    sb.AppendLine($"<strong>Assigned Team:</strong> {empNames}<br/>");
                    sb.AppendLine($"<strong>Companies:</strong> {compNames}<br/>");
                    sb.AppendLine($"<strong>Due Date:</strong> {(task.DueDate.HasValue ? task.DueDate.Value.ToString("MMM dd, yyyy") : "N/A")} (<span style='color:#dc2626; font-weight:bold;'>{overdueTxt}</span>)<br/>");
                    sb.AppendLine($"<strong>Current Progress:</strong> {task.Progress}% | <strong>Status:</strong> {task.Status}");
                    sb.AppendLine("</div></div>");
                }

                sb.AppendLine("<div class='footer'>");
                sb.AppendLine($"<p>This automated escalation report was generated by <strong>AUXINZ.io Task Management System</strong> on {DateTime.Now:f}.</p>");
                sb.AppendLine($"<p>Target Recipient: {hrEmail} (CC: {ccEmail}, BCC: {bccEmail})</p>");
                sb.AppendLine("</div></div></body></html>");

                return await SendEmailAsync(hrEmail, subject, sb.ToString(), ccEmail, bccEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing HR overdue task notification email.");
                return false;
            }
        }

        public async Task<bool> SendGrievanceNotificationToHrAsync(HrComplaint complaint)
        {
            if (complaint == null) return false;

            var subject = $"🔒 [Confidential Grievance] Ticket #{complaint.TicketNumber}: {complaint.Subject} ({complaint.Priority} Priority)";
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html><head><style>");
            sb.AppendLine("body { font-family: 'Segoe UI', Arial, sans-serif; background-color: #0b132b; color: #f8fafc; margin: 0; padding: 24px; }");
            sb.AppendLine(".container { max-width: 680px; background: #1c2541; border: 1px solid rgba(255,255,255,0.15); border-radius: 14px; padding: 28px; box-shadow: 0 12px 30px rgba(0,0,0,0.5); }");
            sb.AppendLine(".header { background: linear-gradient(135deg, #4338ca, #1e1b4b); border-left: 6px solid #f59e0b; color: #ffffff; padding: 20px 24px; border-radius: 10px; margin-bottom: 24px; }");
            sb.AppendLine(".badge { display: inline-block; padding: 5px 10px; border-radius: 6px; font-size: 12px; font-weight: bold; text-transform: uppercase; }");
            sb.AppendLine(".badge-urgent { background: #fee2e2; color: #dc2626; }");
            sb.AppendLine(".badge-high { background: #fef3c7; color: #b45309; }");
            sb.AppendLine(".badge-normal { background: #e0e7ff; color: #4338ca; }");
            sb.AppendLine(".detail-box { background: rgba(255,255,255,0.04); border: 1px solid rgba(255,255,255,0.1); border-radius: 10px; padding: 18px; margin-bottom: 18px; }");
            sb.AppendLine(".label { color: #94a3b8; font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 4px; }");
            sb.AppendLine(".value { color: #ffffff; font-size: 15px; margin-bottom: 12px; line-height: 1.5; }");
            sb.AppendLine(".footer { margin-top: 24px; font-size: 12px; color: #64748b; border-top: 1px solid rgba(255,255,255,0.1); padding-top: 14px; }");
            sb.AppendLine("</style></head><body>");

            sb.AppendLine("<div class='container'>");
            sb.AppendLine("<div class='header'>");
            sb.AppendLine($"<div style='font-size:12px; color:#f59e0b; font-weight:bold; letter-spacing:0.1em; text-transform:uppercase;'>Confidential HR Grievance Portal</div>");
            sb.AppendLine($"<h2 style='margin:6px 0 0 0; font-size:22px;'>Ticket #{complaint.TicketNumber}</h2>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='detail-box'>");
            sb.AppendLine($"<div class='label'>Subject</div><div class='value' style='font-weight:bold; font-size:17px;'>{complaint.Subject}</div>");
            sb.AppendLine($"<div class='label'>Category</div><div class='value'><span style='background:#312e81; color:#c7d2fe; padding:4px 8px; border-radius:4px; font-size:13px;'>{complaint.Category}</span></div>");
            sb.AppendLine($"<div class='label'>Priority</div><div class='value'><span class='badge {(complaint.Priority == "Urgent" ? "badge-urgent" : complaint.Priority == "High" ? "badge-high" : "badge-normal")}'>{complaint.Priority} Priority</span></div>");
            
            var submitterText = complaint.IsAnonymous 
                ? "🔒 <em>Anonymous Employee/Intern (Identity Protected)</em>" 
                : $"<strong>{complaint.SubmitterName}</strong> ({complaint.SubmitterEmail}) - Dept: {complaint.Department ?? "Not Specified"}";
            
            sb.AppendLine($"<div class='label'>Submitted By</div><div class='value'>{submitterText}</div>");
            sb.AppendLine($"<div class='label'>Submitted Timestamp</div><div class='value'>{complaint.CreatedAt:f}</div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='detail-box'>");
            sb.AppendLine("<div class='label'>Full Grievance Statement / Details</div>");
            sb.AppendLine($"<div class='value' style='white-space: pre-wrap; background:rgba(0,0,0,0.25); padding:14px; border-radius:8px; border:1px solid rgba(255,255,255,0.05);'>{complaint.Description}</div>");
            sb.AppendLine("</div>");

            sb.AppendLine("<div class='footer'>");
            sb.AppendLine($"<p>🔒 <strong>Confidentiality Notice:</strong> This message was sent directly from the AUXINZ.io Confidential HR Grievance Portal. Please review and manage this ticket inside the internal HR portal.</p>");
            sb.AppendLine($"<p>HR Destination: {_emailSettings.HrEmailAddress}</p>");
            sb.AppendLine("</div></div></body></html>");

            return await SendEmailAsync(_emailSettings.HrEmailAddress, subject, sb.ToString());
        }
    }
}
