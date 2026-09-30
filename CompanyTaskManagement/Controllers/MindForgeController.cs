using System;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.MindForge.Commands;
using CompanyTaskManagement.Application.Features.MindForge.Queries;
using CompanyTaskManagement.Services;
using CompanyTaskManagement.ViewModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyTaskManagement.Controllers
{
    [Authorize]
    public class MindForgeController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUserSessionService _sessionService;

        public MindForgeController(IMediator mediator, IUserSessionService sessionService)
        {
            _mediator = mediator;
            _sessionService = sessionService;
        }

        // =========================================================
        // GET: /MindForge
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "MindForge – Employee Brain Games";

            var dashboard = await _mediator.Send(new GetMindForgeDashboardQuery());

            var model = new MindForgePageViewModel
            {
                TopLeaderboardScores = dashboard.TopLeaderboardScores,
                TotalGamesPlayed = dashboard.TotalGamesPlayed,
                HighScoreToday = dashboard.HighScoreToday
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
                var empName = _sessionService.GetCurrentEmployeeName();
                var empId = _sessionService.GetCurrentEmployeeId();

                var result = await _mediator.Send(new SubmitMindForgeScoreCommand
                {
                    EmployeeId = empId,
                    EmployeeName = string.IsNullOrWhiteSpace(empName) ? "Anonymous Gamer" : empName,
                    GameType = dto.GameType,
                    Score = dto.Score,
                    TimeTakenSeconds = dto.TimeTakenSeconds
                });

                if (!result.Succeeded)
                {
                    return Json(new { success = false, message = string.Join("; ", result.Messages) });
                }

                return Json(new
                {
                    success = true,
                    message = "Score saved dynamically to database!",
                    savedScore = result.Data.SavedScore,
                    leaderboard = result.Data.Leaderboard
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
            var scores = await _mediator.Send(new GetMindForgeLeaderboardQuery(gameType));
            return Json(new { success = true, leaderboard = scores });
        }
    }
}
