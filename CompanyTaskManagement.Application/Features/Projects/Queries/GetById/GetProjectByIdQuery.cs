using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.Projects.Queries.GetById
{
    public class GetProjectByIdQuery : IRequest<Result<Project>>
    {
        public int Id { get; set; }

        public GetProjectByIdQuery(int id)
        {
            Id = id;
        }
    }

    internal class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, Result<Project>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetProjectByIdQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Project>> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
        {
            var project = await _unitOfWork.Repository<Project>().Entities
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken);

            if (project == null)
            {
                return await Result<Project>.FailAsync("Project not found.");
            }

            return await Result<Project>.SuccessAsync(project);
        }
    }
}
