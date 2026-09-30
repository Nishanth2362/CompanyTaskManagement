using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Security;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Identity.Commands.Register
{
    public class RegisterUserCommand : IRequest<Result<UserAccount>>
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Employee";
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public string? Designation { get; set; }
    }

    internal class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<UserAccount>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<RegisterUserCommandHandler> _logger;

        public RegisterUserCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<RegisterUserCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<UserAccount>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var trimmedEmail = request.Email.Trim().ToLower();

                var existingUser = await _unitOfWork.Repository<UserAccount>().Entities
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == trimmedEmail, cancellationToken);

                if (existingUser != null)
                {
                    return await Result<UserAccount>.FailAsync($"Corporate email '{request.Email}' is already registered. Please sign in.");
                }

                var effectiveFullName = request.FullName.Trim();

                // Link to existing employee profile by email or create a new employee profile
                var existingEmp = await _unitOfWork.Repository<Employee>().Entities
                    .FirstOrDefaultAsync(e => e.Email != null && e.Email.ToLower() == trimmedEmail, cancellationToken)
                    ?? await _unitOfWork.Repository<Employee>().Entities
                    .FirstOrDefaultAsync(e => e.Name.ToLower() == effectiveFullName.ToLower(), cancellationToken);

                int linkedEmployeeId;
                if (existingEmp != null)
                {
                    linkedEmployeeId = existingEmp.Id;
                    if (string.IsNullOrWhiteSpace(existingEmp.Phone) && !string.IsNullOrWhiteSpace(request.Phone))
                    {
                        existingEmp.Phone = request.Phone.Trim();
                    }
                    if (string.IsNullOrWhiteSpace(existingEmp.Email))
                    {
                        existingEmp.Email = trimmedEmail;
                    }
                    await _unitOfWork.Repository<Employee>().UpdateAsync(existingEmp);
                }
                else
                {
                    var newEmp = new Employee
                    {
                        Name = effectiveFullName,
                        Email = trimmedEmail,
                        Phone = request.Phone?.Trim(),
                        Department = !string.IsNullOrWhiteSpace(request.Department) ? request.Department.Trim() : "Engineering",
                        Designation = !string.IsNullOrWhiteSpace(request.Designation) ? request.Designation.Trim() : (request.Role == "HR" ? "HR Specialist" : "Software Engineer"),
                        CompanyName = "Auxinz.io",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.Repository<Employee>().AddAsync(newEmp);
                    await _unitOfWork.Commit(cancellationToken);
                    linkedEmployeeId = newEmp.Id;
                }

                var newUser = new UserAccount
                {
                    Email = trimmedEmail,
                    FullName = effectiveFullName,
                    PasswordHash = PasswordHasher.HashPassword(request.Password),
                    Role = string.IsNullOrWhiteSpace(request.Role) ? "Employee" : request.Role,
                    EmployeeId = linkedEmployeeId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _unitOfWork.Repository<UserAccount>().AddAsync(newUser);
                await _unitOfWork.Commit(cancellationToken);

                _logger.LogInformation("New user registered: {Email} ({FullName}, {Role})", newUser.Email, newUser.FullName, newUser.Role);
                return await Result<UserAccount>.SuccessAsync(newUser, $"Registration successful for {newUser.FullName}!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering user: {Message}", ex.Message);
                return await Result<UserAccount>.FailAsync(ex.Message);
            }
        }
    }
}
