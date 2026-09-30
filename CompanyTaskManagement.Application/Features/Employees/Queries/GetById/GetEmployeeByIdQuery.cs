using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.Employees.Queries.GetById
{
    public class GetEmployeeByIdQuery : IRequest<Result<Employee>>
    {
        public int Id { get; set; }

        public GetEmployeeByIdQuery(int id)
        {
            Id = id;
        }
    }

    internal class GetEmployeeByIdQueryHandler : IRequestHandler<GetEmployeeByIdQuery, Result<Employee>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetEmployeeByIdQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Employee>> Handle(GetEmployeeByIdQuery request, CancellationToken cancellationToken)
        {
            var employee = await _unitOfWork.Repository<Employee>().Entities
                .Include(e => e.TaskEmployees)
                    .ThenInclude(te => te.Task)
                        .ThenInclude(t => t.Project)
                .Include(e => e.TaskEmployees)
                    .ThenInclude(te => te.Task)
                        .ThenInclude(t => t.TaskCompanies)
                            .ThenInclude(tc => tc.Company)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken);

            if (employee == null)
            {
                return await Result<Employee>.FailAsync("Employee not found.");
            }

            return await Result<Employee>.SuccessAsync(employee);
        }
    }
}
