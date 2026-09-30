using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Employees.Commands.Delete
{
    public class DeleteEmployeeCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }

        public DeleteEmployeeCommand(int id)
        {
            Id = id;
        }
    }

    internal class DeleteEmployeeCommandHandler : IRequestHandler<DeleteEmployeeCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<DeleteEmployeeCommandHandler> _logger;

        public DeleteEmployeeCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<DeleteEmployeeCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(DeleteEmployeeCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var employee = await _unitOfWork.Repository<Employee>().GetByIdAsync(request.Id);
                if (employee == null)
                {
                    return await Result<int>.FailAsync("Employee record not found.");
                }

                var hasAssignedTasks = await _unitOfWork.Repository<TaskItem>().Entities
                    .AnyAsync(t => t.TaskEmployees.Any(te => te.EmployeeId == request.Id), cancellationToken);

                if (hasAssignedTasks)
                {
                    return await Result<int>.FailAsync("Cannot delete employee because tasks are currently assigned to them.");
                }

                await _unitOfWork.Repository<Employee>().DeleteAsync(employee);
                await _unitOfWork.CommitAndRemoveCache(cancellationToken, "employees-cache");
                _logger.LogInformation("Employee {EmployeeId} deleted successfully.", request.Id);
                return await Result<int>.SuccessAsync(request.Id, "Employee deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee {EmployeeId}: {Message}", request.Id, ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}
