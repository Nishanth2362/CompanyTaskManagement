using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Domain.Enums;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskStatus = CompanyTaskManagement.Domain.Enums.TaskStatus;

namespace CompanyTaskManagement.Application.Features.Tasks.Commands.Sync
{
    public class SyncTaskToTeamCommand : IRequest<Result<bool>>
    {
        public int TaskId { get; set; }
        public List<int> AssignedEmployeeIds { get; set; } = new();
    }

    internal class SyncTaskToTeamCommandHandler : IRequestHandler<SyncTaskToTeamCommand, Result<bool>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<SyncTaskToTeamCommandHandler> _logger;

        public SyncTaskToTeamCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<SyncTaskToTeamCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<bool>> Handle(SyncTaskToTeamCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var task = await _unitOfWork.Repository<TaskItem>().GetByIdAsync(request.TaskId);
                if (task == null)
                {
                    return await Result<bool>.FailAsync("Task not found.");
                }

                var existingTeamTasks = await _unitOfWork.Repository<TeamTask>().Entities
                    .Where(tt => tt.Title == task.TaskName)
                    .ToListAsync(cancellationToken);

                if (existingTeamTasks.Any())
                {
                    foreach (var tt in existingTeamTasks)
                    {
                        if (task.Status == TaskStatus.Completed)
                        {
                            tt.Status = TeamTaskStatus.Completed;
                            tt.CompletedAt = DateTime.Now;
                            tt.IncompleteReason = null;
                        }
                        else if (task.Status == TaskStatus.InProgress || task.Status == TaskStatus.InReview)
                        {
                            tt.Status = TeamTaskStatus.InProgress;
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(task.DelayReason))
                            {
                                tt.Status = TeamTaskStatus.NotCompleted;
                                tt.IncompleteReason = task.DelayReason;
                            }
                            else
                            {
                                tt.Status = TeamTaskStatus.Pending;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(task.DelayReason))
                        {
                            tt.IncompleteReason = task.DelayReason;
                        }

                        tt.Priority = task.Priority;
                        tt.ProjectId = task.ProjectId ?? tt.ProjectId;

                        await _unitOfWork.Repository<TeamTask>().UpdateAsync(tt);
                    }

                    await _unitOfWork.Commit(cancellationToken);
                }
                else if (request.AssignedEmployeeIds != null && request.AssignedEmployeeIds.Any())
                {
                    foreach (var empId in request.AssignedEmployeeIds)
                    {
                        var teamMember = await _unitOfWork.Repository<TeamMember>().Entities
                            .Include(tm => tm.Team)
                            .FirstOrDefaultAsync(tm => tm.EmployeeId == empId, cancellationToken);

                        if (teamMember != null && teamMember.Team != null)
                        {
                            var teamTaskStatus = task.Status switch
                            {
                                TaskStatus.Completed => TeamTaskStatus.Completed,
                                TaskStatus.InProgress => TeamTaskStatus.InProgress,
                                TaskStatus.InReview => TeamTaskStatus.InProgress,
                                _ => !string.IsNullOrWhiteSpace(task.DelayReason) ? TeamTaskStatus.NotCompleted : TeamTaskStatus.Pending
                            };

                            var newTeamTask = new TeamTask
                            {
                                TeamId = teamMember.TeamId,
                                AssignedByLeaderId = teamMember.Team.TeamLeaderId,
                                AssignedToEmployeeId = empId,
                                Title = task.TaskName,
                                Description = task.Description ?? task.TaskName,
                                Priority = task.Priority,
                                Status = teamTaskStatus,
                                IncompleteReason = task.DelayReason,
                                ProjectId = task.ProjectId,
                                StartDate = task.StartDate ?? task.CreatedAt,
                                EndDate = task.EndDate ?? task.DueDate,
                                DueDate = task.DueDate ?? task.EndDate,
                                CreatedAt = task.CreatedAt,
                                CompletedAt = task.Status == TaskStatus.Completed ? DateTime.Now : null
                            };

                            await _unitOfWork.Repository<TeamTask>().AddAsync(newTeamTask);
                        }
                    }

                    await _unitOfWork.Commit(cancellationToken);
                }

                return await Result<bool>.SuccessAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing task {TaskId} to team tasks: {Message}", request.TaskId, ex.Message);
                return await Result<bool>.FailAsync(ex.Message);
            }
        }
    }
}
