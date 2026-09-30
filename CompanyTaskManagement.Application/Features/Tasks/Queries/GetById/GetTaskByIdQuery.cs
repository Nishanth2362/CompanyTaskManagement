using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.Tasks.Queries.GetById
{
    public class GetTaskByIdQuery : IRequest<Result<TaskItem>>
    {
        public int Id { get; set; }
    }

    internal class GetTaskByIdQueryHandler : IRequestHandler<GetTaskByIdQuery, Result<TaskItem>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetTaskByIdQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<TaskItem>> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
        {
            var task = await _unitOfWork.Repository<TaskItem>().Entities
                .Include(t => t.Project)
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

            if (task == null)
            {
                return await Result<TaskItem>.FailAsync("Task not found.");
            }

            return await Result<TaskItem>.SuccessAsync(task);
        }
    }
}

