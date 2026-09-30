using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.MindForge.Queries
{
    public class MindForgeDashboardDto
    {
        public List<MindForgeScore> TopLeaderboardScores { get; set; } = new();
        public int TotalGamesPlayed { get; set; }
        public int HighScoreToday { get; set; }
    }

    public class MindForgeLeaderboardItemDto
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string GameType { get; set; } = string.Empty;
        public int Score { get; set; }
        public int TimeTakenSeconds { get; set; }
        public string PlayedAt { get; set; } = string.Empty;
    }

    public class GetMindForgeDashboardQuery : IRequest<MindForgeDashboardDto>
    {
    }

    public class GetMindForgeLeaderboardQuery : IRequest<List<MindForgeLeaderboardItemDto>>
    {
        public string? GameType { get; set; }
        public GetMindForgeLeaderboardQuery(string? gameType = null) => GameType = gameType;
    }

    internal class MindForgeQueriesHandler :
        IRequestHandler<GetMindForgeDashboardQuery, MindForgeDashboardDto>,
        IRequestHandler<GetMindForgeLeaderboardQuery, List<MindForgeLeaderboardItemDto>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public MindForgeQueriesHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<MindForgeDashboardDto> Handle(GetMindForgeDashboardQuery request, CancellationToken cancellationToken)
        {
            await EnsureSeededAsync(cancellationToken);

            var leaderboard = await _unitOfWork.Repository<MindForgeScore>().Entities
                .AsNoTracking()
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.TimeTakenSeconds)
                .Take(10)
                .ToListAsync(cancellationToken);

            var totalPlayed = await _unitOfWork.Repository<MindForgeScore>().Entities.CountAsync(cancellationToken);
            var highScore = leaderboard.Count > 0 ? leaderboard.Max(s => s.Score) : 0;

            return new MindForgeDashboardDto
            {
                TopLeaderboardScores = leaderboard,
                TotalGamesPlayed = totalPlayed,
                HighScoreToday = highScore
            };
        }

        public async Task<List<MindForgeLeaderboardItemDto>> Handle(GetMindForgeLeaderboardQuery request, CancellationToken cancellationToken)
        {
            await EnsureSeededAsync(cancellationToken);

            var query = _unitOfWork.Repository<MindForgeScore>().Entities.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.GameType) && request.GameType != "all")
            {
                query = query.Where(s => s.GameType == request.GameType);
            }

            var scores = await query
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

            return scores;
        }

        private async Task EnsureSeededAsync(CancellationToken cancellationToken)
        {
            try
            {
                var any = await _unitOfWork.Repository<MindForgeScore>().Entities.AnyAsync(cancellationToken);
                if (!any)
                {
                    var seeds = new List<MindForgeScore>
                    {
                        new MindForgeScore { EmployeeName = "Mujimal", GameType = "MemoryMatch", Score = 1200, TimeTakenSeconds = 24, PlayedAt = DateTime.Now },
                        new MindForgeScore { EmployeeName = "Anas Ahamad", GameType = "WordScramble", Score = 980, TimeTakenSeconds = 32, PlayedAt = DateTime.Now },
                        new MindForgeScore { EmployeeName = "Karthikeyan", GameType = "DotNetQuiz", Score = 1150, TimeTakenSeconds = 28, PlayedAt = DateTime.Now },
                        new MindForgeScore { EmployeeName = "Srithar", GameType = "MathChallenge", Score = 1050, TimeTakenSeconds = 30, PlayedAt = DateTime.Now },
                        new MindForgeScore { EmployeeName = "Santhosh", GameType = "MemoryMatch", Score = 920, TimeTakenSeconds = 38, PlayedAt = DateTime.Now }
                    };

                    foreach (var s in seeds)
                    {
                        await _unitOfWork.Repository<MindForgeScore>().AddAsync(s);
                    }
                    await _unitOfWork.Commit(cancellationToken);
                }
            }
            catch
            {
                // Fallback safe ignore if DB schema not migrated yet
            }
        }
    }
}
