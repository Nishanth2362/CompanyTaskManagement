using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Internship.Commands
{
    // 1. Ask Doubt
    public class AskDoubtCommand : IRequest<Result<int>>
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string InternName { get; set; } = string.Empty;
        public string? InternEmail { get; set; }
        public int? InternEmployeeId { get; set; }
        public string Track { get; set; } = "Backend .NET / C#";
        public string? Tags { get; set; }
        public string? ScreenshotPath { get; set; }
    }

    // 2. Add Clarification
    public class AddClarificationCommand : IRequest<Result<int>>
    {
        public int DoubtId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public int? EmployeeId { get; set; }
        public string ClarificationText { get; set; } = string.Empty;
        public string? CodeSolution { get; set; }
        public string? HelpfulLink { get; set; }
    }

    // 3. Accept Clarification
    public class AcceptClarificationCommand : IRequest<Result<int>>
    {
        public int DoubtId { get; set; }
        public int ClarificationId { get; set; }
    }

    // 4. Upvote Doubt
    public class UpvoteDoubtCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    // 5. Add / Delete Material
    public class AddStudyMaterialCommand : IRequest<Result<int>>
    {
        public InternStudyMaterial Material { get; set; } = null!;
    }

    public class DeleteStudyMaterialCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    // 6. YouTube References
    public class AddYouTubeReferenceCommand : IRequest<Result<int>>
    {
        public InternYouTubeReference Reference { get; set; } = null!;
    }

    public class DeleteYouTubeReferenceCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    // 7. Members
    public class AddInternshipMemberCommand : IRequest<Result<int>>
    {
        public InternshipMember Member { get; set; } = null!;
    }

    public class DeleteInternshipMemberCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    public class UpdateInternshipMemberStatusCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string Status { get; set; } = "Active";
    }

    // 8. Tests
    public class SubmitOnlineTestCommand : IRequest<Result<int>>
    {
        public string InternName { get; set; } = string.Empty;
        public string? InternEmail { get; set; }
        public string TopicTitle { get; set; } = string.Empty;
        public string Track { get; set; } = string.Empty;
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public string? FeedbackNotes { get; set; }
    }

    public class UploadTestResultCommand : IRequest<Result<int>>
    {
        public InternTestResult Result { get; set; } = null!;
    }

    public class DeleteTestResultCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class InternshipCommandsHandler :
        IRequestHandler<AskDoubtCommand, Result<int>>,
        IRequestHandler<AddClarificationCommand, Result<int>>,
        IRequestHandler<AcceptClarificationCommand, Result<int>>,
        IRequestHandler<UpvoteDoubtCommand, Result<int>>,
        IRequestHandler<AddStudyMaterialCommand, Result<int>>,
        IRequestHandler<DeleteStudyMaterialCommand, Result<int>>,
        IRequestHandler<AddYouTubeReferenceCommand, Result<int>>,
        IRequestHandler<DeleteYouTubeReferenceCommand, Result<int>>,
        IRequestHandler<AddInternshipMemberCommand, Result<int>>,
        IRequestHandler<DeleteInternshipMemberCommand, Result<int>>,
        IRequestHandler<UpdateInternshipMemberStatusCommand, Result<int>>,
        IRequestHandler<SubmitOnlineTestCommand, Result<int>>,
        IRequestHandler<UploadTestResultCommand, Result<int>>,
        IRequestHandler<DeleteTestResultCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<InternshipCommandsHandler> _logger;

        public InternshipCommandsHandler(IUnitOfWork<int> unitOfWork, ILogger<InternshipCommandsHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(AskDoubtCommand request, CancellationToken cancellationToken)
        {
            var doubt = new InternDoubt
            {
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                InternName = request.InternName.Trim(),
                InternEmployeeId = request.InternEmployeeId,
                Domain = !string.IsNullOrWhiteSpace(request.Track) ? request.Track : ".NET / C#",
                ScreenshotPath = request.ScreenshotPath,
                CreatedAt = DateTime.Now,
                Status = "Open",
                Upvotes = 0
            };

            await _unitOfWork.Repository<InternDoubt>().AddAsync(doubt);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(doubt.Id, "Doubt posted successfully.");
        }

        public async Task<Result<int>> Handle(AddClarificationCommand request, CancellationToken cancellationToken)
        {
            var doubt = await _unitOfWork.Repository<InternDoubt>().GetByIdAsync(request.DoubtId);
            if (doubt == null) return await Result<int>.FailAsync("Doubt not found.");

            var clarification = new InternDoubtClarification
            {
                InternDoubtId = request.DoubtId,
                ClarifiedByEmployeeName = request.EmployeeName.Trim(),
                EmployeeId = request.EmployeeId,
                ClarificationText = request.ClarificationText.Trim(),
                CodeSolution = request.CodeSolution,
                HelpfulLink = request.HelpfulLink,
                AnsweredAt = DateTime.Now
            };

            if (doubt.Status == "Open") doubt.Status = "In Discussion";

            await _unitOfWork.Repository<InternDoubtClarification>().AddAsync(clarification);
            await _unitOfWork.Repository<InternDoubt>().UpdateAsync(doubt);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(clarification.Id, "Clarification submitted.");
        }

        public async Task<Result<int>> Handle(AcceptClarificationCommand request, CancellationToken cancellationToken)
        {
            var clarification = await _unitOfWork.Repository<InternDoubtClarification>().GetByIdAsync(request.ClarificationId);
            if (clarification == null || clarification.InternDoubtId != request.DoubtId)
                return await Result<int>.FailAsync("Clarification not found.");

            clarification.IsAcceptedSolution = true;
            await _unitOfWork.Repository<InternDoubtClarification>().UpdateAsync(clarification);

            var doubt = await _unitOfWork.Repository<InternDoubt>().GetByIdAsync(request.DoubtId);
            if (doubt != null)
            {
                doubt.Status = "Resolved";
                await _unitOfWork.Repository<InternDoubt>().UpdateAsync(doubt);
            }

            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(request.DoubtId, "Clarification marked as accepted solution.");
        }

        public async Task<Result<int>> Handle(UpvoteDoubtCommand request, CancellationToken cancellationToken)
        {
            var doubt = await _unitOfWork.Repository<InternDoubt>().GetByIdAsync(request.Id);
            if (doubt == null) return await Result<int>.FailAsync("Doubt not found.");

            doubt.Upvotes += 1;
            await _unitOfWork.Repository<InternDoubt>().UpdateAsync(doubt);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(doubt.Upvotes);
        }

        public async Task<Result<int>> Handle(AddStudyMaterialCommand request, CancellationToken cancellationToken)
        {
            request.Material.CreatedAt = DateTime.Now;
            await _unitOfWork.Repository<InternStudyMaterial>().AddAsync(request.Material);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(request.Material.Id, "Study material added successfully.");
        }

        public async Task<Result<int>> Handle(DeleteStudyMaterialCommand request, CancellationToken cancellationToken)
        {
            var mat = await _unitOfWork.Repository<InternStudyMaterial>().GetByIdAsync(request.Id);
            if (mat != null)
            {
                await _unitOfWork.Repository<InternStudyMaterial>().DeleteAsync(mat);
                await _unitOfWork.Commit(cancellationToken);
                return await Result<int>.SuccessAsync(request.Id, "Study material deleted.");
            }
            return await Result<int>.FailAsync("Material not found.");
        }

        public async Task<Result<int>> Handle(AddYouTubeReferenceCommand request, CancellationToken cancellationToken)
        {
            request.Reference.CreatedAt = DateTime.Now;
            await _unitOfWork.Repository<InternYouTubeReference>().AddAsync(request.Reference);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(request.Reference.Id, "Video added successfully.");
        }

        public async Task<Result<int>> Handle(DeleteYouTubeReferenceCommand request, CancellationToken cancellationToken)
        {
            var video = await _unitOfWork.Repository<InternYouTubeReference>().GetByIdAsync(request.Id);
            if (video != null)
            {
                await _unitOfWork.Repository<InternYouTubeReference>().DeleteAsync(video);
                await _unitOfWork.Commit(cancellationToken);
                return await Result<int>.SuccessAsync(request.Id, "Video deleted.");
            }
            return await Result<int>.FailAsync("Video not found.");
        }

        public async Task<Result<int>> Handle(AddInternshipMemberCommand request, CancellationToken cancellationToken)
        {
            var trimmedName = request.Member.Name.Trim();
            var existing = await _unitOfWork.Repository<InternshipMember>().Entities
                .FirstOrDefaultAsync(m => m.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

            if (existing != null)
            {
                existing.Role = request.Member.Role;
                existing.Domain = request.Member.Domain;
                existing.MentorName = request.Member.MentorName;
                existing.Notes = request.Member.Notes;
                await _unitOfWork.Repository<InternshipMember>().UpdateAsync(existing);
            }
            else
            {
                request.Member.Name = trimmedName;
                await _unitOfWork.Repository<InternshipMember>().AddAsync(request.Member);
            }

            // Sync with Employee directory
            var existingEmp = await _unitOfWork.Repository<Employee>().Entities
                .FirstOrDefaultAsync(e => e.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

            if (existingEmp == null)
            {
                var newEmp = new Employee
                {
                    Name = trimmedName,
                    Email = !string.IsNullOrWhiteSpace(request.Member.Email) ? request.Member.Email.Trim() : $"{trimmedName.ToLower().Replace(" ", ".")}@auxinz.io",
                    Department = "Intern",
                    Designation = $"{request.Member.Role} ({request.Member.Domain})",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                await _unitOfWork.Repository<Employee>().AddAsync(newEmp);
            }

            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(request.Member.Id, $"Member '{trimmedName}' saved successfully.");
        }

        public async Task<Result<int>> Handle(DeleteInternshipMemberCommand request, CancellationToken cancellationToken)
        {
            var member = await _unitOfWork.Repository<InternshipMember>().GetByIdAsync(request.Id);
            if (member != null)
            {
                await _unitOfWork.Repository<InternshipMember>().DeleteAsync(member);
                await _unitOfWork.Commit(cancellationToken);
                return await Result<int>.SuccessAsync(request.Id, "Member removed from internship program.");
            }
            return await Result<int>.FailAsync("Member not found.");
        }

        public async Task<Result<int>> Handle(UpdateInternshipMemberStatusCommand request, CancellationToken cancellationToken)
        {
            var member = await _unitOfWork.Repository<InternshipMember>().GetByIdAsync(request.Id);
            if (member != null)
            {
                member.Status = request.Status;
                await _unitOfWork.Repository<InternshipMember>().UpdateAsync(member);
                await _unitOfWork.Commit(cancellationToken);
                return await Result<int>.SuccessAsync(member.Id, "Status updated.");
            }
            return await Result<int>.FailAsync("Member not found.");
        }

        public async Task<Result<int>> Handle(SubmitOnlineTestCommand request, CancellationToken cancellationToken)
        {
            double percentage = request.TotalQuestions > 0 ? Math.Round((double)request.Score / request.TotalQuestions * 100, 1) : 0;
            var testResult = new InternTestResult
            {
                InternName = request.InternName.Trim(),
                InternEmail = request.InternEmail?.Trim(),
                TopicTitle = request.TopicTitle.Trim(),
                Track = request.Track.Trim(),
                ScorePercentage = percentage,
                IsPassed = percentage >= 60.0,
                TakenAt = DateTime.Now,
                FeedbackNotes = request.FeedbackNotes
            };

            await _unitOfWork.Repository<InternTestResult>().AddAsync(testResult);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(testResult.Id, "Test result recorded successfully.");
        }

        public async Task<Result<int>> Handle(UploadTestResultCommand request, CancellationToken cancellationToken)
        {
            request.Result.TakenAt = DateTime.Now;
            request.Result.IsPassed = request.Result.ScorePercentage >= 60.0;
            await _unitOfWork.Repository<InternTestResult>().AddAsync(request.Result);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(request.Result.Id, "Test result uploaded.");
        }

        public async Task<Result<int>> Handle(DeleteTestResultCommand request, CancellationToken cancellationToken)
        {
            var res = await _unitOfWork.Repository<InternTestResult>().GetByIdAsync(request.Id);
            if (res != null)
            {
                await _unitOfWork.Repository<InternTestResult>().DeleteAsync(res);
                await _unitOfWork.Commit(cancellationToken);
                return await Result<int>.SuccessAsync(request.Id, "Test result deleted.");
            }
            return await Result<int>.FailAsync("Test result not found.");
        }
    }
}
