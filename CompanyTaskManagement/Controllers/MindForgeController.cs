using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using CompanyTaskManagement.Services;
using CompanyTaskManagement.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Controllers
{
    public class MindForgeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserSessionService _sessionService;

        public MindForgeController(ApplicationDbContext context, IUserSessionService sessionService)
        {
            _context = context;
            _sessionService = sessionService;
        }

        // =========================================================
        // GET: /MindForge
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "MindForge – Employee Brain Games";

            await EnsureMindForgeTablesCreatedAsync();

            var leaderboard = await _context.MindForgeScores
                .AsNoTracking()
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.TimeTakenSeconds)
                .Take(10)
                .ToListAsync();

            int totalPlayed = await _context.MindForgeScores.CountAsync();
            int highScore = leaderboard.Any() ? leaderboard.Max(s => s.Score) : 0;

            var model = new MindForgePageViewModel
            {
                TopLeaderboardScores = leaderboard,
                TotalGamesPlayed = totalPlayed,
                HighScoreToday = highScore
            };

            return View(model);
        }

        // =========================================================
        // POST: /MindForge/SubmitScore
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> SubmitScore([FromBody] SubmitMindForgeScoreDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.GameType))
            {
                return Json(new { success = false, message = "Invalid score submission data." });
            }

            try
            {
                await EnsureMindForgeTablesCreatedAsync();

                var empName = _sessionService.GetCurrentEmployeeName();
                var empId = _sessionService.GetCurrentEmployeeId();

                var scoreEntity = new MindForgeScore
                {
                    EmployeeId = empId,
                    EmployeeName = string.IsNullOrWhiteSpace(empName) ? "Anonymous Gamer" : empName,
                    GameType = dto.GameType,
                    Score = dto.Score,
                    TimeTakenSeconds = dto.TimeTakenSeconds,
                    PlayedAt = DateTime.Now
                };

                _context.MindForgeScores.Add(scoreEntity);
                await _context.SaveChangesAsync();

                var topLeaderboard = await _context.MindForgeScores
                    .Where(s => s.GameType == dto.GameType)
                    .OrderByDescending(s => s.Score)
                    .ThenBy(s => s.TimeTakenSeconds)
                    .Take(10)
                    .Select(s => new {
                        s.EmployeeName,
                        s.Score,
                        s.TimeTakenSeconds,
                        PlayedAt = s.PlayedAt.ToString("MMM dd, HH:mm")
                    })
                    .ToListAsync();

                return Json(new { 
                    success = true, 
                    message = "Score saved dynamically to database!",
                    savedScore = scoreEntity.Score,
                    leaderboard = topLeaderboard
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Failed to record score in database: {ex.Message}" });
            }
        }

        // =========================================================
        // GET: /MindForge/GetLeaderboard
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetLeaderboard(string? gameType = null)
        {
            await EnsureMindForgeTablesCreatedAsync();

            var query = _context.MindForgeScores.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(gameType) && gameType != "all")
            {
                query = query.Where(s => s.GameType == gameType);
            }

            var scores = await query
                .OrderByDescending(s => s.Score)
                .ThenBy(s => s.TimeTakenSeconds)
                .Take(10)
                .Select(s => new {
                    s.Id,
                    s.EmployeeName,
                    s.GameType,
                    s.Score,
                    s.TimeTakenSeconds,
                    PlayedAt = s.PlayedAt.ToString("MMM dd, HH:mm")
                })
                .ToListAsync();

            return Json(new { success = true, leaderboard = scores });
        }

        private async Task EnsureMindForgeTablesCreatedAsync()
        {
            try
            {
                var sqlQuestionsCleanup = @"
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'MindForgeQuestions')
                    BEGIN
                        DROP TABLE [MindForgeQuestions];
                    END;";

                await _context.Database.ExecuteSqlRawAsync(sqlQuestionsCleanup);

                var sqlScores = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MindForgeScores')
                    BEGIN
                        CREATE TABLE [MindForgeScores] (
                            [Id] INT IDENTITY(1,1) PRIMARY KEY,
                            [EmployeeId] INT NULL,
                            [EmployeeName] NVARCHAR(150) NOT NULL,
                            [GameType] NVARCHAR(100) NOT NULL,
                            [Score] INT NOT NULL DEFAULT 0,
                            [TimeTakenSeconds] INT NOT NULL DEFAULT 0,
                            [PlayedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                        );
                    END;

                    IF NOT EXISTS (SELECT 1 FROM [MindForgeScores])
                    BEGIN
                        INSERT INTO [MindForgeScores] ([EmployeeName], [GameType], [Score], [TimeTakenSeconds]) VALUES
                        ('Mujimal', 'MemoryMatch', 1200, 24),
                        ('Anas Ahamad', 'WordScramble', 980, 32),
                        ('Karthikeyan', 'DotNetQuiz', 1150, 28),
                        ('Srithar', 'MathChallenge', 1050, 30),
                        ('Santhosh', 'MemoryMatch', 920, 38);
                    END;";

                await _context.Database.ExecuteSqlRawAsync(sqlScores);
            }
            catch
            {
                // Self-healing fallback
            }
        }
    }
}
