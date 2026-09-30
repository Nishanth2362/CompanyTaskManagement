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
using TaskStatus = CompanyTaskManagement.Domain.Enums.TaskStatus;

namespace CompanyTaskManagement.Application.Features.Tasks.Queries.GetPaged
{
    public class GetTaskMetricsQuery : IRequest<Result<TaskDashboardMetrics>>
    {
        public int? CurrentEmployeeId { get; set; }
    }

    internal class GetTaskMetricsQueryHandler : IRequestHandler<GetTaskMetricsQuery, Result<TaskDashboardMetrics>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetTaskMetricsQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<TaskDashboardMetrics>> Handle(GetTaskMetricsQuery request, CancellationToken cancellationToken)
        {
            var today = DateTime.Today;
            var now = DateTime.Now;
            var empId = request.CurrentEmployeeId ?? -1;

            var tasks = _unitOfWork.Repository<TaskItem>().Entities.AsNoTracking();

            // Fetch lightweight projection in 1 single round-trip instead of 7 sequential queries
            var taskData = await tasks
                .Select(t => new
                {
                    t.Status,
                    t.Priority,
                    t.StartDate,
                    t.CreatedAt,
                    t.EndDate,
                    t.DueDate,
                    t.DelayReason,
                    IsMyTask = empId > 0 && t.TaskEmployees.Any(te => te.EmployeeId == empId)
                })
                .ToListAsync(cancellationToken);

            var metrics = new TaskDashboardMetrics
            {
                AllCount = taskData.Count,
                TodayCount = taskData.Count(t => (t.StartDate.HasValue ? t.StartDate.Value.Date == today : t.CreatedAt.Date == today)),
                InProgressCount = taskData.Count(t => t.Status == TaskStatus.InProgress),
                OverdueCount = taskData.Count(t => t.Status != TaskStatus.Completed && ((t.EndDate.HasValue && t.EndDate.Value <= now) || (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) || !string.IsNullOrEmpty(t.DelayReason))),
                CompletedCount = taskData.Count(t => t.Status == TaskStatus.Completed),
                UrgentCount = taskData.Count(t => t.Priority == TaskPriority.Urgent || t.Priority == TaskPriority.High),
                MyTasksCount = taskData.Count(t => t.IsMyTask)
            };

            return await Result<TaskDashboardMetrics>.SuccessAsync(metrics);
        }
    }
}

