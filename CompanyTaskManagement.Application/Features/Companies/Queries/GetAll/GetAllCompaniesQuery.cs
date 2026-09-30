using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.Companies.Queries.GetAll
{
    public class GetAllCompaniesQuery : IRequest<Result<List<Company>>>
    {
    }

    internal class GetAllCompaniesQueryHandler : IRequestHandler<GetAllCompaniesQuery, Result<List<Company>>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetAllCompaniesQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<Company>>> Handle(GetAllCompaniesQuery request, CancellationToken cancellationToken)
        {
            var companies = await _unitOfWork.Repository<Company>().Entities
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync(cancellationToken);

            return await Result<List<Company>>.SuccessAsync(companies);
        }
    }
}
