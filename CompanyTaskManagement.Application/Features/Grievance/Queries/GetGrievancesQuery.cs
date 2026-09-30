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

namespace CompanyTaskManagement.Application.Features.Grievance.Queries
{
    public class GrievancesDashboardData
    {
        public List<HrComplaint> Complaints { get; set; } = new();
        public int AllCount { get; set; }
        public int SubmittedCount { get; set; }
        public int InReviewCount { get; set; }
        public int ResolvedCount { get; set; }
        public int UrgentCount { get; set; }
    }

    public class GetGrievancesQuery : IRequest<Result<GrievancesDashboardData>>
    {
        public string Filter { get; set; } = "all";
        public string? Search { get; set; }
        public string? Category { get; set; }
    }

    public class GetGrievanceByIdQuery : IRequest<Result<HrComplaint>>
    {
        public int Id { get; set; }

        public GetGrievanceByIdQuery(int id)
        {
            Id = id;
        }
    }

    internal class GrievanceQueriesHandler :
        IRequestHandler<GetGrievancesQuery, Result<GrievancesDashboardData>>,
        IRequestHandler<GetGrievanceByIdQuery, Result<HrComplaint>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GrievanceQueriesHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<GrievancesDashboardData>> Handle(GetGrievancesQuery request, CancellationToken cancellationToken)
        {
            var query = _unitOfWork.Repository<HrComplaint>().Entities
                .Include(c => c.Employee)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.Category))
            {
                query = query.Where(c => c.Category == request.Category);
            }

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.ToLower().Trim();
                query = query.Where(c => c.Subject.ToLower().Contains(s)
                                      || c.TicketNumber.ToLower().Contains(s)
                                      || (c.SubmitterName != null && c.SubmitterName.ToLower().Contains(s))
                                      || c.Description.ToLower().Contains(s));
            }

            var allList = await query.ToListAsync(cancellationToken);

            int allCount = allList.Count;
            int submittedCount = allList.Count(c => c.Status == "Submitted");
            int inReviewCount = allList.Count(c => c.Status == "Under Review" || c.Status == "In Investigation");
            int resolvedCount = allList.Count(c => c.Status == "Resolved");
            int urgentCount = allList.Count(c => c.Priority == "Urgent" && c.Status != "Resolved" && c.Status != "Dismissed");

            List<HrComplaint> filtered;
            switch (request.Filter.ToLower())
            {
                case "submitted":
                    filtered = allList.Where(c => c.Status == "Submitted").OrderByDescending(c => c.CreatedAt).ToList();
                    break;
                case "inreview":
                    filtered = allList.Where(c => c.Status == "Under Review" || c.Status == "In Investigation").OrderByDescending(c => c.CreatedAt).ToList();
                    break;
                case "resolved":
                    filtered = allList.Where(c => c.Status == "Resolved" || c.Status == "Dismissed").OrderByDescending(c => c.ResolvedAt ?? c.CreatedAt).ToList();
                    break;
                case "urgent":
                    filtered = allList.Where(c => c.Priority == "Urgent").OrderByDescending(c => c.CreatedAt).ToList();
                    break;
                default:
                    filtered = allList.OrderByDescending(c => c.CreatedAt).ToList();
                    break;
            }

            var data = new GrievancesDashboardData
            {
                Complaints = filtered,
                AllCount = allCount,
                SubmittedCount = submittedCount,
                InReviewCount = inReviewCount,
                ResolvedCount = resolvedCount,
                UrgentCount = urgentCount
            };

            return await Result<GrievancesDashboardData>.SuccessAsync(data);
        }

        public async Task<Result<HrComplaint>> Handle(GetGrievanceByIdQuery request, CancellationToken cancellationToken)
        {
            var complaint = await _unitOfWork.Repository<HrComplaint>().Entities
                .Include(c => c.Employee)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

            if (complaint == null)
            {
                return await Result<HrComplaint>.FailAsync("Grievance case not found.");
            }

            return await Result<HrComplaint>.SuccessAsync(complaint);
        }
    }
}
