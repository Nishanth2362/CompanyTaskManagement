using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Tasks.Commands.LogActivity
{
    public class LogTaskActivityCommand : IRequest<Result<int>>
    {
        public int TaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public int? EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string ActionType { get; set; } = "Update";
        public string? OldStatus { get; set; }
        public string? NewStatus { get; set; }
        public int Progress { get; set; }
        public string? DelayReason { get; set; }
        public string? ErrorDetails { get; set; }
        public string? ScreenshotPath { get; set; }
    }

    internal class LogTaskActivityCommandHandler : IRequestHandler<LogTaskActivityCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<LogTaskActivityCommandHandler> _logger;

        public LogTaskActivityCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<LogTaskActivityCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(LogTaskActivityCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var log = new TaskActivityLog
                {
                    TaskId = request.TaskId,
                    TaskName = request.TaskName,
                    EmployeeId = request.EmployeeId,
                    EmployeeName = !string.IsNullOrWhiteSpace(request.EmployeeName) ? request.EmployeeName : "System",
                    ActionType = request.ActionType,
                    OldStatus = request.OldStatus,
                    NewStatus = request.NewStatus,
                    Progress = request.Progress,
                    DelayReason = request.DelayReason,
                    ErrorDetails = request.ErrorDetails,
                    ErrorScreenshotPath = request.ScreenshotPath,
                    LoggedAt = DateTime.Now
                };

                await _unitOfWork.Repository<TaskActivityLog>().AddAsync(log);
                await _unitOfWork.Commit(cancellationToken);

                return await Result<int>.SuccessAsync(log.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging task activity for task {TaskId}: {Message}", request.TaskId, ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}
