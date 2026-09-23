using System;
using System.Linq;
using System.Threading.Tasks;
using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using CompanyTaskManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskStatus = CompanyTaskManagement.Models.TaskStatus;

namespace CompanyTaskManagement.Controllers
{
    public class TeamsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserSessionService _sessionService;

        public TeamsController(ApplicationDbContext context, IUserSessionService sessionService)
        {
            _context = context;
            _sessionService = sessionService;
        }

        // =========================================================
        // INDEX: Teams of Auxinzio Dashboard
        // =========================================================
        public async Task<IActionResult> Index(string search = "", int? selectedTeamId = null)
        {
            var teamsQuery = _context.Teams
                .Include(t => t.TeamLeader)
                .Include(t => t.Members)
                    .ThenInclude(m => m.Employee)
                .Include(t => t.TeamTasks)
                    .ThenInclude(tk => tk.AssignedToEmployee)
                .Include(t => t.TeamTasks)
                    .ThenInclude(tk => tk.AssignedByLeader)
                .Include(t => t.TeamTasks)
                    .ThenInclude(tk => tk.Project)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                teamsQuery = teamsQuery.Where(t => t.Name.ToLower().Contains(s) ||
                                                   (t.Description != null && t.Description.ToLower().Contains(s)) ||
                                                   (t.TeamLeader != null && t.TeamLeader.Name.ToLower().Contains(s)));
            }

            var teams = await teamsQuery.OrderBy(t => t.Name).ToListAsync();
            var allEmployees = await _context.Employees.Where(e => e.IsActive).OrderBy(e => e.Name).ToListAsync();
            var cutoff12h = DateTime.UtcNow.AddHours(-12);
            var allReviews = await _context.TeamLeaderReviews
                .Include(r => r.TeamLeader)
                .Include(r => r.Employee)
                .Where(r => r.CreatedAt >= cutoff12h)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var allProjects = await _context.Projects.OrderBy(p => p.ProjectName).ToListAsync();

            ViewBag.Search = search;
            ViewBag.SelectedTeamId = selectedTeamId;
            ViewBag.AllEmployees = allEmployees;
            ViewBag.AllProjects = allProjects;
            ViewBag.AllReviews = allReviews;

            // Summary Stats
            ViewBag.TotalTeams = teams.Count;
            ViewBag.TotalLeaders = teams.Where(t => t.TeamLeaderId.HasValue).Select(t => t.TeamLeaderId).Distinct().Count();
            ViewBag.TotalMembers = await _context.TeamMembers.Select(m => m.EmployeeId).Distinct().CountAsync();
            var allTasks = teams.SelectMany(t => t.TeamTasks).ToList();
            ViewBag.TotalTasks = allTasks.Count;
            ViewBag.CompletedTasks = allTasks.Count(t => t.Status == TeamTaskStatus.Completed);

            var now = DateTime.Now;
            var incompleteTasksList = allTasks.Where(t =>
                t.Status == TeamTaskStatus.NotCompleted ||
                (t.Status != TeamTaskStatus.Completed &&
                 ((t.EndDate.HasValue && t.EndDate.Value <= now) || (t.DueDate.HasValue && t.DueDate.Value <= now)))
            ).OrderByDescending(t => t.CreatedAt).ToList();

            ViewBag.IncompleteTasksList = incompleteTasksList;
            ViewBag.IncompleteTasks = incompleteTasksList.Count;

            return View(teams);
        }

        // =========================================================
        // ADMIN / HR: Create Team
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

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["ErrorMessage"] = "Team name cannot be empty.";
                return RedirectToAction(nameof(Index));
            }

            var team = new Team
            {
                Name = name.Trim(),
                Description = description?.Trim(),
                TeamLeaderId = teamLeaderId > 0 ? teamLeaderId : null,
                CreatedAt = DateTime.UtcNow
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Team '{team.Name}' created successfully!";
            return RedirectToAction(nameof(Index), new { selectedTeamId = team.Id });
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

            var team = await _context.Teams.FindAsync(teamId);
            if (team == null)
            {
                TempData["ErrorMessage"] = "Team not found.";
                return RedirectToAction(nameof(Index));
            }

            team.TeamLeaderId = (teamLeaderId.HasValue && teamLeaderId.Value > 0) ? teamLeaderId.Value : null;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Team Leader updated successfully.";
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

            var team = await _context.Teams.FindAsync(teamId);
            if (team == null)
            {
                TempData["ErrorMessage"] = "Team not found.";
                return RedirectToAction(nameof(Index));
            }

            var exists = await _context.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.EmployeeId == employeeId);
            if (exists)
            {
                TempData["ErrorMessage"] = "Employee is already a member of this team.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
            }

            var member = new TeamMember
            {
                TeamId = teamId,
                EmployeeId = employeeId,
                AssignedAt = DateTime.UtcNow
            };

            _context.TeamMembers.Add(member);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Employee added to team successfully.";
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

            var member = await _context.TeamMembers.FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.EmployeeId == employeeId);
            if (member != null)
            {
                _context.TeamMembers.Remove(member);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Employee removed from team.";
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
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                TempData["ErrorMessage"] = "Task title and description are required.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
            }

            var team = await _context.Teams.FindAsync(teamId);
            if (team == null)
            {
                TempData["ErrorMessage"] = "Team not found.";
                return RedirectToAction(nameof(Index));
            }

            var isMember = await _context.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.EmployeeId == assignedToEmployeeId);
            if (!isMember && team.TeamLeaderId != assignedToEmployeeId)
            {
                TempData["ErrorMessage"] = "Team Leaders can only assign tasks to their own allocated team members.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
            }

            var leaderId = (assignedByLeaderId.HasValue && assignedByLeaderId.Value > 0)
                ? assignedByLeaderId.Value
                : team.TeamLeaderId;

            var task = new TeamTask
            {
                TeamId = teamId,
                AssignedByLeaderId = leaderId,
                AssignedToEmployeeId = assignedToEmployeeId,
                Title = title.Trim(),
                Description = description.Trim(),
                Priority = Enum.IsDefined(typeof(TaskPriority), priority) ? (TaskPriority)priority : TaskPriority.Medium,
                Status = TeamTaskStatus.Pending,
                TaskDurationType = string.IsNullOrWhiteSpace(taskDurationType) ? "Full Day" : taskDurationType.Trim(),
                ProjectId = (projectId.HasValue && projectId.Value > 0) ? projectId.Value : null,
                StartDate = startDate,
                EndDate = endDate,
                DueDate = dueDate ?? endDate,
                CreatedAt = DateTime.UtcNow
            };

            _context.TeamTasks.Add(task);
            await _context.SaveChangesAsync();

            // Automatically sync & create task in the main Tasks page (/Tasks)
            var mainTask = new TaskItem
            {
                TaskName = title.Trim(),
                Description = description.Trim(),
                Priority = Enum.IsDefined(typeof(TaskPriority), priority) ? (TaskPriority)priority : TaskPriority.Medium,
                Status = CompanyTaskManagement.Models.TaskStatus.ToDo,
                ProjectId = (projectId.HasValue && projectId.Value > 0) ? projectId.Value : null,
                StartDate = startDate ?? DateTime.Now,
                EndDate = endDate ?? dueDate,
                DueDate = dueDate ?? endDate,
                CreatedAt = DateTime.Now,
                Progress = 0
            };

            _context.Tasks.Add(mainTask);
            await _context.SaveChangesAsync();

            if (assignedToEmployeeId > 0)
            {
                _context.TaskEmployees.Add(new TaskEmployee
                {
                    TaskId = mainTask.Id,
                    EmployeeId = assignedToEmployeeId
                });
            }

            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Name.Contains("Auxinzio") || c.Name.Contains("Auxin"))
                          ?? await _context.Companies.FirstOrDefaultAsync();

            if (company != null)
            {
                _context.TaskCompanies.Add(new TaskCompany
                {
                    TaskId = mainTask.Id,
                    CompanyId = company.Id
                });
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Task assigned to team employee and updated on main Tasks board successfully!";
            return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
        }

        // =========================================================
        // EMPLOYEE / TL: Update Task Status & Report Reason
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateTaskStatus(int taskId, int status, string? incompleteReason)
        {
            var task = await _context.TeamTasks.FindAsync(taskId);
            if (task == null)
            {
                TempData["ErrorMessage"] = "Task not found.";
                return RedirectToAction(nameof(Index));
            }

            var newStatus = (TeamTaskStatus)status;

            var isOverdue = (task.EndDate.HasValue && task.EndDate.Value <= DateTime.Now) || (task.DueDate.HasValue && task.DueDate.Value <= DateTime.Now) || task.Status == TeamTaskStatus.NotCompleted;
            var hasJustification = !string.IsNullOrWhiteSpace(task.IncompleteReason) || !string.IsNullOrWhiteSpace(incompleteReason);

            if (isOverdue && !hasJustification && newStatus != TeamTaskStatus.NotCompleted)
            {
                TempData["ErrorMessage"] = "Overdue Task Locked: Please click 'Justification' to provide your delay reason before updating status.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = task.TeamId });
            }

            task.Status = newStatus;

            if (newStatus == TeamTaskStatus.NotCompleted)
            {
                if (string.IsNullOrWhiteSpace(incompleteReason))
                {
                    TempData["ErrorMessage"] = "Reason is required when marking a task as Not Completed.";
                    return RedirectToAction(nameof(Index), new { selectedTeamId = task.TeamId });
                }
                task.IncompleteReason = incompleteReason.Trim();

                var mainTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskName == task.Title);
                if (mainTask != null)
                {
                    mainTask.DelayReason = incompleteReason.Trim();
                }
            }
            else if (newStatus == TeamTaskStatus.Completed)
            {
                task.CompletedAt = DateTime.UtcNow;
                task.IncompleteReason = null;

                var mainTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskName == task.Title);
                if (mainTask != null)
                {
                    mainTask.Status = TaskStatus.Completed;
                    mainTask.Progress = 100;
                    mainTask.DelayReason = null;
                    mainTask.EndDate = DateTime.Now;
                }
                else
                {
                    var newMainTask = new TaskItem
                    {
                        TaskName = task.Title,
                        Description = task.Description,
                        Priority = task.Priority,
                        Status = TaskStatus.Completed,
                        ProjectId = task.ProjectId,
                        StartDate = task.StartDate ?? task.CreatedAt,
                        EndDate = DateTime.Now,
                        DueDate = task.DueDate ?? task.EndDate,
                        CreatedAt = task.CreatedAt,
                        Progress = 100,
                        DelayReason = null
                    };
                    _context.Tasks.Add(newMainTask);
                    await _context.SaveChangesAsync();

                    if (task.AssignedToEmployeeId > 0)
                    {
                        _context.TaskEmployees.Add(new TaskEmployee
                        {
                            TaskId = newMainTask.Id,
                            EmployeeId = task.AssignedToEmployeeId
                        });
                    }
                }
            }
            else if (newStatus == TeamTaskStatus.InProgress)
            {
                task.IncompleteReason = null;
                var mainTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskName == task.Title);
                if (mainTask != null)
                {
                    mainTask.Status = TaskStatus.InProgress;
                    if (mainTask.Progress < 50) mainTask.Progress = 50;
                    mainTask.DelayReason = null;
                }
            }
            else
            {
                task.IncompleteReason = null;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Task status updated to '{newStatus}'.";
            return RedirectToAction(nameof(Index), new { selectedTeamId = task.TeamId });
        }

        // =========================================================
        // EMPLOYEE: Provide Justification for Incomplete / Overdue Task
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProvideJustification(int taskId, string justificationReason)
        {
            if (string.IsNullOrWhiteSpace(justificationReason))
            {
                TempData["ErrorMessage"] = "Justification reason is required.";
                return RedirectToAction(nameof(Index));
            }

            var task = await _context.TeamTasks.FindAsync(taskId);
            if (task == null)
            {
                TempData["ErrorMessage"] = "Task not found.";
                return RedirectToAction(nameof(Index));
            }

            task.Status = TeamTaskStatus.NotCompleted;
            task.IncompleteReason = justificationReason.Trim();

            // Sync justification note directly to main Tasks sheet (/Task)
            var mainTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskName == task.Title);
            if (mainTask != null)
            {
                mainTask.DelayReason = justificationReason.Trim();
                if (mainTask.Status != TaskStatus.Completed)
                {
                    mainTask.Status = TaskStatus.InProgress;
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Justification submitted for task '{task.Title}'.";
            return RedirectToAction(nameof(Index), new { selectedTeamId = task.TeamId });
        }

        // =========================================================
        // TEAM LEADER: Grant Extra Time & Provide Suggestion / Guidance
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantExtraTime(int taskId, DateTime? newEndDate, string? leaderSuggestion)
        {
            var task = await _context.TeamTasks
                .Include(t => t.Team)
                .FirstOrDefaultAsync(t => t.Id == taskId);

            if (task == null)
            {
                TempData["ErrorMessage"] = "Task not found.";
                return RedirectToAction(nameof(Index));
            }

            var currentEmpId = _sessionService.GetCurrentEmployeeId();
            var isAdmin = _sessionService.IsAdmin();
            var isTeamLeader = task.Team != null && task.Team.TeamLeaderId.HasValue && currentEmpId.HasValue && task.Team.TeamLeaderId.Value == currentEmpId.Value;

            if (!isAdmin && !isTeamLeader)
            {
                TempData["ErrorMessage"] = "Only the assigned Team Leader of this team (or Admin) can grant extra time or provide suggestions.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = task.TeamId });
            }

            if (newEndDate.HasValue)
            {
                task.EndDate = newEndDate.Value;
                task.DueDate = newEndDate.Value;
                // If extra time is granted in future, resume status to In Progress
                if (newEndDate.Value > DateTime.Now && task.Status == TeamTaskStatus.NotCompleted)
                {
                    task.Status = TeamTaskStatus.InProgress;
                }
            }

            if (!string.IsNullOrWhiteSpace(leaderSuggestion))
            {
                task.LeaderSuggestion = leaderSuggestion.Trim();
            }

            // Sync extra time and leader guidance directly to main Tasks sheet (/Task)
            var targetMainTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskName == task.Title);
            if (targetMainTask != null)
            {
                if (newEndDate.HasValue)
                {
                    targetMainTask.EndDate = newEndDate.Value;
                    targetMainTask.DueDate = newEndDate.Value;
                }
                if (!string.IsNullOrWhiteSpace(leaderSuggestion))
                {
                    targetMainTask.ErrorDetails = (string.IsNullOrWhiteSpace(targetMainTask.ErrorDetails) ? "" : targetMainTask.ErrorDetails + " | ") + $"Leader Guidance: {leaderSuggestion.Trim()}";
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Extra time / leader guidance saved for task '{task.Title}'.";
            return RedirectToAction(nameof(Index), new { selectedTeamId = task.TeamId });
        }

        // =========================================================
        // REVIEWS & APPRECIATIONS: Given by Team Leaders to Employees
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLeaderReview(int teamLeaderId, int employeeId, string? category, int rating, string comments, int? taskId)
        {
            if (teamLeaderId <= 0 || employeeId <= 0 || string.IsNullOrWhiteSpace(comments))
            {
                TempData["ErrorMessage"] = "Team Leader, Employee, and Appreciation Comments are required.";
                return RedirectToAction(nameof(Index));
            }

            var leader = await _context.Employees.FindAsync(teamLeaderId);
            var employee = await _context.Employees.FindAsync(employeeId);

            if (leader == null || employee == null)
            {
                TempData["ErrorMessage"] = "Selected Team Leader or Employee was not found.";
                return RedirectToAction(nameof(Index));
            }

            var review = new TeamLeaderReview
            {
                TeamLeaderId = teamLeaderId,
                LeaderName = leader.Name,
                ReviewerName = leader.Name,
                ReviewerRole = "Team Leader",
                EmployeeId = employeeId,
                EmployeeName = employee.Name,
                Category = string.IsNullOrWhiteSpace(category) ? "🌟 Outstanding Performance" : category.Trim(),
                Rating = Math.Clamp(rating, 1, 5),
                Comments = comments.Trim(),
                Feedback = comments.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.TeamLeaderReviews.Add(review);

            string taskMsg = "";
            if (taskId.HasValue && taskId.Value > 0)
            {
                var completedTask = await _context.TeamTasks.FindAsync(taskId.Value);
                if (completedTask != null)
                {
                    taskMsg = $" Completed task '{completedTask.Title}' has been reviewed and removed from the active board.";

                    var mainTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskName == completedTask.Title);
                    if (mainTask != null)
                    {
                        mainTask.Status = TaskStatus.Completed;
                        mainTask.Progress = 100;
                        mainTask.DelayReason = null;
                        mainTask.EndDate = DateTime.Now;
                    }
                    else
                    {
                        var newMainTask = new TaskItem
                        {
                            TaskName = completedTask.Title,
                            Description = completedTask.Description,
                            Priority = completedTask.Priority,
                            Status = TaskStatus.Completed,
                            ProjectId = completedTask.ProjectId,
                            StartDate = completedTask.StartDate ?? completedTask.CreatedAt,
                            EndDate = DateTime.Now,
                            DueDate = completedTask.DueDate ?? completedTask.EndDate,
                            CreatedAt = completedTask.CreatedAt,
                            Progress = 100,
                            DelayReason = null
                        };
                        _context.Tasks.Add(newMainTask);
                        await _context.SaveChangesAsync();

                        if (completedTask.AssignedToEmployeeId > 0)
                        {
                            _context.TaskEmployees.Add(new TaskEmployee
                            {
                                TaskId = newMainTask.Id,
                                EmployeeId = completedTask.AssignedToEmployeeId
                            });
                        }
                    }

                    _context.TeamTasks.Remove(completedTask);
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Appreciation & Review for '{employee.Name}' submitted successfully by Team Leader {leader.Name}!{taskMsg}";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // ADMIN / HR: Delete Team
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTeam(int id)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["ErrorMessage"] = "Access Denied: Only Admin and HR can delete teams.";
                return RedirectToAction(nameof(Index));
            }

            var team = await _context.Teams.FindAsync(id);
            if (team != null)
            {
                _context.Teams.Remove(team);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Team deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // ADMIN / TL: Delete Task
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTask(int id)
        {
            var task = await _context.TeamTasks.FindAsync(id);
            if (task != null)
            {
                var teamId = task.TeamId;
                _context.TeamTasks.Remove(task);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Task deleted successfully.";
                return RedirectToAction(nameof(Index), new { selectedTeamId = teamId });
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
