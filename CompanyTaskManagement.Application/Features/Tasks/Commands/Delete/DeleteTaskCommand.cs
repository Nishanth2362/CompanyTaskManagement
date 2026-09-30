using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Tasks.Commands.Delete
{
    public class DeleteTaskCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
    }

    internal class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<DeleteTaskCommandHandler> _logger;

        public DeleteTaskCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<DeleteTaskCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var task = await _unitOfWork.Repository<TaskItem>().GetByIdAsync(request.Id);
                if (task == null)
                {
                    return await Result<int>.FailAsync("Task not found.");
                }

                await _unitOfWork.Repository<TaskItem>().DeleteAsync(task);
                await _unitOfWork.CommitAndRemoveCache(cancellationToken, "tasks-cache");
                _logger.LogInformation("Task {TaskId} deleted successfully.", request.Id);
                return await Result<int>.SuccessAsync(request.Id, "Task deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting task {TaskId}: {Message}", request.Id, ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}

