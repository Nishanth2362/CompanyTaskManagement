using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Employees.Commands.AddIntern
{
    public class AddInternCommand : IRequest<Result<int>>
    {
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Domain { get; set; } = "Backend .NET / C#";
        public string? MentorName { get; set; }
        public DateTime? JoinedDate { get; set; }
        public string? Notes { get; set; }
    }

    internal class AddInternCommandHandler : IRequestHandler<AddInternCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<AddInternCommandHandler> _logger;

        public AddInternCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<AddInternCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(AddInternCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return await Result<int>.FailAsync("Intern name is required.");
                }

                var trimmedName = request.Name.Trim();
                var domain = string.IsNullOrWhiteSpace(request.Domain) ? "Backend .NET / C#" : request.Domain;
                var email = !string.IsNullOrWhiteSpace(request.Email) ? request.Email.Trim() : $"{trimmedName.ToLower().Replace(" ", ".")}@auxinz.io";

                var existingEmp = await _unitOfWork.Repository<Employee>().Entities
                    .FirstOrDefaultAsync(e => e.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

                int empId;
                if (existingEmp == null)
                {
                    var newEmp = new Employee
                    {
                        Name = trimmedName,
                        Email = email,
                        Phone = request.Phone,
                        Department = "Intern",
                        Designation = $"Software Intern ({domain})",
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    await _unitOfWork.Repository<Employee>().AddAsync(newEmp);
                    await _unitOfWork.CommitAndRemoveCache(cancellationToken, "employees-cache");
                    empId = newEmp.Id;
                }
                else
                {
                    existingEmp.Department = "Intern";
                    if (string.IsNullOrWhiteSpace(existingEmp.Designation) || !existingEmp.Designation.Contains("Intern"))
                    {
                        existingEmp.Designation = $"Software Intern ({domain})";
                    }
                    await _unitOfWork.Repository<Employee>().UpdateAsync(existingEmp);
                    await _unitOfWork.CommitAndRemoveCache(cancellationToken, "employees-cache");
                    empId = existingEmp.Id;
                }

                // Sync with InternshipMembers table
                var existingMember = await _unitOfWork.Repository<InternshipMember>().Entities
                    .FirstOrDefaultAsync(m => m.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

                if (existingMember == null)
                {
                    var newMember = new InternshipMember
                    {
                        Name = trimmedName,
                        Email = email,
                        Role = "Intern",
                        Domain = domain,
                        MentorName = request.MentorName,
                        Status = "Active",
                        JoinedDate = request.JoinedDate ?? DateTime.Today,
                        Notes = request.Notes
                    };
                    await _unitOfWork.Repository<InternshipMember>().AddAsync(newMember);
                    await _unitOfWork.Commit(cancellationToken);
                }

                _logger.LogInformation("Intern {Name} added successfully.", trimmedName);
                return await Result<int>.SuccessAsync(empId, $"Intern '{trimmedName}' added successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding intern: {Message}", ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}
