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
using TaskStatus = CompanyTaskManagement.Domain.Enums.TaskStatus;

namespace CompanyTaskManagement.Application.Features.Tasks.Queries.GetPaged
{
    public class GetPagedTasksQuery : IRequest<PaginatedResult<TaskSummaryDto>>
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string Filter { get; set; } = "all";
        public string? Search { get; set; }
        public int? CurrentEmployeeId { get; set; }
        public bool IsEmployeeOnly { get; set; } = false;
    }

    public class TaskSummaryDto
    {
        public int Id { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TaskPriority Priority { get; set; }
        public TaskStatus Status { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Progress { get; set; }
        public string? DelayReason { get; set; }
        public string? ErrorDetails { get; set; }
        public bool IsOverdue { get; set; }
        public List<string> CompanyNames { get; set; } = new();
        public List<string> AssignedEmployeeNames { get; set; } = new();
    }

    public class TaskDashboardMetrics
    {
        public int AllCount { get; set; }
        public int TodayCount { get; set; }
        public int InProgressCount { get; set; }
        public int OverdueCount { get; set; }
        public int CompletedCount { get; set; }
        public int UrgentCount { get; set; }
        public int MyTasksCount { get; set; }
    }

    internal class GetPagedTasksQueryHandler : IRequestHandler<GetPagedTasksQuery, PaginatedResult<TaskSummaryDto>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetPagedTasksQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<PaginatedResult<TaskSummaryDto>> Handle(GetPagedTasksQuery request, CancellationToken cancellationToken)
        {
            var baseQuery = _unitOfWork.Repository<TaskItem>().Entities
                .AsNoTracking()
                .Include(t => t.Project)
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee);

            IQueryable<TaskItem> query = baseQuery;

            // Search filter
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var search = request.Search.Trim().ToLower();
                query = query.Where(t => t.TaskName.ToLower().Contains(search) || (t.Description != null && t.Description.ToLower().Contains(search)));
            }

            var today = DateTime.Today;
            var now = DateTime.Now;

            // Apply Tab filter
            query = (request.Filter?.ToLower()) switch
            {
                "today" => query.Where(t => (t.StartDate.HasValue ? t.StartDate.Value.Date == today : t.CreatedAt.Date == today)),
                "inprogress" => query.Where(t => t.Status == TaskStatus.InProgress),
                "overdue" => query.Where(t => t.Status != TaskStatus.Completed && ((t.EndDate.HasValue && t.EndDate.Value <= now) || (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) || !string.IsNullOrEmpty(t.DelayReason))),
                "completed" => query.Where(t => t.Status == TaskStatus.Completed),
                "urgent" => query.Where(t => t.Priority == TaskPriority.Urgent || t.Priority == TaskPriority.High),
                "mytasks" when request.CurrentEmployeeId.HasValue => query.Where(t => t.TaskEmployees.Any(te => te.EmployeeId == request.CurrentEmployeeId.Value)),
                _ => query
            };

            // DB-Level Count
            var totalCount = await query.CountAsync(cancellationToken);

            // DB-Level Skip/Take pagination
            var items = await query
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Priority)
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(t => new TaskSummaryDto
                {
                    Id = t.Id,
                    TaskName = t.TaskName,
                    Description = t.Description,
                    Priority = t.Priority,
                    Status = t.Status,
                    ProjectId = t.ProjectId,
                    ProjectName = t.Project != null ? t.Project.ProjectName : null,
                    StartDate = t.StartDate,
                    EndDate = t.EndDate,
                    DueDate = t.DueDate,
                    CreatedAt = t.CreatedAt,
                    Progress = t.Progress,
                    DelayReason = t.DelayReason,
                    ErrorDetails = t.ErrorDetails,
                    IsOverdue = t.Status != TaskStatus.Completed && ((t.EndDate.HasValue && t.EndDate.Value <= now) || (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) || !string.IsNullOrEmpty(t.DelayReason)),
                    CompanyNames = t.TaskCompanies.Select(tc => tc.Company.Name).ToList(),
                    AssignedEmployeeNames = t.TaskEmployees.Select(te => te.Employee.Name).ToList()
                })
                .ToListAsync(cancellationToken);

            return PaginatedResult<TaskSummaryDto>.Create(items, totalCount, request.PageNumber, request.PageSize);
        }
    }
}

