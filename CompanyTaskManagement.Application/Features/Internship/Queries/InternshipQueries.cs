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

namespace CompanyTaskManagement.Application.Features.Internship.Queries
{
    public class InternshipDashboardData
    {
        public List<InternStudyMaterial> Materials { get; set; } = new();
        public List<InternDoubt> Doubts { get; set; } = new();
        public List<InternshipMember> Members { get; set; } = new();
        public List<InternYouTubeReference> YouTubeVideos { get; set; } = new();
        public List<InternTestResult> TestResults { get; set; } = new();
        public List<Employee> Employees { get; set; } = new();

        public int TotalMaterials { get; set; }
        public int TotalDoubts { get; set; }
        public int OpenDoubts { get; set; }
        public int ResolvedDoubts { get; set; }
        public int TotalClarifications { get; set; }
        public int TotalMembers { get; set; }
        public int TotalInterns { get; set; }
        public int TotalMentors { get; set; }
        public int TotalYouTubeVideos { get; set; }
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public double AverageScore { get; set; }
    }

    public class GetInternshipDashboardQuery : IRequest<Result<InternshipDashboardData>>
    {
        public string? Track { get; set; }
        public string? DoubtStatus { get; set; }
        public string? Search { get; set; }
    }

    public class GetDoubtDetailsQuery : IRequest<Result<InternDoubt>>
    {
        public int Id { get; set; }

        public GetDoubtDetailsQuery(int id)
        {
            Id = id;
        }
    }

    internal class InternshipQueriesHandler :
        IRequestHandler<GetInternshipDashboardQuery, Result<InternshipDashboardData>>,
        IRequestHandler<GetDoubtDetailsQuery, Result<InternDoubt>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public InternshipQueriesHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<InternshipDashboardData>> Handle(GetInternshipDashboardQuery request, CancellationToken cancellationToken)
        {
            var search = request.Search?.ToLower().Trim();

            // 1. Materials
            var materialsQuery = _unitOfWork.Repository<InternStudyMaterial>().Entities.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.Track) && request.Track != "all")
            {
                materialsQuery = materialsQuery.Where(m => m.Track == request.Track);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                materialsQuery = materialsQuery.Where(m => m.Title.ToLower().Contains(search) || m.Description.ToLower().Contains(search) || (m.Tags != null && m.Tags.ToLower().Contains(search)));
            }
            var materials = await materialsQuery.OrderBy(m => m.Track).ThenBy(m => m.Difficulty).ToListAsync(cancellationToken);

            // 2. Doubts
            var doubtsQuery = _unitOfWork.Repository<InternDoubt>().Entities
                .Include(d => d.Clarifications)
                .Include(d => d.InternEmployee)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(request.DoubtStatus) && request.DoubtStatus != "all")
            {
                doubtsQuery = doubtsQuery.Where(d => d.Status == request.DoubtStatus);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                doubtsQuery = doubtsQuery.Where(d => d.Title.ToLower().Contains(search) || d.Description.ToLower().Contains(search) || d.InternName.ToLower().Contains(search));
            }
            var doubts = await doubtsQuery.OrderByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);

            // 3. Members
            var membersQuery = _unitOfWork.Repository<InternshipMember>().Entities.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                membersQuery = membersQuery.Where(m => m.Name.ToLower().Contains(search) || m.Domain.ToLower().Contains(search) || m.Role.ToLower().Contains(search));
            }
            var members = await membersQuery.OrderBy(m => m.Role).ThenBy(m => m.Name).ToListAsync(cancellationToken);

            // 4. YouTube Videos
            var youTubeQuery = _unitOfWork.Repository<InternYouTubeReference>().Entities.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.Track) && request.Track != "all")
            {
                youTubeQuery = youTubeQuery.Where(y => y.Track == request.Track);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                youTubeQuery = youTubeQuery.Where(y => y.Title.ToLower().Contains(search) ||
                                                       y.Description.ToLower().Contains(search) ||
                                                       (y.ChannelOrMentorName != null && y.ChannelOrMentorName.ToLower().Contains(search)) ||
                                                       (y.Tags != null && y.Tags.ToLower().Contains(search)));
            }
            var youTubeVideos = await youTubeQuery.OrderByDescending(y => y.CreatedAt).ToListAsync(cancellationToken);

            // 5. Test Results
            var testsQuery = _unitOfWork.Repository<InternTestResult>().Entities.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(request.Track) && request.Track != "all")
            {
                testsQuery = testsQuery.Where(t => t.Track == request.Track);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                testsQuery = testsQuery.Where(t => t.InternName.ToLower().Contains(search) ||
                                                   t.TopicTitle.ToLower().Contains(search) ||
                                                   t.Track.ToLower().Contains(search) ||
                                                   (t.FeedbackNotes != null && t.FeedbackNotes.ToLower().Contains(search)));
            }
            var testResults = await testsQuery.OrderByDescending(t => t.TakenAt).ToListAsync(cancellationToken);

            // 6. Employees
            var employees = await _unitOfWork.Repository<Employee>().Entities
                .OrderBy(e => e.Name)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var totalMaterials = await _unitOfWork.Repository<InternStudyMaterial>().Entities.CountAsync(cancellationToken);
            var totalClarifications = await _unitOfWork.Repository<InternDoubtClarification>().Entities.CountAsync(cancellationToken);
            var totalYouTube = await _unitOfWork.Repository<InternYouTubeReference>().Entities.CountAsync(cancellationToken);
            var totalTests = await _unitOfWork.Repository<InternTestResult>().Entities.CountAsync(cancellationToken);

            var data = new InternshipDashboardData
            {
                Materials = materials,
                Doubts = doubts,
                Members = members,
                YouTubeVideos = youTubeVideos,
                TestResults = testResults,
                Employees = employees,
                TotalMaterials = totalMaterials,
                TotalDoubts = doubts.Count,
                OpenDoubts = doubts.Count(d => d.Status == "Open"),
                ResolvedDoubts = doubts.Count(d => d.Status == "Resolved" || d.Clarifications.Any(c => c.IsAcceptedSolution)),
                TotalClarifications = totalClarifications,
                TotalMembers = members.Count,
                TotalInterns = members.Count(m => m.Role == "Intern" || m.Role == "Graduate Trainee"),
                TotalMentors = members.Count(m => m.Role == "Technical Mentor" || m.Role == "Senior Lead"),
                TotalYouTubeVideos = totalYouTube,
                TotalTests = totalTests,
                PassedTests = testResults.Count(t => t.IsPassed),
                AverageScore = testResults.Any() ? Math.Round(testResults.Average(t => t.ScorePercentage), 1) : 0
            };

            return await Result<InternshipDashboardData>.SuccessAsync(data);
        }

        public async Task<Result<InternDoubt>> Handle(GetDoubtDetailsQuery request, CancellationToken cancellationToken)
        {
            var doubt = await _unitOfWork.Repository<InternDoubt>().Entities
                .Include(d => d.Clarifications)
                .Include(d => d.InternEmployee)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

            if (doubt == null)
            {
                return await Result<InternDoubt>.FailAsync("Doubt not found.");
            }

            return await Result<InternDoubt>.SuccessAsync(doubt);
        }
    }
}
