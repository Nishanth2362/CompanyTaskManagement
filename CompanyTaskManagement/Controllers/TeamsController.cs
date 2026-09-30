using System;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.Teams.Commands;
using CompanyTaskManagement.Application.Features.Teams.Queries.GetTeamsDashboard;
using CompanyTaskManagement.Domain.Enums;
using CompanyTaskManagement.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CompanyTaskManagement.Controllers
{
    [Authorize]
    public class TeamsController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUserSessionService _sessionService;

        public TeamsController(IMediator mediator, IUserSessionService sessionService)
        {
            _mediator = mediator;
            _sessionService = sessionService;
        }

        // =========================================================
        // INDEX: Teams of Auxinzio Dashboard
        // =========================================================
        public async Task<IActionResult> Index(string search = "", int? selectedTeamId = null, int page = 1, int pageSize = 12)
        {
            var result = await _mediator.Send(new GetTeamsDashboardQuery
            {
                Search = search,
                Page = page,
                PageSize = pageSize
            });

            var data = result.Data ?? new TeamsDashboardData();

            ViewBag.Search = search;
            ViewBag.SelectedTeamId = selectedTeamId;
            ViewBag.AllEmployees = data.AllEmployees;
            ViewBag.AllProjects = data.AllProjects;
            ViewBag.AllReviews = data.AllReviews;

            ViewBag.TotalTeams = data.TotalTeams;
            ViewBag.CurrentPage = data.CurrentPage;
            ViewBag.PageSize = data.PageSize;
            ViewBag.TotalPages = data.TotalPages;
            ViewBag.TotalLeaders = data.TotalLeaders;
            ViewBag.TotalMembers = data.TotalMembers;
            ViewBag.TotalTasks = data.TotalTasks;
            ViewBag.CompletedTasks = data.CompletedTasks;

            ViewBag.IncompleteTasksList = data.IncompleteTasks;
            ViewBag.IncompleteTasks = data.IncompleteTasks.Count;
            ViewBag.IncompleteCount = data.IncompleteTasks.Count;

            ViewBag.IsAdminOrHr = _sessionService.IsAdminOrHr();
            ViewBag.CurrentRole = _sessionService.GetCurrentRole();
            ViewBag.CurrentEmployeeId = _sessionService.GetCurrentEmployeeId();
            ViewBag.CurrentEmployeeName = _sessionService.GetCurrentEmployeeName();

            return View(data.Teams);
        }

        // =========================================================
        // ADMIN / HR: Create New Team
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTeam(string name, string? description, int? teamLeaderId)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["ErrorMessage"] = "Access Denied: Only Admin and HR can create teams.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _mediator.Send(new CreateTeamCommand
            {
                Name = name,
                Description = description,
                TeamLeaderId = teamLeaderId
            });

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Messages.Count > 0 ? result.Messages[0] : "Team created successfully!";
                return RedirectToAction(nameof(Index), new { selectedTeamId = result.Data });
            }

            TempData["ErrorMessage"] = string.Join("; ", result.Messages);
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // ADMIN / HR: Assign / Update Team Leader
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignLeader(int teamId, int? teamLeaderId)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["ErrorMessage"] = "Access Denied: Only Admin and HR can assign team leaders.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _mediator.Send(new AssignLeaderCommand
            {
                TeamId = teamId,
                TeamLeaderId = teamLeaderId
            });

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Team Leader updated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
        }

        // =========================================================
        // ADMIN / HR: Add Employee to Team
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTeamMember(int teamId, int employeeId)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["ErrorMessage"] = "Access Denied: Only Admin and HR can add team members.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
            }

            var result = await _mediator.Send(new AddTeamMemberCommand
            {
                TeamId = teamId,
                EmployeeId = employeeId
            });

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Employee added to team successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
        }

        // =========================================================
        // ADMIN / HR: Remove Employee from Team
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveTeamMember(int teamId, int employeeId)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["ErrorMessage"] = "Access Denied: Only Admin and HR can remove team members.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
            }

            var result = await _mediator.Send(new RemoveTeamMemberCommand
            {
                TeamId = teamId,
                EmployeeId = employeeId
            });

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Employee removed from team.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
        }

        // =========================================================
        // TEAM LEADER: Assign Task to Employee
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTask(int teamId, int assignedToEmployeeId, string title, string description, int priority, string? taskDurationType, int? projectId, DateTime? startDate, DateTime? endDate, DateTime? dueDate, int? assignedByLeaderId)
        {
            var result = await _mediator.Send(new AssignTeamTaskCommand
            {
                TeamId = teamId,
                AssignedToEmployeeId = assignedToEmployeeId,
                Title = title,
                Description = description,
                Priority = priority,
                TaskDurationType = taskDurationType,
                ProjectId = projectId,
                StartDate = startDate,
                EndDate = endDate,
                DueDate = dueDate,
                AssignedByLeaderId = assignedByLeaderId
            });

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Messages.Count > 0 ? result.Messages[0] : "Task assigned successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
        }

        // =========================================================
        // EMPLOYEE / LEADER: Update Task Status
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTaskStatus(int taskId, int status, string? incompleteReason = null)
        {
            var result = await _mediator.Send(new UpdateTeamTaskStatusCommand
            {
                TaskId = taskId,
                Status = status,
                IncompleteReason = incompleteReason
            });

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Messages.Count > 0 ? result.Messages[0] : "Task status updated.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = result.Data });
            }

            TempData["ErrorMessage"] = string.Join("; ", result.Messages);
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // EMPLOYEE: Provide Justification for Incomplete / Overdue Task
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProvideJustification(int taskId, string justificationReason)
        {
            var result = await _mediator.Send(new ProvideJustificationCommand
            {
                TaskId = taskId,
                JustificationReason = justificationReason
            });

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Messages.Count > 0 ? result.Messages[0] : "Justification submitted.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = result.Data });
            }

            TempData["ErrorMessage"] = string.Join("; ", result.Messages);
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // TEAM LEADER: Grant Extra Time & Provide Suggestion / Guidance
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantExtraTime(int taskId, DateTime? newEndDate, string? leaderSuggestion)
        {
            var result = await _mediator.Send(new GrantExtraTimeCommand
            {
                TaskId = taskId,
                NewEndDate = newEndDate,
                LeaderSuggestion = leaderSuggestion
            });

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Messages.Count > 0 ? result.Messages[0] : "Extra time granted.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = result.Data });
            }

            TempData["ErrorMessage"] = string.Join("; ", result.Messages);
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // TEAM LEADER / ADMIN: Delete Team Task
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTask(int taskId)
        {
            var result = await _mediator.Send(new DeleteTeamTaskCommand { TaskId = taskId });
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Task deleted successfully.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = result.Data });
            }

            TempData["ErrorMessage"] = string.Join("; ", result.Messages);
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // TEAM LEADER / ADMIN: Submit Performance Review for Team Member
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(int employeeId, int? teamLeaderId, string category, int rating, string comment, string? reviewerRole = "Team Leader", string? reviewerName = null, int? teamId = null)
        {
            var result = await _mediator.Send(new SubmitTeamLeaderReviewCommand
            {
                EmployeeId = employeeId,
                TeamLeaderId = teamLeaderId,
                Category = category,
                Rating = rating,
                Comment = comment,
                ReviewerRole = reviewerRole,
                ReviewerName = reviewerName
            });

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Messages.Count > 0 ? result.Messages[0] : "Review submitted successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
        }
    }
}
