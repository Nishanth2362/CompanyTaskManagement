using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;

namespace CompanyTaskManagement.Application.Features.Identity.Queries.GetEmployeeName
{
    public class GetEmployeeNameQuery : IRequest<Result<string>>
    {
        public int EmployeeId { get; set; }

        public GetEmployeeNameQuery(int employeeId)
        {
            EmployeeId = employeeId;
        }
    }

    internal class GetEmployeeNameQueryHandler : IRequestHandler<GetEmployeeNameQuery, Result<string>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetEmployeeNameQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<string>> Handle(GetEmployeeNameQuery request, CancellationToken cancellationToken)
        {
            var emp = await _unitOfWork.Repository<Employee>().GetByIdAsync(request.EmployeeId);
            return await Result<string>.SuccessAsync(emp?.Name ?? "Employee");
        }
    }
}
