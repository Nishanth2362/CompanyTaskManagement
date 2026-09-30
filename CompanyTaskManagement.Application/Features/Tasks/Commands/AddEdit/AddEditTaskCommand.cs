using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Domain.Enums;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TaskStatus = CompanyTaskManagement.Domain.Enums.TaskStatus;

namespace CompanyTaskManagement.Application.Features.Tasks.Commands.AddEdit
{
    public class AddEditTaskCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string TaskName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;
        public TaskStatus Status { get; set; } = TaskStatus.ToDo;
        public int? ProjectId { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int Progress { get; set; } = 0;
        public string? DelayReason { get; set; }
        public string? ErrorDetails { get; set; }
        public string? ErrorScreenshotPath { get; set; }
        public List<int> CompanyIds { get; set; } = new();
        public List<int> EmployeeIds { get; set; } = new();
    }

    internal class AddEditTaskCommandHandler : IRequestHandler<AddEditTaskCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<AddEditTaskCommandHandler> _logger;

        public AddEditTaskCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<AddEditTaskCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(AddEditTaskCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.TaskName))
                {
                    return await Result<int>.FailAsync("Task Name is required.");
                }

                if (request.Id == 0)
                {
                    var task = new TaskItem
                    {
                        TaskName = request.TaskName,
                        Description = request.Description,
                        Priority = request.Priority,
                        Status = request.Status,
                        ProjectId = request.ProjectId,
                        StartDate = request.StartDate,
                        EndDate = request.EndDate,
                        DueDate = request.DueDate,
                        Progress = request.Progress,
                        DelayReason = request.DelayReason,
                        ErrorDetails = request.ErrorDetails,
                        ErrorScreenshotPath = request.ErrorScreenshotPath,
                        CreatedAt = DateTime.UtcNow
                    };

                    if (request.CompanyIds != null && request.CompanyIds.Count > 0)
                    {
                        foreach (var cId in request.CompanyIds)
                        {
                            task.TaskCompanies.Add(new TaskCompany { CompanyId = cId });
                        }
                    }

                    if (request.EmployeeIds != null && request.EmployeeIds.Count > 0)
                    {
                        foreach (var eId in request.EmployeeIds)
                        {
                            task.TaskEmployees.Add(new TaskEmployee { EmployeeId = eId });
                        }
                    }

                    await _unitOfWork.Repository<TaskItem>().AddAsync(task);
                    await _unitOfWork.CommitAndRemoveCache(cancellationToken, "tasks-cache");
                    _logger.LogInformation("Task {TaskId} created successfully.", task.Id);
                    return await Result<int>.SuccessAsync(task.Id, "Task created successfully.");
                }
                else
                {
                    var task = await _unitOfWork.Repository<TaskItem>().Entities
                        .Include(t => t.TaskCompanies)
                        .Include(t => t.TaskEmployees)
                        .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken);

                    if (task == null)
                    {
                        return await Result<int>.FailAsync("Task not found.");
                    }

                    task.TaskName = request.TaskName;
                    task.Description = request.Description;
                    task.Priority = request.Priority;
                    task.Status = request.Status;
                    task.ProjectId = request.ProjectId;
                    task.StartDate = request.StartDate;
                    task.EndDate = request.EndDate;
                    task.DueDate = request.DueDate;
                    task.Progress = request.Progress;
                    task.DelayReason = request.DelayReason;
                    task.ErrorDetails = request.ErrorDetails;
                    if (!string.IsNullOrEmpty(request.ErrorScreenshotPath))
                    {
                        task.ErrorScreenshotPath = request.ErrorScreenshotPath;
                    }

                    // Update Company relationships
                    task.TaskCompanies.Clear();
                    if (request.CompanyIds != null)
                    {
                        foreach (var cId in request.CompanyIds)
                        {
                            task.TaskCompanies.Add(new TaskCompany { TaskId = task.Id, CompanyId = cId });
                        }
                    }

                    // Update Employee relationships
                    task.TaskEmployees.Clear();
                    if (request.EmployeeIds != null)
                    {
                        foreach (var eId in request.EmployeeIds)
                        {
                            task.TaskEmployees.Add(new TaskEmployee { TaskId = task.Id, EmployeeId = eId });
                        }
                    }

                    await _unitOfWork.Repository<TaskItem>().UpdateAsync(task);
                    await _unitOfWork.CommitAndRemoveCache(cancellationToken, "tasks-cache");
                    _logger.LogInformation("Task {TaskId} updated successfully.", task.Id);
                    return await Result<int>.SuccessAsync(task.Id, "Task updated successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AddEditTaskCommand: {Message}", ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}

