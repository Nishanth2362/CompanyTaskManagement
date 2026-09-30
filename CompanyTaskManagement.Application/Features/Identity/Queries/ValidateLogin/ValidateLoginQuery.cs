using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Security;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.Identity.Queries.ValidateLogin
{
    public class ValidateLoginQuery : IRequest<Result<UserAccount>>
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    internal class ValidateLoginQueryHandler : IRequestHandler<ValidateLoginQuery, Result<UserAccount>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public ValidateLoginQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<UserAccount>> Handle(ValidateLoginQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return await Result<UserAccount>.FailAsync("Email and password are required.");
            }

            var trimmedEmail = request.Email.Trim().ToLower();
            var user = await _unitOfWork.Repository<UserAccount>().Entities
                .Include(u => u.Employee)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == trimmedEmail && u.IsActive, cancellationToken);

            if (user == null || !PasswordHasher.VerifyPassword(user.PasswordHash, request.Password))
            {
                return await Result<UserAccount>.FailAsync("Invalid email address or password.");
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _unitOfWork.Repository<UserAccount>().UpdateAsync(user);
            await _unitOfWork.Commit(cancellationToken);

            return await Result<UserAccount>.SuccessAsync(user);
        }
    }
}
