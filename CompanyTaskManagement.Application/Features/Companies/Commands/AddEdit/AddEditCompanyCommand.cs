using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Companies.Commands.AddEdit
{
    public class AddEditCompanyCommand : IRequest<Result<int>>
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    internal class AddEditCompanyCommandHandler : IRequestHandler<AddEditCompanyCommand, Result<int>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<AddEditCompanyCommandHandler> _logger;

        public AddEditCompanyCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<AddEditCompanyCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<int>> Handle(AddEditCompanyCommand request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return await Result<int>.FailAsync("Company Name is required.");
                }

                var trimmedName = request.Name.Trim();

                if (request.Id == 0)
                {
                    var existing = await _unitOfWork.Repository<Company>().Entities
                        .FirstOrDefaultAsync(c => c.Name.ToLower() == trimmedName.ToLower(), cancellationToken);

                    if (existing != null)
                    {
                        return await Result<int>.SuccessAsync(existing.Id, "Company already exists.");
                    }

                    var company = new Company
                    {
                        Name = trimmedName
                    };

                    await _unitOfWork.Repository<Company>().AddAsync(company);
                    await _unitOfWork.CommitAndRemoveCache(cancellationToken, "companies-cache");
                    _logger.LogInformation("Company {CompanyId} created.", company.Id);
                    return await Result<int>.SuccessAsync(company.Id, "Company created successfully.");
                }
                else
                {
                    var company = await _unitOfWork.Repository<Company>().GetByIdAsync(request.Id);
                    if (company == null)
                    {
                        return await Result<int>.FailAsync("Company not found.");
                    }

                    company.Name = trimmedName;
                    await _unitOfWork.Repository<Company>().UpdateAsync(company);
                    await _unitOfWork.CommitAndRemoveCache(cancellationToken, "companies-cache");
                    return await Result<int>.SuccessAsync(company.Id, "Company updated successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving company: {Message}", ex.Message);
                return await Result<int>.FailAsync(ex.Message);
            }
        }
    }
}
