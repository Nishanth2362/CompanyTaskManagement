using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Companies.Commands.Delete
{
    public class DeleteCompanyCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }

        public DeleteCompanyCommand(int id)
        {
            Id = id;
        }
    }

    internal class DeleteCompanyCommandHandler : IRequestHandler<DeleteCompanyCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<DeleteCompanyCommandHandler> _logger;

        public DeleteCompanyCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<DeleteCompanyCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(DeleteCompanyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var company = await _unitOfWork.Repository<Company>().GetByIdAsync(request.Id);
                if (company == null)
                {
                    return await Result<int>.FailAsync("Company not found.");
                }

                var isReferenced = await _unitOfWork.Repository<TaskItem>().Entities
                    .AnyAsync(t => t.TaskCompanies.Any(tc => tc.CompanyId == request.Id), cancellationToken);

                if (isReferenced)
                {
                    return await Result<int>.FailAsync("Cannot delete company because it is linked to one or more tasks.");
                }

                await _unitOfWork.Repository<Company>().DeleteAsync(company);
                await _unitOfWork.CommitAndRemoveCache(cancellationToken, "companies-cache");
                _logger.LogInformation("Company {CompanyId} deleted.", request.Id);
                return await Result<int>.SuccessAsync(request.Id, "Company deleted successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting company {CompanyId}: {Message}", request.Id, ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}
