using System;
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

namespace CompanyTaskManagement.Application.Features.Tasks.Commands.QuickStatus
{
    public class QuickUpdateTaskStatusCommand : IRequest<Result<int>>
    {
        public int TaskId { get; set; }
        public TaskStatus Status { get; set; }
        public int? Progress { get; set; }
        public int? CurrentEmployeeId { get; set; }
        public string? CurrentEmployeeName { get; set; }
    }

    internal class QuickUpdateTaskStatusCommandHandler : IRequestHandler<QuickUpdateTaskStatusCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<QuickUpdateTaskStatusCommandHandler> _logger;

        public QuickUpdateTaskStatusCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<QuickUpdateTaskStatusCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(QuickUpdateTaskStatusCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var task = await _unitOfWork.Repository<TaskItem>().Entities
                    .Include(t => t.TaskEmployees)
                    .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

                if (task == null)
                {
                    return await Result<int>.FailAsync("Task not found.");
                }

                var oldStatus = task.Status.ToString();
                task.Status = request.Status;

                if (request.Status == TaskStatus.Completed)
                {
                    task.Progress = 100;
                    task.DelayReason = null;
                }
                else if (request.Progress.HasValue)
                {
                    task.Progress = Math.Clamp(request.Progress.Value, 0, 100);
                }

                var actorName = !string.IsNullOrWhiteSpace(request.CurrentEmployeeName) 
                    ? request.CurrentEmployeeName 
                    : "User";

                var log = new TaskActivityLog
                {
                    TaskId = task.Id,
                    TaskName = task.TaskName,
                    EmployeeId = request.CurrentEmployeeId,
                    EmployeeName = actorName,
                    ActionType = "Quick Status Update",
                    OldStatus = oldStatus,
                    NewStatus = task.Status.ToString(),
                    Progress = task.Progress,
                    DelayReason = task.DelayReason,
                    ErrorDetails = task.ErrorDetails,
                    LoggedAt = DateTime.Now
                };

                await _unitOfWork.Repository<TaskActivityLog>().AddAsync(log);
                await _unitOfWork.Repository<TaskItem>().UpdateAsync(task);
                await _unitOfWork.CommitAndRemoveCache(cancellationToken, "tasks-cache");

                _logger.LogInformation("Task {TaskId} status updated to {Status} by {User}", task.Id, task.Status, actorName);
                return await Result<int>.SuccessAsync(task.Id, $"Task status updated to {task.Status}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for task {TaskId}: {Message}", request.TaskId, ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}
