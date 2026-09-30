using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.Projects.Queries.GetAll
{
    public class GetAllProjectsQuery : IRequest<Result<List<Project>>>
    {
    }

    internal class GetAllProjectsQueryHandler : IRequestHandler<GetAllProjectsQuery, Result<List<Project>>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetAllProjectsQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<List<Project>>> Handle(GetAllProjectsQuery request, CancellationToken cancellationToken)
        {
            var projects = await _unitOfWork.Repository<Project>().Entities
                .AsNoTracking()
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            return await Result<List<Project>>.SuccessAsync(projects);
        }
    }
}

