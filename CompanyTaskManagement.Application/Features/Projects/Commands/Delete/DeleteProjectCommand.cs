using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Projects.Commands.Delete
{
    public class DeleteProjectCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }

        public DeleteProjectCommand(int id)
        {
            Id = id;
        }
    }

    internal class DeleteProjectCommandHandler : IRequestHandler<DeleteProjectCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<DeleteProjectCommandHandler> _logger;

        public DeleteProjectCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<DeleteProjectCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var project = await _unitOfWork.Repository<Project>().GetByIdAsync(request.Id);
                if (project == null)
                {
                    return await Result<int>.FailAsync("Project not found.");
                }

                var hasTasks = await _unitOfWork.Repository<TaskItem>().Entities
                    .AnyAsync(t => t.ProjectId == request.Id, cancellationToken);

                if (hasTasks)
                {
                    return await Result<int>.FailAsync("Cannot delete project because tasks are associated with it.");
                }

                await _unitOfWork.Repository<Project>().DeleteAsync(project);
                await _unitOfWork.CommitAndRemoveCache(cancellationToken, "projects-cache");
                _logger.LogInformation("Project {ProjectId} deleted successfully.", request.Id);
                return await Result<int>.SuccessAsync(request.Id, "Project deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {ProjectId}: {Message}", request.Id, ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}
