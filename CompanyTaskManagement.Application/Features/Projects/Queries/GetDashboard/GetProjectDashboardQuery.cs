using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskStatus = CompanyTaskManagement.Domain.Enums.TaskStatus;

namespace CompanyTaskManagement.Application.Features.Projects.Queries.GetDashboard
{
    public class ProjectDashboardMetrics
    {
        public int TotalProjects { get; set; }
        public int InProgressCount { get; set; }
        public int PlanningCount { get; set; }
        public int CompletedCount { get; set; }
        public int OnHoldCount { get; set; }
        public decimal TotalBudget { get; set; }
        public Dictionary<int, (int Total, int Completed)> TaskStats { get; set; } = new();
    }

    public class GetProjectDashboardQuery : IRequest<Result<(List<Project> Projects, ProjectDashboardMetrics Metrics)>>
    {
        public string Status { get; set; } = "all";
        public string Search { get; set; } = string.Empty;
    }

    internal class GetProjectDashboardQueryHandler : IRequestHandler<GetProjectDashboardQuery, Result<(List<Project> Projects, ProjectDashboardMetrics Metrics)>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetProjectDashboardQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<(List<Project> Projects, ProjectDashboardMetrics Metrics)>> Handle(GetProjectDashboardQuery request, CancellationToken cancellationToken)
        {
            var baseQuery = _unitOfWork.Repository<Project>().Entities.AsNoTracking();

            var query = baseQuery;
            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                query = query.Where(p => p.ProjectName.ToLower().Contains(s) ||
                                         p.ClientCompany.ToLower().Contains(s) ||
                                         p.Description.ToLower().Contains(s) ||
                                         p.LeadManagerName.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(request.Status) && request.Status.ToLower() != "all")
            {
                query = query.Where(p => p.Status.ToLower() == request.Status.ToLower());
            }

            var projects = await query.OrderByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
            var allProjects = await baseQuery.ToListAsync(cancellationToken);

            var taskStats = await _unitOfWork.Repository<TaskItem>().Entities
                .AsNoTracking()
                .Where(t => t.ProjectId.HasValue)
                .GroupBy(t => t.ProjectId!.Value)
                .Select(g => new { ProjectId = g.Key, Total = g.Count(), Completed = g.Count(t => t.Status == TaskStatus.Completed) })
                .ToDictionaryAsync(g => g.ProjectId, g => (Total: g.Total, Completed: g.Completed), cancellationToken);

            var metrics = new ProjectDashboardMetrics
            {
                TotalProjects = allProjects.Count,
                InProgressCount = allProjects.Count(p => p.Status == "In Progress"),
                PlanningCount = allProjects.Count(p => p.Status == "Planning"),
                CompletedCount = allProjects.Count(p => p.Status == "Completed"),
                OnHoldCount = allProjects.Count(p => p.Status == "On Hold"),
                TotalBudget = allProjects.Sum(p => p.Budget ?? 0),
                TaskStats = taskStats
            };

            return await Result<(List<Project> Projects, ProjectDashboardMetrics Metrics)>.SuccessAsync((projects, metrics));
        }
    }
}
