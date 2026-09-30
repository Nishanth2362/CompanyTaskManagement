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

namespace CompanyTaskManagement.Application.Features.Tasks.Queries.GetFiltered
{
    public class GetFilteredTasksQuery : IRequest<Result<List<TaskItem>>>
    {
        public string Filter { get; set; } = "all";
        public int? CurrentEmployeeId { get; set; }
    }

    internal class GetFilteredTasksQueryHandler : IRequestHandler<GetFilteredTasksQuery, Result<List<TaskItem>>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetFilteredTasksQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<TaskItem>>> Handle(GetFilteredTasksQuery request, CancellationToken cancellationToken)
        {
            var today = DateTime.Today;
            var now = DateTime.Now;

            var baseQuery = _unitOfWork.Repository<TaskItem>().Entities
                .Include(t => t.Project)
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .AsNoTracking();

            var currentFilter = string.IsNullOrWhiteSpace(request.Filter) ? "all" : request.Filter.ToLower();
            IQueryable<TaskItem> query = currentFilter switch
            {
                "today" => baseQuery.Where(t => (t.StartDate.HasValue ? t.StartDate.Value.Date == today : t.CreatedAt.Date == today)),
                "inprogress" => baseQuery.Where(t => t.Status == TaskStatus.InProgress),
                "overdue" => baseQuery.Where(t => t.Status != TaskStatus.Completed && ((t.EndDate.HasValue && t.EndDate.Value <= now) || (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) || !string.IsNullOrEmpty(t.DelayReason))),
                "completed" => baseQuery.Where(t => t.Status == TaskStatus.Completed),
                "urgent" => baseQuery.Where(t => t.Priority == TaskPriority.Urgent || t.Priority == TaskPriority.High),
                "mytasks" when request.CurrentEmployeeId.HasValue => baseQuery.Where(t => t.TaskEmployees.Any(te => te.EmployeeId == request.CurrentEmployeeId.Value)),
                _ => baseQuery
            };

            var filteredTasks = await query
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Priority)
                .ToListAsync(cancellationToken);

            return await Result<List<TaskItem>>.SuccessAsync(filteredTasks);
        }
    }
}
