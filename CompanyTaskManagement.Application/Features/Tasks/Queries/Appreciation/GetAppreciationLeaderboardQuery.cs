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

namespace CompanyTaskManagement.Application.Features.Tasks.Queries.Appreciation
{
    public class EmployeeAppreciationDto
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int TotalAssignedTasks { get; set; }
        public int CompletedTasksCount { get; set; }
        public int PerfectTasksCount { get; set; }
        public double CompletionRate { get; set; }
        public double PerfectRate { get; set; }
        public List<string> AssignedCompanies { get; set; } = new();
        public List<string> PerfectlyCompletedTasks { get; set; } = new();
    }

    public class MotivationalQuoteDto
    {
        public string Quote { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Category { get; set; } = "Growth & Resilience";
        public string Icon { get; set; } = "bi-lightbulb-fill";
    }

    public class WeeklyAppreciationDataDto
    {
        public List<EmployeeAppreciationDto> Leaderboard { get; set; } = new();
        public int TotalSystemTasks { get; set; }
        public int TotalCompletedTasks { get; set; }
        public int TotalPerfectTasks { get; set; }
        public double OverallSystemPerfectionRate { get; set; }
        public List<TaskActivityLog> AllActivityLogs { get; set; } = new();
        public List<MotivationalQuoteDto> DailyInspirations { get; set; } = new();
    }

    public class GetAppreciationLeaderboardQuery : IRequest<Result<WeeklyAppreciationDataDto>>
    {
    }

    internal class GetAppreciationLeaderboardQueryHandler : IRequestHandler<GetAppreciationLeaderboardQuery, Result<WeeklyAppreciationDataDto>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetAppreciationLeaderboardQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<WeeklyAppreciationDataDto>> Handle(GetAppreciationLeaderboardQuery request, CancellationToken cancellationToken)
        {
            // Ultra-optimized projection: loads ONLY necessary scalar columns directly via SQL join
            var employeesData = await _unitOfWork.Repository<Employee>().Entities
                .Where(e => e.IsActive)
                .AsNoTracking()
                .Select(e => new
                {
                    e.Id,
                    e.Name,
                    Tasks = e.TaskEmployees
                        .Where(te => te.Task != null)
                        .Select(te => new
                        {
                            te.Task.Status,
                            te.Task.Progress,
                            te.Task.DelayReason,
                            te.Task.TaskName,
                            Companies = te.Task.TaskCompanies.Select(tc => tc.Company.Name)
                        })
                })
                .ToListAsync(cancellationToken);

            var leaderboard = new List<EmployeeAppreciationDto>(employeesData.Count);

            foreach (var emp in employeesData)
            {
                int totalAssigned = 0;
                int completedCount = 0;
                var perfectTasks = new List<string>();
                var companiesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var t in emp.Tasks)
                {
                    totalAssigned++;
                    if (t.Status == TaskStatus.Completed)
                    {
                        completedCount++;
                        if (t.Progress >= 100 && string.IsNullOrWhiteSpace(t.DelayReason))
                        {
                            perfectTasks.Add(t.TaskName);
                        }
                    }
                    foreach (var c in t.Companies)
                    {
                        if (!string.IsNullOrEmpty(c))
                        {
                            companiesSet.Add(c);
                        }
                    }
                }

                int perfectCount = perfectTasks.Count;
                double completionRate = totalAssigned > 0 ? Math.Round((double)completedCount / totalAssigned * 100, 1) : 0;
                double perfectRate = totalAssigned > 0 ? Math.Round((double)perfectCount / totalAssigned * 100, 1) : 0;

                leaderboard.Add(new EmployeeAppreciationDto
                {
                    EmployeeId = emp.Id,
                    EmployeeName = emp.Name,
                    TotalAssignedTasks = totalAssigned,
                    CompletedTasksCount = completedCount,
                    PerfectTasksCount = perfectCount,
                    CompletionRate = completionRate,
                    PerfectRate = perfectRate,
                    AssignedCompanies = companiesSet.ToList(),
                    PerfectlyCompletedTasks = perfectTasks
                });
            }

            leaderboard = leaderboard
                .OrderByDescending(e => e.PerfectTasksCount)
                .ThenByDescending(e => e.CompletedTasksCount)
                .ThenByDescending(e => e.CompletionRate)
                .ToList();

            int totalSystemTasks = leaderboard.Sum(x => x.TotalAssignedTasks);
            int totalCompletedTasks = leaderboard.Sum(x => x.CompletedTasksCount);
            int totalPerfectTasks = leaderboard.Sum(x => x.PerfectTasksCount);
            double perfectionRate = totalCompletedTasks > 0
                ? Math.Round((double)totalPerfectTasks / totalCompletedTasks * 100, 1)
                : 0;

            // Fetch top 25 recent activity events (default view page size is 5, max 25)
            var activityLogs = await _unitOfWork.Repository<TaskActivityLog>().Entities
                .AsNoTracking()
                .OrderByDescending(l => l.LoggedAt)
                .Take(25)
                .ToListAsync(cancellationToken);

            var quotes = new List<MotivationalQuoteDto>
            {
                new()
                {
                    Quote = "Quality is not an act, it is a habit. What we build today shapes our tomorrow.",
                    Author = "Aristotle",
                    Category = "Craftsmanship",
                    Icon = "bi-award-fill"
                },
                new()
                {
                    Quote = "It always seems impossible until it's done. Resilience transforms constraints into triumphs.",
                    Author = "Nelson Mandela",
                    Category = "Resilience",
                    Icon = "bi-lightning-charge-fill"
                },
                new()
                {
                    Quote = "Great things in business are never done by one person. They're done by a team of people.",
                    Author = "Steve Jobs",
                    Category = "Team Synergy",
                    Icon = "bi-people-fill"
                }
            };

            var data = new WeeklyAppreciationDataDto
            {
                Leaderboard = leaderboard,
                TotalSystemTasks = totalSystemTasks,
                TotalCompletedTasks = totalCompletedTasks,
                TotalPerfectTasks = totalPerfectTasks,
                OverallSystemPerfectionRate = perfectionRate,
                AllActivityLogs = activityLogs,
                DailyInspirations = quotes
            };

            return await Result<WeeklyAppreciationDataDto>.SuccessAsync(data);
        }
    }
}
