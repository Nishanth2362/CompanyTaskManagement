using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Domain.Enums;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Tasks.Commands.QuickError
{
    public class QuickUploadErrorLogCommand : IRequest<Result<int>>
    {
        public int TaskId { get; set; }
        public string? DelayReason { get; set; }
        public string? ErrorDetails { get; set; }
        public string? ScreenshotPath { get; set; }
        public int? CurrentEmployeeId { get; set; }
        public string? CurrentEmployeeName { get; set; }
    }

    internal class QuickUploadErrorLogCommandHandler : IRequestHandler<QuickUploadErrorLogCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<QuickUploadErrorLogCommandHandler> _logger;

        public QuickUploadErrorLogCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<QuickUploadErrorLogCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(QuickUploadErrorLogCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var task = await _unitOfWork.Repository<TaskItem>().Entities
                    .FirstOrDefaultAsync(t => t.Id == request.TaskId, cancellationToken);

                if (task == null)
                {
                    return await Result<int>.FailAsync("Task not found.");
                }

                if (!string.IsNullOrWhiteSpace(request.DelayReason))
                {
                    task.DelayReason = request.DelayReason.Trim();
                }

                if (!string.IsNullOrWhiteSpace(request.ErrorDetails))
                {
                    task.ErrorDetails = request.ErrorDetails.Trim();
                }

                if (!string.IsNullOrWhiteSpace(request.ScreenshotPath))
                {
                    task.ErrorScreenshotPath = request.ScreenshotPath.Trim();
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
                    ActionType = "Error / Delay Report",
                    OldStatus = task.Status.ToString(),
                    NewStatus = task.Status.ToString(),
                    Progress = task.Progress,
                    DelayReason = task.DelayReason,
                    ErrorDetails = task.ErrorDetails,
                    ErrorScreenshotPath = task.ErrorScreenshotPath,
                    LoggedAt = DateTime.Now
                };

                await _unitOfWork.Repository<TaskActivityLog>().AddAsync(log);
                await _unitOfWork.Repository<TaskItem>().UpdateAsync(task);
                await _unitOfWork.CommitAndRemoveCache(cancellationToken, "tasks-cache");

                _logger.LogInformation("Error log recorded for Task {TaskId} by {User}", task.Id, actorName);
                return await Result<int>.SuccessAsync(task.Id, "Task error log updated successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording error log for task {TaskId}: {Message}", request.TaskId, ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}
