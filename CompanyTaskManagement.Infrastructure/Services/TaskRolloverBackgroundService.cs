using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Domain.Enums;
using CompanyTaskManagement.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskStatus = CompanyTaskManagement.Domain.Enums.TaskStatus;

namespace CompanyTaskManagement.Infrastructure.Services
{
    public class TaskRolloverBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TaskRolloverBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

        public TaskRolloverBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<TaskRolloverBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TaskRolloverBackgroundService initialized.");

            // Initial short delay on app startup
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    await ExecuteRolloverSyncAsync(dbContext, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during TaskRolloverBackgroundService execution.");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("TaskRolloverBackgroundService is stopping gracefully.");
        }

        private async Task ExecuteRolloverSyncAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            // 1. Sync overdue TeamTasks into main Tasks table if missing
            var overdueTeamTasks = await dbContext.TeamTasks
                .AsSplitQuery()
                .Include(tt => tt.AssignedToEmployee)
                .Include(tt => tt.Project)
                .Where(tt => tt.Status == TeamTaskStatus.NotCompleted ||
                             (tt.Status != TeamTaskStatus.Completed &&
                              ((tt.EndDate.HasValue && tt.EndDate.Value <= now) ||
                               (tt.DueDate.HasValue && tt.DueDate.Value <= now))))
                .ToListAsync(cancellationToken);

            if (overdueTeamTasks.Any())
            {
                var overdueTitles = overdueTeamTasks.Select(tt => tt.Title).Distinct().ToList();
                var existingTasksList = await dbContext.Tasks
                    .Where(t => overdueTitles.Contains(t.TaskName))
                    .ToListAsync(cancellationToken);

                var existingTasks = existingTasksList
                    .GroupBy(t => t.TaskName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                foreach (var tt in overdueTeamTasks)
                {
                    if (!existingTasks.TryGetValue(tt.Title, out var matchingTask))
                    {
                        matchingTask = new TaskItem
                        {
                            TaskName = tt.Title,
                            Description = tt.Description,
                            Priority = tt.Priority,
                            Status = TaskStatus.ToDo,
                            ProjectId = tt.ProjectId,
                            StartDate = tt.StartDate ?? tt.CreatedAt,
                            EndDate = tt.EndDate ?? tt.DueDate,
                            DueDate = tt.DueDate ?? tt.EndDate,
                            CreatedAt = tt.CreatedAt,
                            Progress = 0,
                            DelayReason = !string.IsNullOrWhiteSpace(tt.IncompleteReason)
                                ? tt.IncompleteReason
                                : $"Overdue Team Task (End: {(tt.EndDate.HasValue ? tt.EndDate.Value.ToString("MMM dd, h:mm tt") : "Elapsed")})"
                        };
                        dbContext.Tasks.Add(matchingTask);
                        await dbContext.SaveChangesAsync(cancellationToken);

                        if (tt.AssignedToEmployeeId > 0)
                        {
                            dbContext.TaskEmployees.Add(new TaskEmployee
                            {
                                TaskId = matchingTask.Id,
                                EmployeeId = tt.AssignedToEmployeeId
                            });
                        }
                        existingTasks[tt.Title] = matchingTask;
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(tt.IncompleteReason) && string.IsNullOrWhiteSpace(matchingTask.DelayReason))
                        {
                            matchingTask.DelayReason = tt.IncompleteReason;
                        }
                        else if (string.IsNullOrWhiteSpace(matchingTask.DelayReason) && matchingTask.Status != TaskStatus.Completed)
                        {
                            matchingTask.DelayReason = $"Overdue Team Task (End: {(tt.EndDate.HasValue ? tt.EndDate.Value.ToString("MMM dd, h:mm tt") : "Elapsed")})";
                        }
                    }
                }
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // 2. Sync Completed Team Tasks & Team Leader Reviews into Tasks table
            var completedTeamTasks = await dbContext.TeamTasks
                .AsSplitQuery()
                .Include(tt => tt.AssignedToEmployee)
                .Include(tt => tt.Project)
                .Where(tt => tt.Status == TeamTaskStatus.Completed)
                .ToListAsync(cancellationToken);

            if (completedTeamTasks.Any())
            {
                var completedTitles = completedTeamTasks.Select(ctt => ctt.Title).Distinct().ToList();
                var completedExistingList = await dbContext.Tasks
                    .Where(t => completedTitles.Contains(t.TaskName))
                    .ToListAsync(cancellationToken);

                var existingTasks = completedExistingList
                    .GroupBy(t => t.TaskName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                foreach (var ctt in completedTeamTasks)
                {
                    if (existingTasks.TryGetValue(ctt.Title, out var mTask))
                    {
                        mTask.Status = TaskStatus.Completed;
                        mTask.Progress = 100;
                        mTask.DelayReason = null;
                        if (ctt.CompletedAt.HasValue)
                        {
                            mTask.EndDate = ctt.CompletedAt.Value;
                        }
                    }
                    else
                    {
                        var newCompletedTask = new TaskItem
                        {
                            TaskName = ctt.Title,
                            Description = ctt.Description,
                            Priority = ctt.Priority,
                            Status = TaskStatus.Completed,
                            ProjectId = ctt.ProjectId,
                            StartDate = ctt.StartDate ?? ctt.CreatedAt,
                            EndDate = ctt.CompletedAt ?? ctt.EndDate ?? DateTime.Now,
                            DueDate = ctt.DueDate ?? ctt.EndDate,
                            CreatedAt = ctt.CreatedAt,
                            Progress = 100,
                            DelayReason = null
                        };
                        dbContext.Tasks.Add(newCompletedTask);
                        await dbContext.SaveChangesAsync(cancellationToken);

                        if (ctt.AssignedToEmployeeId > 0)
                        {
                            dbContext.TaskEmployees.Add(new TaskEmployee
                            {
                                TaskId = newCompletedTask.Id,
                                EmployeeId = ctt.AssignedToEmployeeId
                            });
                        }
                        existingTasks[ctt.Title] = newCompletedTask;
                    }
                }
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // 3. Process Auto-Rollover for past incomplete tasks (deduplicated per day)
            var pastIncompleteTasks = await dbContext.Tasks
                .Where(t => t.Status != TaskStatus.Completed &&
                            ((t.EndDate.HasValue && t.EndDate.Value <= now) ||
                             (t.DueDate.HasValue && t.DueDate.Value.Date < today)))
                .ToListAsync(cancellationToken);

            if (pastIncompleteTasks.Any())
            {
                var alreadyLoggedTaskIds = await dbContext.TaskActivityLogs
                    .Where(l => l.ActionType == "Overdue / Delay Flagged" && l.LoggedAt.Date == today)
                    .Select(l => l.TaskId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                var alreadyLoggedSet = new HashSet<int>(alreadyLoggedTaskIds);
                var taskIds = pastIncompleteTasks.Select(t => t.Id).ToList();

                var taskEmpMap = await dbContext.TaskEmployees
                    .Where(te => taskIds.Contains(te.TaskId))
                    .GroupBy(te => te.TaskId)
                    .ToDictionaryAsync(g => g.Key, g => g.Select(x => x.EmployeeId).FirstOrDefault(), cancellationToken);

                foreach (var task in pastIncompleteTasks)
                {
                    var prevDateStr = (task.EndDate ?? task.DueDate).HasValue ? (task.EndDate ?? task.DueDate)!.Value.ToString("MMM dd, h:mm tt") : "Previous Date";
                    if (string.IsNullOrWhiteSpace(task.DelayReason))
                    {
                        task.DelayReason = $"End time elapsed ({prevDateStr}). Pending completion/justification.";
                    }

                    if (!alreadyLoggedSet.Contains(task.Id))
                    {
                        taskEmpMap.TryGetValue(task.Id, out var empId);

                        dbContext.TaskActivityLogs.Add(new TaskActivityLog
                        {
                            TaskId = task.Id,
                            TaskName = task.TaskName,
                            EmployeeId = empId > 0 ? empId : (int?)null,
                            EmployeeName = "System Auto-Rollover",
                            ActionType = "Overdue / Delay Flagged",
                            OldStatus = task.Status.ToString(),
                            NewStatus = task.Status.ToString(),
                            Progress = task.Progress,
                            DelayReason = task.DelayReason,
                            ErrorDetails = $"Task end time passed ({prevDateStr}) and flagged for overdue follow-up on ({today:MMM dd, yyyy}).",
                            LoggedAt = DateTime.Now
                        });

                        alreadyLoggedSet.Add(task.Id);
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
