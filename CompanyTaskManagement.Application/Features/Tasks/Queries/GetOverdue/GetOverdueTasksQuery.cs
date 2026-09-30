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

namespace CompanyTaskManagement.Application.Features.Tasks.Queries.GetOverdue
{
    public class OverdueTaskPreviewDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int Progress { get; set; }
        public string DueDate { get; set; } = string.Empty;
        public string Companies { get; set; } = string.Empty;
        public string Employees { get; set; } = string.Empty;
        public string? DelayReason { get; set; }
        public string? ErrorDetails { get; set; }
        public bool HasScreenshot { get; set; }
    }

    public class GetOverdueTaskPreviewQuery : IRequest<Result<List<OverdueTaskPreviewDto>>>
    {
    }

    public class GetOverdueTasksEntitiesQuery : IRequest<Result<List<TaskItem>>>
    {
    }

    internal class GetOverdueTasksQueryHandler : 
        IRequestHandler<GetOverdueTaskPreviewQuery, Result<List<OverdueTaskPreviewDto>>>,
        IRequestHandler<GetOverdueTasksEntitiesQuery, Result<List<TaskItem>>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetOverdueTasksQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<OverdueTaskPreviewDto>>> Handle(GetOverdueTaskPreviewQuery request, CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var today = DateTime.Today;

            var overdueTasks = await _unitOfWork.Repository<TaskItem>().Entities
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .Where(t => t.Status != TaskStatus.Completed &&
                            ((t.EndDate.HasValue && t.EndDate.Value <= now) ||
                             (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) ||
                             !string.IsNullOrEmpty(t.DelayReason)))
                .AsNoTracking()
                .Select(t => new OverdueTaskPreviewDto
                {
                    Id = t.Id,
                    Name = t.TaskName,
                    Priority = t.Priority.ToString(),
                    Status = t.Status.ToString(),
                    Progress = t.Progress,
                    DueDate = t.EndDate.HasValue ? t.EndDate.Value.ToString("MMM dd, h:mm tt") : (t.DueDate.HasValue ? t.DueDate.Value.ToString("MMM dd, yyyy") : ""),
                    Companies = string.Join(", ", t.TaskCompanies.Select(tc => tc.Company.Name)),
                    Employees = string.Join(", ", t.TaskEmployees.Select(te => te.Employee.Name)),
                    DelayReason = t.DelayReason,
                    ErrorDetails = t.ErrorDetails,
                    HasScreenshot = !string.IsNullOrEmpty(t.ErrorScreenshotPath)
                })
                .ToListAsync(cancellationToken);

            return await Result<List<OverdueTaskPreviewDto>>.SuccessAsync(overdueTasks);
        }

        public async Task<Result<List<TaskItem>>> Handle(GetOverdueTasksEntitiesQuery request, CancellationToken cancellationToken)
        {
            var now = DateTime.Now;
            var today = DateTime.Today;

            var overdueTasks = await _unitOfWork.Repository<TaskItem>().Entities
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .Where(t => t.Status != TaskStatus.Completed &&
                            ((t.EndDate.HasValue && t.EndDate.Value <= now) ||
                             (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) ||
                             !string.IsNullOrEmpty(t.DelayReason)))
                .ToListAsync(cancellationToken);

            return await Result<List<TaskItem>>.SuccessAsync(overdueTasks);
        }
    }
}
