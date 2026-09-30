using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Security;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Application.Features.Identity.Commands.ForgotPassword
{
    public class ForgotPasswordCommand : IRequest<Result<string>>
    {
        public string Email { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    internal class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, Result<string>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;
        private readonly ILogger<ForgotPasswordCommandHandler> _logger;

        public ForgotPasswordCommandHandler(IUnitOfWork<int> unitOfWork, ILogger<ForgotPasswordCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<string>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
        {
            try
            {
                var trimmedEmail = request.Email.Trim().ToLower();
                var user = await _unitOfWork.Repository<UserAccount>().Entities
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == trimmedEmail, cancellationToken);

                if (user == null)
                {
                    return await Result<string>.FailAsync($"No account was found with corporate email '{request.Email}'.");
                }

                user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);
                await _unitOfWork.Repository<UserAccount>().UpdateAsync(user);
                await _unitOfWork.Commit(cancellationToken);

                _logger.LogInformation("Password reset successfully for corporate user {Email}.", user.Email);
                return await Result<string>.SuccessAsync(user.FullName, $"Password has been successfully updated for {user.FullName}!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password: {Message}", ex.Message);
                return await Result<string>.FailAsync(ex.Message);
            }
        }
    }
}
