using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.Tasks.Queries.GetOverdue;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Services;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            var result = await mediator.Send(new GetOverdueTasksEntitiesQuery());
            var overdueTasks = result.Data ?? new List<TaskItem>();

            if (overdueTasks.Count > 0)
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
