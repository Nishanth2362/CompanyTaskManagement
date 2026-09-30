using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.MindForge.Queries;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.MindForge.Commands
{
    public class SubmitMindForgeScoreResultDto
    {
        public int SavedScore { get; set; }
        public List<MindForgeLeaderboardItemDto> Leaderboard { get; set; } = new();
    }

    public class SubmitMindForgeScoreCommand : IRequest<Result<SubmitMindForgeScoreResultDto>>
    {
        public int? EmployeeId { get; set; }
        public string EmployeeName { get; set; } = "Anonymous Gamer";
        public string GameType { get; set; } = string.Empty;
        public int Score { get; set; }
        public int TimeTakenSeconds { get; set; }
    }

    internal class MindForgeCommandsHandler :
        IRequestHandler<SubmitMindForgeScoreCommand, Result<SubmitMindForgeScoreResultDto>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public MindForgeCommandsHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<SubmitMindForgeScoreResultDto>> Handle(SubmitMindForgeScoreCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.GameType))
            {
                return await Result<SubmitMindForgeScoreResultDto>.FailAsync("Invalid game type.");
            }

            var scoreEntity = new MindForgeScore
            {
                EmployeeId = request.EmployeeId,
                EmployeeName = string.IsNullOrWhiteSpace(request.EmployeeName) ? "Anonymous Gamer" : request.EmployeeName,
                GameType = request.GameType,
                Score = request.Score,
                TimeTakenSeconds = request.TimeTakenSeconds,
                PlayedAt = DateTime.Now
            };

            await _unitOfWork.Repository<MindForgeScore>().AddAsync(scoreEntity);
            await _unitOfWork.Commit(cancellationToken);

            var topLeaderboard = await _unitOfWork.Repository<MindForgeScore>().Entities
                .Where(s => s.GameType == request.GameType)
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.TimeTakenSeconds)
                .Take(10)
                .Select(s => new MindForgeLeaderboardItemDto
                {
                    Id = s.Id,
                    EmployeeName = s.EmployeeName,
                    GameType = s.GameType,
                    Score = s.Score,
                    TimeTakenSeconds = s.TimeTakenSeconds,
                    PlayedAt = s.PlayedAt.ToString("MMM dd, HH:mm")
                })
                .ToListAsync(cancellationToken);

            return await Result<SubmitMindForgeScoreResultDto>.SuccessAsync(new SubmitMindForgeScoreResultDto
            {
                SavedScore = scoreEntity.Score,
                Leaderboard = topLeaderboard
            }, "Score recorded successfully!");
        }
    }
}
