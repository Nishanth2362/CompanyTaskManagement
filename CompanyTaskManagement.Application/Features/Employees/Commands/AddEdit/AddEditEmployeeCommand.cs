using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Employees.Commands.AddEdit
{
    public class AddEditEmployeeCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? CompanyName { get; set; } = "Auxinzio";
        public string? Designation { get; set; } = "Software Engineer";
        public string? Department { get; set; } = "Engineering";
        public string? Phone { get; set; }
        public bool IsActive { get; set; } = true;
    }

    internal class AddEditEmployeeCommandHandler : IRequestHandler<AddEditEmployeeCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<AddEditEmployeeCommandHandler> _logger;

        public AddEditEmployeeCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<AddEditEmployeeCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(AddEditEmployeeCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return await Result<int>.FailAsync("Full Name is required.");
                }

                if (request.Id == 0)
                {
                    var employee = new Employee
                    {
                        Name = request.Name,
                        Email = request.Email,
                        CompanyName = request.CompanyName,
                        Designation = request.Designation,
                        Department = request.Department,
                        Phone = request.Phone,
                        IsActive = request.IsActive,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Repository<Employee>().AddAsync(employee);
                    await _unitOfWork.CommitAndRemoveCache(cancellationToken, "employees-cache");
                    _logger.LogInformation("Employee {EmployeeId} created successfully.", employee.Id);
                    return await Result<int>.SuccessAsync(employee.Id, "Employee registered successfully.");
                }
                else
                {
                    var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(request.Id);
                    if (employee == null)
                    {
                        return await Result<int>.FailAsync("Employee record not found.");
                    }

                    employee.Name = request.Name;
                    employee.Email = request.Email;
                    employee.CompanyName = request.CompanyName;
                    employee.Designation = request.Designation;
                    employee.Department = request.Department;
                    employee.Phone = request.Phone;
                    employee.IsActive = request.IsActive;

                    await _unitOfWork.Repository<Employee>().UpdateAsync(employee);
                    await _unitOfWork.CommitAndRemoveCache(cancellationToken, "employees-cache");
                    _logger.LogInformation("Employee {EmployeeId} updated successfully.", employee.Id);
                    return await Result<int>.SuccessAsync(employee.Id, "Employee updated successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Employee command: {Message}", ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}

