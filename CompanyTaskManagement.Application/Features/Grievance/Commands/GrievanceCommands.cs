using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Grievance.Commands
{
    // 1. Submit Grievance
    public class SubmitGrievanceCommand : IRequest<Result<HrComplaint>>
    {
        public HrComplaint Complaint { get; set; } = null!;
        public string? AttachmentPath { get; set; }
    }

    // 2. Update Status
    public class UpdateGrievanceStatusCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ResolutionSummary { get; set; }
        public string? ResolvedBy { get; set; }
    }

    // 3. Add Internal Note
    public class AddGrievanceInternalNoteCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string NoteText { get; set; } = string.Empty;
    }

    internal class GrievanceCommandsHandler :
        IRequestHandler<SubmitGrievanceCommand, Result<HrComplaint>>,
        IRequestHandler<UpdateGrievanceStatusCommand, Result<int>>,
        IRequestHandler<AddGrievanceInternalNoteCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<GrievanceCommandsHandler> _logger;

        public GrievanceCommandsHandler(IUnitOfWork<int> unitOfWork, ILogger<GrievanceCommandsHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<HrComplaint>> Handle(SubmitGrievanceCommand request, CancellationToken cancellationToken)
        {
            var model = request.Complaint;
            if (model.IsAnonymous)
            {
                model.EmployeeId = null;
                model.SubmitterName = "Anonymous Contributor";
                model.SubmitterEmail = null;
            }
            else if (model.EmployeeId.HasValue && string.IsNullOrWhiteSpace(model.SubmitterName))
            {
                var emp = await _unitOfWork.Repository<Employee>().GetByIdAsync(model.EmployeeId.Value);
                if (emp != null) model.SubmitterName = emp.Name;
            }

            model.AttachmentPath = request.AttachmentPath;
            model.CreatedAt = DateTime.Now;
            model.Status = "Submitted";
            if (string.IsNullOrWhiteSpace(model.TicketNumber))
            {
                model.TicketNumber = $"HRG-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            }

            await _unitOfWork.Repository<HrComplaint>().AddAsync(model);
            await _unitOfWork.Commit(cancellationToken);

            _logger.LogInformation("New HR Grievance logged with Ticket {Ticket}", model.TicketNumber);
            return await Result<HrComplaint>.SuccessAsync(model);
        }

        public async Task<Result<int>> Handle(UpdateGrievanceStatusCommand request, CancellationToken cancellationToken)
        {
            var complaint = await _unitOfWork.Repository<HrComplaint>().GetByIdAsync(request.Id);
            if (complaint == null) return await Result<int>.FailAsync("Grievance case not found.");

            complaint.Status = request.Status;
            if (!string.IsNullOrWhiteSpace(request.ResolutionSummary))
            {
                complaint.HrResponseNotes = request.ResolutionSummary.Trim();
            }

            if (request.Status == "Resolved" || request.Status == "Dismissed")
            {
                complaint.ResolvedAt = DateTime.Now;
                complaint.HrInvestigatorName = request.ResolvedBy ?? "HR Administration";
            }

            await _unitOfWork.Repository<HrComplaint>().UpdateAsync(complaint);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(complaint.Id, $"Case {complaint.TicketNumber} status updated to {request.Status}.");
        }

        public async Task<Result<int>> Handle(AddGrievanceInternalNoteCommand request, CancellationToken cancellationToken)
        {
            var complaint = await _unitOfWork.Repository<HrComplaint>().GetByIdAsync(request.Id);
            if (complaint == null) return await Result<int>.FailAsync("Grievance case not found.");

            var timestamp = DateTime.Now.ToString("MMM dd, yyyy hh:mm tt");
            var formattedNote = $"[{timestamp} - {request.AuthorName}]\n{request.NoteText.Trim()}\n----------------------------------------\n";

            complaint.HrResponseNotes = string.IsNullOrWhiteSpace(complaint.HrResponseNotes)
                ? formattedNote
                : complaint.HrResponseNotes + "\n" + formattedNote;

            await _unitOfWork.Repository<HrComplaint>().UpdateAsync(complaint);
            await _unitOfWork.Commit(cancellationToken);
            return await Result<int>.SuccessAsync(complaint.Id, "Internal investigation note added successfully.");
        }
    }
}
