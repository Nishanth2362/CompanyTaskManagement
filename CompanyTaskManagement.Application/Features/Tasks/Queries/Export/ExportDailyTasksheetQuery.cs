using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Domain.Enums;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TaskStatus = CompanyTaskManagement.Domain.Enums.TaskStatus;

namespace CompanyTaskManagement.Application.Features.Tasks.Queries.Export
{
    public class ExportDailyTasksheetQuery : IRequest<Result<(byte[] Data, string FileName)>>
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Filter { get; set; } = "all";
    }

    internal class ExportDailyTasksheetQueryHandler : IRequestHandler<ExportDailyTasksheetQuery, Result<(byte[] Data, string FileName)>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public ExportDailyTasksheetQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<(byte[] Data, string FileName)>> Handle(ExportDailyTasksheetQuery request, CancellationToken cancellationToken)
        {
            var today = DateTime.Today;
            IQueryable<TaskItem> baseQuery = _unitOfWork.Repository<TaskItem>().Entities
                .Include(t => t.Project)
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .AsNoTracking();

            IQueryable<TaskItem> query = baseQuery;
            var filter = request.Filter?.ToLower() ?? "all";

            if (request.StartDate.HasValue && request.EndDate.HasValue)
            {
                var start = request.StartDate.Value.Date;
                var end = request.EndDate.Value.Date;
                query = query.Where(t => (t.CreatedAt.Date >= start && t.CreatedAt.Date <= end) ||
                                         (t.DueDate.HasValue && t.DueDate.Value.Date >= start && t.DueDate.Value.Date <= end) ||
                                         (t.StartDate.HasValue && t.StartDate.Value.Date >= start && t.StartDate.Value.Date <= end));
            }
            else if (request.StartDate.HasValue)
            {
                var start = request.StartDate.Value.Date;
                query = query.Where(t => t.CreatedAt.Date >= start ||
                                         (t.DueDate.HasValue && t.DueDate.Value.Date >= start) ||
                                         (t.StartDate.HasValue && t.StartDate.Value.Date >= start));
            }
            else if (request.EndDate.HasValue)
            {
                var end = request.EndDate.Value.Date;
                query = query.Where(t => t.CreatedAt.Date <= end ||
                                         (t.DueDate.HasValue && t.DueDate.Value.Date <= end) ||
                                         (t.StartDate.HasValue && t.StartDate.Value.Date <= end));
            }

            if (filter == "today")
            {
                query = query.Where(t => !t.DueDate.HasValue || t.DueDate.Value.Date == today || t.CreatedAt.Date == today);
            }
            else if (filter == "inprogress")
            {
                query = query.Where(t => t.Status == TaskStatus.InProgress);
            }
            else if (filter == "overdue")
            {
                var now = DateTime.Now;
                query = query.Where(t => t.Status != TaskStatus.Completed &&
                                         ((t.EndDate.HasValue && t.EndDate.Value <= now) ||
                                          (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) ||
                                          !string.IsNullOrEmpty(t.DelayReason)));
            }
            else if (filter == "completed")
            {
                query = query.Where(t => t.Status == TaskStatus.Completed);
            }
            else if (filter == "urgent")
            {
                query = query.Where(t => t.Priority == TaskPriority.Urgent || t.Priority == TaskPriority.High);
            }

            var tasks = await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Priority).ToListAsync(cancellationToken);

            var builder = new StringBuilder();
            builder.Append('\uFEFF'); // UTF-8 BOM
            builder.AppendLine("Task ID,Date,Task Name,Description,Priority,Status,Progress (%),Start Time,End Time,Total Hours,Companies,Assigned Employees,Delay / Overdue Reason,Error Details");

            foreach (var t in tasks)
            {
                var id = t.Id;
                var dateStr = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd") : t.CreatedAt.ToString("yyyy-MM-dd");
                var name = $"\"{(t.TaskName ?? "").Replace("\"", "\"\"")}\"";
                var desc = $"\"{(t.Description ?? "").Replace("\"", "\"\"")}\"";
                var priority = t.Priority.ToString();
                var status = t.Status.ToString();
                var progress = t.Progress;
                var startTime = t.StartDate.HasValue ? t.StartDate.Value.ToString("hh:mm tt") : "-";
                var endTime = t.EndDate.HasValue ? t.EndDate.Value.ToString("hh:mm tt") : "-";

                string totalHours = "-";
                if (t.StartDate.HasValue && t.EndDate.HasValue && t.EndDate.Value >= t.StartDate.Value)
                {
                    var span = t.EndDate.Value - t.StartDate.Value;
                    totalHours = $"{span.Hours}h {span.Minutes}m";
                }

                var companies = $"\"{string.Join(", ", t.TaskCompanies.Select(c => c.Company?.Name).Where(n => !string.IsNullOrEmpty(n))).Replace("\"", "\"\"")}\"";
                var employees = $"\"{string.Join(", ", t.TaskEmployees.Select(e => e.Employee?.Name).Where(n => !string.IsNullOrEmpty(n))).Replace("\"", "\"\"")}\"";
                var delayReason = $"\"{(t.DelayReason ?? "").Replace("\"", "\"\"")}\"";
                var errorDetails = $"\"{(t.ErrorDetails ?? "").Replace("\"", "\"\"")}\"";

                builder.AppendLine($"{id},{dateStr},{name},{desc},{priority},{status},{progress}%,{startTime},{endTime},{totalHours},{companies},{employees},{delayReason},{errorDetails}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(builder.ToString());
            string fileName = (request.StartDate.HasValue && request.EndDate.HasValue)
                ? $"Auxinzio_Tasksheet_{request.StartDate.Value:yyyyMMdd}_to_{request.EndDate.Value:yyyyMMdd}.csv"
                : $"Auxinzio_Total_Tasksheet_{DateTime.Today:yyyy-MM-dd}.csv";

            return await Result<(byte[] Data, string FileName)>.SuccessAsync((buffer, fileName));
        }
    }
}
