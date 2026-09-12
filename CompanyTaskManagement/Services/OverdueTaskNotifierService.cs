using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using Microsoft.EntityFrameworkCore;
using TaskStatus = CompanyTaskManagement.Models.TaskStatus;

namespace CompanyTaskManagement.Services
{
    public class OverdueTaskNotifierService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OverdueTaskNotifierService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

        public OverdueTaskNotifierService(IServiceProvider serviceProvider, ILogger<OverdueTaskNotifierService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("OverdueTaskNotifierService background worker initialized.");

            try
            {
                // Initial startup check after 15 seconds — waits silently if cancelled
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // App is shutting down before first check — exit cleanly
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndNotifyOverdueTasksAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while executing OverdueTaskNotifierService check.");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // App is shutting down — exit the loop cleanly without logging an error
                    break;
                }
            }

            _logger.LogInformation("OverdueTaskNotifierService is stopping gracefully.");
        }

        private async Task CheckAndNotifyOverdueTasksAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var now = DateTime.Now;
            var today = DateTime.Today;
            var overdueTasks = await dbContext.Tasks
                .AsSplitQuery()
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .Where(t => t.Status != TaskStatus.Completed && 
                            ((t.EndDate.HasValue && t.EndDate.Value <= now) || 
                             (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) || 
                             !string.IsNullOrEmpty(t.DelayReason)))
                .ToListAsync();

            if (overdueTasks.Any())
            {
                _logger.LogInformation("Found {Count} overdue uncompleted task(s). Sending email notification to HR...", overdueTasks.Count);
                await emailService.SendOverdueTasksNotificationToHrAsync(overdueTasks);
            }
            else
            {
                _logger.LogInformation("OverdueTaskNotifierService check completed: No overdue uncompleted tasks found.");
            }
        }
    }
}
