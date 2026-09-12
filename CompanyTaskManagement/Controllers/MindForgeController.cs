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

            var wordScrambles = await _context.MindForgeQuestions
                .Where(q => q.GameType == "WordScramble")
                .AsNoTracking()
                .ToListAsync();

            var dotnetQuizzes = await _context.MindForgeQuestions
                .Where(q => q.GameType == "DotNetQuiz")
                .AsNoTracking()
                .ToListAsync();

            var mathChallenges = await _context.MindForgeQuestions
                .Where(q => q.GameType == "MathChallenge")
                .AsNoTracking()
                .ToListAsync();

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
                WordScrambles = wordScrambles,
                DotNetQuizzes = dotnetQuizzes,
                MathChallenges = mathChallenges,
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
                var sqlQuestions = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MindForgeQuestions')
                    BEGIN
                        CREATE TABLE [MindForgeQuestions] (
                            [Id] INT IDENTITY(1,1) PRIMARY KEY,
                            [GameType] NVARCHAR(100) NOT NULL,
                            [QuestionText] NVARCHAR(MAX) NOT NULL,
                            [ScrambledOrSnippet] NVARCHAR(MAX) NULL,
                            [CorrectAnswer] NVARCHAR(500) NOT NULL,
                            [OptionA] NVARCHAR(500) NULL,
                            [OptionB] NVARCHAR(500) NULL,
                            [OptionC] NVARCHAR(500) NULL,
                            [OptionD] NVARCHAR(500) NULL,
                            [Explanation] NVARCHAR(MAX) NULL,
                            [Difficulty] NVARCHAR(50) NOT NULL DEFAULT 'Medium',
                            [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE()
                        );

                        INSERT INTO [MindForgeQuestions] ([GameType], [QuestionText], [ScrambledOrSnippet], [CorrectAnswer], [OptionA], [OptionB], [OptionC], [OptionD], [Explanation], [Difficulty]) VALUES
                        ('WordScramble', 'High-performance web framework for modern cloud apps', 'TESPANOR', 'ASP NET CORE', NULL, NULL, NULL, NULL, 'ASP.NET Core is cross-platform and high-performance.', 'Medium'),
                        ('WordScramble', 'Key Microsoft programming language for .NET development', 'PAHRCS', 'CSHARP', NULL, NULL, NULL, NULL, 'C# is the primary language for .NET ecosystem.', 'Easy'),
                        ('WordScramble', 'High-speed caching and in-memory key-value data store', 'SIRED', 'REDIS', NULL, NULL, NULL, NULL, 'Redis provides fast distributed caching.', 'Medium'),
                        ('WordScramble', 'Object-relational mapper for .NET data access', 'TYITEN', 'ENTITY FRAMEWORK', NULL, NULL, NULL, NULL, 'Entity Framework Core simplifies SQL operations.', 'Medium'),
                        ('WordScramble', 'Asynchronous programming keyword in C#', 'IWAAT', 'AWAIT', NULL, NULL, NULL, NULL, 'await yields execution until task completes.', 'Easy'),
                        ('DotNetQuiz', 'What is the output of the following async code snippet?', 'async Task<int> CalculateAsync() { await Task.Delay(10); return 42; }', '42', '0', '42', 'Task<int>', 'Compiler Error', 'Awaiting Task.Delay returns the result 42.', 'Medium'),
                        ('DotNetQuiz', 'Which LINQ method defers execution until enumerated?', 'var q = db.Tasks.Where(t => t.Progress > 50);', 'Where', 'ToList()', 'Count()', 'Where', 'FirstOrDefault()', 'Where builds an IQueryable with deferred execution.', 'Medium'),
                        ('DotNetQuiz', 'What keyword handles resource disposal automatically?', 'using var stream = File.OpenRead(path);', 'using', 'using', 'try-finally', 'dispose', 'auto', 'C# 8 using declarations dispose objects at scope exit.', 'Easy'),
                        ('MathChallenge', 'What is 15 * 8 - 35?', NULL, '85', '75', '85', '95', '105', '15 * 8 = 120, 120 - 35 = 85.', 'Easy'),
                        ('MathChallenge', 'What is the square root of 256 + 14?', NULL, '30', '28', '30', '32', '34', 'sqrt(256) = 16, 16 + 14 = 30.', 'Medium');
                    END;";

                await _context.Database.ExecuteSqlRawAsync(sqlQuestions);

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
