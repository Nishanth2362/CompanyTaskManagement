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

namespace CompanyTaskManagement.Application.Features.Teams.Queries.GetTeamsDashboard
{
    public class TeamsDashboardData
    {
        public List<Team> Teams { get; set; } = new();
        public List<Employee> AllEmployees { get; set; } = new();
        public List<Project> AllProjects { get; set; } = new();
        public List<TeamLeaderReview> AllReviews { get; set; } = new();
        public List<TeamTask> IncompleteTasks { get; set; } = new();
        public int TotalTeams { get; set; }
        public int TotalLeaders { get; set; }
        public int TotalMembers { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class GetTeamsDashboardQuery : IRequest<Result<TeamsDashboardData>>
    {
        public string Search { get; set; } = string.Empty;
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }

    internal class GetTeamsDashboardQueryHandler : IRequestHandler<GetTeamsDashboardQuery, Result<TeamsDashboardData>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetTeamsDashboardQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<TeamsDashboardData>> Handle(GetTeamsDashboardQuery request, CancellationToken cancellationToken)
        {
            var teamsQuery = _unitOfWork.Repository<Team>().Entities
                .Include(t => t.TeamLeader)
                .Include(t => t.Members)
                    .ThenInclude(m => m.Employee)
                .Include(t => t.TeamTasks)
                    .ThenInclude(tk => tk.AssignedToEmployee)
                .Include(t => t.TeamTasks)
                    .ThenInclude(tk => tk.Project)
                .AsNoTracking();

            var countQuery = _unitOfWork.Repository<Team>().Entities.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                teamsQuery = teamsQuery.Where(t => t.Name.ToLower().Contains(s) ||
                                                   (t.Description != null && t.Description.ToLower().Contains(s)) ||
                                                   (t.TeamLeader != null && t.TeamLeader.Name.ToLower().Contains(s)));

                countQuery = countQuery.Where(t => t.Name.ToLower().Contains(s) ||
                                                   (t.Description != null && t.Description.ToLower().Contains(s)) ||
                                                   (t.TeamLeader != null && t.TeamLeader.Name.ToLower().Contains(s)));
            }

            var totalTeamsCount = await countQuery.CountAsync(cancellationToken);
            var teams = await teamsQuery
                .OrderBy(t => t.Name)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .ToListAsync(cancellationToken);

            var allEmployees = await _unitOfWork.Repository<Employee>().Entities
                .Where(e => e.IsActive)
                .OrderBy(e => e.Name)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var cutoff12h = DateTime.UtcNow.AddHours(-12);
            var allReviews = await _unitOfWork.Repository<TeamLeaderReview>().Entities
                .Include(r => r.TeamLeader)
                .Include(r => r.Employee)
                .Where(r => r.CreatedAt >= cutoff12h)
                .OrderByDescending(r => r.CreatedAt)
                .AsNoTracking()
                .Take(30)
                .ToListAsync(cancellationToken);

            var allProjects = await _unitOfWork.Repository<Project>().Entities
                .OrderBy(p => p.ProjectName)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var totalLeaders = await _unitOfWork.Repository<Team>().Entities
                .Where(t => t.TeamLeaderId.HasValue)
                .Select(t => t.TeamLeaderId)
                .Distinct()
                .CountAsync(cancellationToken);

            var totalMembers = await _unitOfWork.Repository<TeamMember>().Entities
                .Select(m => m.EmployeeId)
                .Distinct()
                .CountAsync(cancellationToken);

            var totalTasks = await _unitOfWork.Repository<TeamTask>().Entities.CountAsync(cancellationToken);
            var completedTasks = await _unitOfWork.Repository<TeamTask>().Entities
                .CountAsync(t => t.Status == TeamTaskStatus.Completed, cancellationToken);

            var now = DateTime.Now;
            var incompleteTasksList = await _unitOfWork.Repository<TeamTask>().Entities
                .Include(t => t.AssignedToEmployee)
                .Include(t => t.Project)
                .Where(t => t.Status == TeamTaskStatus.NotCompleted ||
                            (t.Status != TeamTaskStatus.Completed &&
                             ((t.EndDate.HasValue && t.EndDate.Value <= now) ||
                              (t.DueDate.HasValue && t.DueDate.Value <= now))))
                .OrderByDescending(t => t.EndDate ?? t.DueDate)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var data = new TeamsDashboardData
            {
                Teams = teams,
                AllEmployees = allEmployees,
                AllProjects = allProjects,
                AllReviews = allReviews,
                IncompleteTasks = incompleteTasksList,
                TotalTeams = totalTeamsCount,
                TotalLeaders = totalLeaders,
                TotalMembers = totalMembers,
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                CurrentPage = request.Page,
                PageSize = request.PageSize,
                TotalPages = (int)Math.Ceiling(totalTeamsCount / (double)request.PageSize)
            };

            return await Result<TeamsDashboardData>.SuccessAsync(data);
        }
    }
}
