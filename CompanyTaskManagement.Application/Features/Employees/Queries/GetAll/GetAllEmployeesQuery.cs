using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.Employees.Queries.GetAll
{
    public class GetAllEmployeesQuery : IRequest<Result<List<Employee>>>
    {
    }

    internal class GetAllEmployeesQueryHandler : IRequestHandler<GetAllEmployeesQuery, Result<List<Employee>>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetAllEmployeesQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<Employee>>> Handle(GetAllEmployeesQuery request, CancellationToken cancellationToken)
        {
            var employees = await _unitOfWork.Repository<Employee>().Entities
                .AsNoTracking()
                .OrderBy(e => e.Name)
                .ToListAsync(cancellationToken);

            return await Result<List<Employee>>.SuccessAsync(employees);
        }
    }
}

