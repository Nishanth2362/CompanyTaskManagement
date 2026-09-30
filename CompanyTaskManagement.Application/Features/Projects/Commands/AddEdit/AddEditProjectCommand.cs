using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Projects.Commands.AddEdit
{
    public class AddEditProjectCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string ClientCompany { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "In Progress";
        public string Priority { get; set; } = "High";
        public DateTime? StartDate { get; set; }
        public DateTime? TargetEndDate { get; set; }
        public decimal? Budget { get; set; }
        public string LeadManagerName { get; set; } = string.Empty;
    }

    internal class AddEditProjectCommandHandler : IRequestHandler<AddEditProjectCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<AddEditProjectCommandHandler> _logger;

        public AddEditProjectCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<AddEditProjectCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(AddEditProjectCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.ProjectName))
                {
                    return await Result<int>.FailAsync("Project Name is required.");
                }

                if (request.Id == 0)
                {
                    var project = new Project
                    {
                        ProjectName = request.ProjectName,
                        ClientCompany = request.ClientCompany,
                        Description = request.Description,
                        Status = request.Status,
                        Priority = request.Priority,
                        StartDate = request.StartDate,
                        TargetEndDate = request.TargetEndDate,
                        Budget = request.Budget,
                        LeadManagerName = request.LeadManagerName,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _unitOfWork.Repository<Project>().AddAsync(project);
                    await _unitOfWork.CommitAndRemoveCache(cancellationToken, "projects-cache");
                    _logger.LogInformation("Project {ProjectId} created successfully.", project.Id);
                    return await Result<int>.SuccessAsync(project.Id, "Project created successfully.");
                }
                else
                {
                    var project = await _unitOfWork.Repository<Project>().GetByIdAsync(request.Id);
                    if (project == null)
                    {
                        return await Result<int>.FailAsync("Project not found.");
                    }

                    project.ProjectName = request.ProjectName;
                    project.ClientCompany = request.ClientCompany;
                    project.Description = request.Description;
                    project.Status = request.Status;
                    project.Priority = request.Priority;
                    project.StartDate = request.StartDate;
                    project.TargetEndDate = request.TargetEndDate;
                    project.Budget = request.Budget;
                    project.LeadManagerName = request.LeadManagerName;

                    await _unitOfWork.Repository<Project>().UpdateAsync(project);
                    await _unitOfWork.CommitAndRemoveCache(cancellationToken, "projects-cache");
                    _logger.LogInformation("Project {ProjectId} updated successfully.", project.Id);
                    return await Result<int>.SuccessAsync(project.Id, "Project updated successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving project: {Message}", ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}

