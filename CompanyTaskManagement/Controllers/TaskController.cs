using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using CompanyTaskManagement.Services;
using CompanyTaskManagement.View_Model;
using CompanyTaskManagement.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TaskStatus = CompanyTaskManagement.Models.TaskStatus;

namespace CompanyTaskManagement.Controllers
{
    public class TaskController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly IUserSessionService _sessionService;

        public TaskController(
            ApplicationDbContext context, 
            IEmailService emailService, 
            IWebHostEnvironment environment,
            IUserSessionService sessionService)
        {
            _context = context;
            _emailService = emailService;
            _environment = environment;
            _sessionService = sessionService;
        }

        // =========================================================
        // INDEX
        // =========================================================

        // GET: /Task
        public async Task<IActionResult> Index(string filter = "all")
        {
            await ProcessUncompletedTaskRolloverAsync();

            var today = DateTime.Today;
            var allTasksList = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .ThenByDescending(t => t.Priority)
                .ToListAsync();

            var now = DateTime.Now;
            int allCount = allTasksList.Count;
            int todayCount = allTasksList.Count(t => (t.StartDate.HasValue ? t.StartDate.Value.Date == today : t.CreatedAt.Date == today));
            int inProgressCount = allTasksList.Count(t => t.Status == TaskStatus.InProgress);
            int overdueCount = allTasksList.Count(t => t.Status != TaskStatus.Completed && ((t.EndDate.HasValue && t.EndDate.Value <= now) || (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) || !string.IsNullOrEmpty(t.DelayReason)));
            int completedCount = allTasksList.Count(t => t.Status == TaskStatus.Completed);
            int urgentCount = allTasksList.Count(t => t.Priority == TaskPriority.Urgent || t.Priority == TaskPriority.High);

            ViewBag.CurrentFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.ToLower();
            ViewBag.AllCount = allCount;
            ViewBag.TodayCount = todayCount;
            ViewBag.InProgressCount = inProgressCount;
            ViewBag.OverdueCount = overdueCount;
            ViewBag.CompletedCount = completedCount;
            ViewBag.UrgentCount = urgentCount;

            // Role Context for Views
            ViewBag.CurrentRole = _sessionService.GetCurrentRole();
            ViewBag.IsAdminOrHr = _sessionService.IsAdminOrHr();
            ViewBag.IsEmployee = _sessionService.IsEmployee();
            ViewBag.CurrentEmployeeId = _sessionService.GetCurrentEmployeeId();
            ViewBag.CurrentEmployeeName = _sessionService.GetCurrentEmployeeName();

            var currentEmpId = _sessionService.GetCurrentEmployeeId();
            if (_sessionService.IsEmployee() && currentEmpId.HasValue)
            {
                var empId = currentEmpId.Value;
                ViewBag.MyTasksCount = allTasksList.Count(t => t.TaskEmployees.Any(te => te.EmployeeId == empId));
            }

            return View(allTasksList);
        }

        // =========================================================
        // UNCOMPLETED TASK AUTO-ROLLOVER LOGIC
        // =========================================================

        private async Task ProcessUncompletedTaskRolloverAsync()
        {
            try
            {
                var today = DateTime.Today;
                var now = DateTime.Now;

                // 1. Sync overdue TeamTasks into main Tasks table if missing
                var overdueTeamTasks = await _context.TeamTasks
                    .Include(tt => tt.AssignedToEmployee)
                    .Include(tt => tt.Project)
                    .Where(tt => tt.Status == TeamTaskStatus.NotCompleted || 
                                 (tt.Status != TeamTaskStatus.Completed && 
                                  ((tt.EndDate.HasValue && tt.EndDate.Value <= now) || 
                                   (tt.DueDate.HasValue && tt.DueDate.Value <= now))))
                    .ToListAsync();

                foreach (var tt in overdueTeamTasks)
                {
                    var matchingTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskName == tt.Title);
                    if (matchingTask == null)
                    {
                        matchingTask = new TaskItem
                        {
                            TaskName = tt.Title,
                            Description = tt.Description,
                            Priority = tt.Priority,
                            Status = TaskStatus.ToDo,
                            ProjectId = tt.ProjectId,
                            StartDate = tt.StartDate ?? tt.CreatedAt,
                            EndDate = tt.EndDate ?? tt.DueDate,
                            DueDate = tt.DueDate ?? tt.EndDate,
                            CreatedAt = tt.CreatedAt,
                            Progress = 0,
                            DelayReason = !string.IsNullOrWhiteSpace(tt.IncompleteReason) 
                                ? tt.IncompleteReason 
                                : $"Overdue Team Task (End: {(tt.EndDate.HasValue ? tt.EndDate.Value.ToString("MMM dd, h:mm tt") : "Elapsed")})"
                        };
                        _context.Tasks.Add(matchingTask);
                        await _context.SaveChangesAsync();

                        if (tt.AssignedToEmployeeId > 0)
                        {
                            _context.TaskEmployees.Add(new TaskEmployee
                            {
                                TaskId = matchingTask.Id,
                                EmployeeId = tt.AssignedToEmployeeId
                            });
                            await _context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(tt.IncompleteReason) && string.IsNullOrWhiteSpace(matchingTask.DelayReason))
                        {
                            matchingTask.DelayReason = tt.IncompleteReason;
                        }
                        else if (string.IsNullOrWhiteSpace(matchingTask.DelayReason) && matchingTask.Status != TaskStatus.Completed)
                        {
                            matchingTask.DelayReason = $"Overdue Team Task (End: {(tt.EndDate.HasValue ? tt.EndDate.Value.ToString("MMM dd, h:mm tt") : "Elapsed")})";
                        }
                    }
                }

                // 2. Sync Completed Team Tasks & Team Leader Reviews into Tasks table
                var completedTeamTasks = await _context.TeamTasks
                    .Include(tt => tt.AssignedToEmployee)
                    .Include(tt => tt.Project)
                    .Where(tt => tt.Status == TeamTaskStatus.Completed)
                    .ToListAsync();

                foreach (var ctt in completedTeamTasks)
                {
                    var mTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskName == ctt.Title);
                    if (mTask != null)
                    {
                        mTask.Status = TaskStatus.Completed;
                        mTask.Progress = 100;
                        mTask.DelayReason = null;
                        if (ctt.CompletedAt.HasValue)
                        {
                            mTask.EndDate = ctt.CompletedAt.Value;
                        }
                    }
                    else
                    {
                        var newCompletedTask = new TaskItem
                        {
                            TaskName = ctt.Title,
                            Description = ctt.Description,
                            Priority = ctt.Priority,
                            Status = TaskStatus.Completed,
                            ProjectId = ctt.ProjectId,
                            StartDate = ctt.StartDate ?? ctt.CreatedAt,
                            EndDate = ctt.CompletedAt ?? ctt.EndDate ?? DateTime.Now,
                            DueDate = ctt.DueDate ?? ctt.EndDate,
                            CreatedAt = ctt.CreatedAt,
                            Progress = 100,
                            DelayReason = null
                        };
                        _context.Tasks.Add(newCompletedTask);
                        await _context.SaveChangesAsync();

                        if (ctt.AssignedToEmployeeId > 0)
                        {
                            _context.TaskEmployees.Add(new TaskEmployee
                            {
                                TaskId = newCompletedTask.Id,
                                EmployeeId = ctt.AssignedToEmployeeId
                            });
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                // 3. Sync Team Leader Reviews (which record completed tasks approved by Team Leaders)
                var leaderReviews = await _context.TeamLeaderReviews.ToListAsync();
                foreach (var rev in leaderReviews)
                {
                    string comments = rev.Comments ?? rev.Feedback ?? "";
                    string taskTitle = "";
                    if (comments.Contains("completing task: \""))
                    {
                        int startIdx = comments.IndexOf("completing task: \"") + "completing task: \"".Length;
                        int endIdx = comments.IndexOf("\"", startIdx);
                        if (endIdx > startIdx)
                        {
                            taskTitle = comments.Substring(startIdx, endIdx - startIdx).Trim();
                        }
                    }
                    else if (comments.Contains("completing task:"))
                    {
                        int startIdx = comments.IndexOf("completing task:") + "completing task:".Length;
                        int endIdx = comments.IndexOf(".", startIdx);
                        if (endIdx > startIdx)
                        {
                            taskTitle = comments.Substring(startIdx, endIdx - startIdx).Trim().Trim('"');
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(taskTitle))
                    {
                        var matchingTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskName == taskTitle || t.TaskName.Contains(taskTitle) || taskTitle.Contains(t.TaskName));
                        if (matchingTask != null)
                        {
                            matchingTask.Status = TaskStatus.Completed;
                            matchingTask.Progress = 100;
                            matchingTask.DelayReason = null;
                            if (matchingTask.EndDate == null || matchingTask.EndDate > rev.CreatedAt)
                            {
                                matchingTask.EndDate = rev.CreatedAt;
                            }
                        }
                        else
                        {
                            var newRevTask = new TaskItem
                            {
                                TaskName = taskTitle,
                                Description = comments,
                                Priority = TaskPriority.High,
                                Status = TaskStatus.Completed,
                                StartDate = rev.CreatedAt.AddHours(-4),
                                EndDate = rev.CreatedAt,
                                DueDate = rev.CreatedAt,
                                CreatedAt = rev.CreatedAt.AddHours(-4),
                                Progress = 100,
                                DelayReason = null
                            };
                            _context.Tasks.Add(newRevTask);
                            await _context.SaveChangesAsync();

                            if (rev.EmployeeId.HasValue && rev.EmployeeId.Value > 0)
                            {
                                _context.TaskEmployees.Add(new TaskEmployee
                                {
                                    TaskId = newRevTask.Id,
                                    EmployeeId = rev.EmployeeId.Value
                                });
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }

                // 2. Process Auto-Rollover for past incomplete tasks
                var pastIncompleteTasks = await _context.Tasks
                    .Where(t => t.Status != TaskStatus.Completed && 
                                ((t.EndDate.HasValue && t.EndDate.Value <= now) || 
                                 (t.DueDate.HasValue && t.DueDate.Value.Date < today)))
                    .ToListAsync();

                if (pastIncompleteTasks.Any())
                {
                    foreach (var task in pastIncompleteTasks)
                    {
                        var prevDateStr = (task.EndDate ?? task.DueDate).HasValue ? (task.EndDate ?? task.DueDate)!.Value.ToString("MMM dd, h:mm tt") : "Previous Date";
                        if (string.IsNullOrWhiteSpace(task.DelayReason))
                        {
                            task.DelayReason = $"End time elapsed ({prevDateStr}). Pending completion/justification.";
                        }

                        var empIds = await _context.TaskEmployees.Where(te => te.TaskId == task.Id).Select(te => te.EmployeeId).ToListAsync();
                        
                        _context.TaskActivityLogs.Add(new TaskActivityLog
                        {
                            TaskId = task.Id,
                            TaskName = task.TaskName,
                            EmployeeId = empIds.FirstOrDefault(),
                            EmployeeName = "System Auto-Rollover",
                            ActionType = "Overdue / Delay Flagged",
                            OldStatus = task.Status.ToString(),
                            NewStatus = task.Status.ToString(),
                            Progress = task.Progress,
                            DelayReason = task.DelayReason,
                            ErrorDetails = $"Task end time passed ({prevDateStr}) and flagged for overdue follow-up on ({today:MMM dd, yyyy}).",
                            LoggedAt = DateTime.Now
                        });
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing uncompleted task rollover: {ex.Message}");
            }
        }

        // =========================================================
        // CREATE - GET
        // =========================================================

        // GET: /Task/Create
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["Error"] = "Access Denied: Task creation and assignment is restricted exclusively to Admin and HR.";
                return RedirectToAction(nameof(Index));
            }

            var model = new TaskViewModel
            {
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(7),
                DueDate = DateTime.Now.AddDays(7),
                Priority = TaskPriority.Medium,
                Status = TaskStatus.ToDo,
                Progress = 0
            };

            await LoadCompaniesAndEmployees(model);

            return View(model);
        }

        // =========================================================
        // CREATE - POST
        // =========================================================

        // POST: /Task/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaskViewModel model)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["Error"] = "Access Denied: Task creation and assignment is restricted exclusively to Admin and HR.";
                return RedirectToAction(nameof(Index));
            }

            model.SelectedCompanyIds = model.SelectedCompanyIds?.Distinct().ToList() ?? new List<int>();
            model.SelectedEmployeeIds = model.SelectedEmployeeIds?.Distinct().ToList() ?? new List<int>();

            if (!model.SelectedCompanyIds.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedCompanyIds), "Please select at least one company.");
            }

            if (!model.SelectedEmployeeIds.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedEmployeeIds), "Please select at least one employee.");
            }

            if (!ModelState.IsValid)
            {
                await LoadCompaniesAndEmployees(model);
                return View(model);
            }

            if (model.Status == TaskStatus.Completed && model.Progress < 100)
            {
                model.Progress = 100;
            }

            string? uploadedScreenshotPath = null;
            if (model.ErrorScreenshotFile != null && model.ErrorScreenshotFile.Length > 0)
            {
                uploadedScreenshotPath = await SaveScreenshotFileAsync(model.ErrorScreenshotFile);
            }

            var task = new TaskItem
            {
                TaskName = model.TaskName,
                Description = model.Description,
                Priority = model.Priority,
                Status = model.Status,
                ProjectId = model.ProjectId,
                StartDate = model.StartDate ?? DateTime.Now,
                EndDate = model.EndDate ?? model.DueDate,
                DueDate = model.EndDate ?? model.DueDate,
                Progress = model.Progress,
                DelayReason = model.DelayReason,
                ErrorDetails = model.ErrorDetails,
                ErrorScreenshotPath = uploadedScreenshotPath,
                CreatedAt = DateTime.Now
            };

            _context.Tasks.Add(task);
            await _context.SaveChangesAsync();

            foreach (var companyId in model.SelectedCompanyIds)
            {
                _context.TaskCompanies.Add(new TaskCompany { TaskId = task.Id, CompanyId = companyId });
            }

            foreach (var employeeId in model.SelectedEmployeeIds)
            {
                _context.TaskEmployees.Add(new TaskEmployee { TaskId = task.Id, EmployeeId = employeeId });
            }

            await _context.SaveChangesAsync();

            await SyncTaskToTeamTasksAsync(task, model.SelectedEmployeeIds);

            await RecordTaskActivityLogsAsync(task.Id, task.TaskName, model.SelectedEmployeeIds, "Task Created", null, task.Status.ToString(), task.Progress, task.DelayReason, task.ErrorDetails, uploadedScreenshotPath);

            TempData["Success"] = $"Task \"{task.TaskName}\" created successfully.";

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // EDIT - GET
        // =========================================================

        // GET: /Task/Edit/1
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["Error"] = "Access Denied: Only Admin and HR can edit and reassign tasks.";
                return RedirectToAction(nameof(Index));
            }

            var task = await _context.Tasks
                .Include(t => t.TaskCompanies)
                .Include(t => t.TaskEmployees)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            var model = new TaskViewModel
            {
                Id = task.Id,
                TaskName = task.TaskName,
                Description = task.Description,
                Priority = task.Priority,
                Status = task.Status,
                ProjectId = task.ProjectId,
                StartDate = task.StartDate ?? task.CreatedAt,
                EndDate = task.EndDate ?? task.DueDate,
                DueDate = task.EndDate ?? task.DueDate,
                Progress = task.Progress,
                DelayReason = task.DelayReason,
                ErrorDetails = task.ErrorDetails,
                ErrorScreenshotPath = task.ErrorScreenshotPath,
                SelectedCompanyIds = task.TaskCompanies.Select(tc => tc.CompanyId).ToList(),
                SelectedEmployeeIds = task.TaskEmployees.Select(te => te.EmployeeId).ToList()
            };

            await LoadCompaniesAndEmployees(model);

            return View(model);
        }

        // =========================================================
        // EDIT - POST
        // =========================================================

        // POST: /Task/Edit/1
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TaskViewModel model)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["Error"] = "Access Denied: Only Admin and HR can edit and reassign tasks.";
                return RedirectToAction(nameof(Index));
            }

            if (id != model.Id)
            {
                return BadRequest();
            }

            model.SelectedCompanyIds = model.SelectedCompanyIds?.Distinct().ToList() ?? new List<int>();
            model.SelectedEmployeeIds = model.SelectedEmployeeIds?.Distinct().ToList() ?? new List<int>();

            if (!model.SelectedCompanyIds.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedCompanyIds), "Please select at least one company.");
            }

            if (!model.SelectedEmployeeIds.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedEmployeeIds), "Please select at least one employee.");
            }

            if (!ModelState.IsValid)
            {
                await LoadCompaniesAndEmployees(model);
                return View(model);
            }

            var task = await _context.Tasks
                .Include(t => t.TaskCompanies)
                .Include(t => t.TaskEmployees)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            if (model.Status == TaskStatus.Completed)
            {
                model.Progress = 100;
            }

            if (model.ErrorScreenshotFile != null && model.ErrorScreenshotFile.Length > 0)
            {
                task.ErrorScreenshotPath = await SaveScreenshotFileAsync(model.ErrorScreenshotFile);
            }

            var oldStatusStr = task.Status.ToString();

            task.TaskName = model.TaskName;
            task.Description = model.Description;
            task.Priority = model.Priority;
            task.Status = model.Status;
            task.ProjectId = model.ProjectId;
            task.StartDate = model.StartDate;
            task.EndDate = model.EndDate;
            task.DueDate = model.EndDate ?? model.DueDate;
            task.Progress = model.Progress;
            task.DelayReason = model.DelayReason;
            task.ErrorDetails = model.ErrorDetails;

            _context.TaskCompanies.RemoveRange(task.TaskCompanies);
            foreach (var companyId in model.SelectedCompanyIds)
            {
                _context.TaskCompanies.Add(new TaskCompany { TaskId = task.Id, CompanyId = companyId });
            }

            _context.TaskEmployees.RemoveRange(task.TaskEmployees);
            foreach (var employeeId in model.SelectedEmployeeIds)
            {
                _context.TaskEmployees.Add(new TaskEmployee { TaskId = task.Id, EmployeeId = employeeId });
            }

            await _context.SaveChangesAsync();

            await SyncTaskToTeamTasksAsync(task, model.SelectedEmployeeIds);

            await RecordTaskActivityLogsAsync(task.Id, task.TaskName, model.SelectedEmployeeIds, "Task Edited / Updated", oldStatusStr, task.Status.ToString(), task.Progress, task.DelayReason, task.ErrorDetails, task.ErrorScreenshotPath);

            TempData["Success"] = $"Task \"{task.TaskName}\" updated successfully.";

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // DELETE - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["Error"] = "Access Denied: Only Admin and HR can delete company tasks.";
                return RedirectToAction(nameof(Index));
            }

            var task = await _context.Tasks
                .Include(t => t.TaskCompanies)
                .Include(t => t.TaskEmployees)
                .Include(t => t.TaskActivityLogs)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            var taskName = task.TaskName;
            _context.Tasks.Remove(task);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Task \"{taskName}\" deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // DETAILS
        // =========================================================

        // GET: /Task/Details/1
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .Include(t => t.TaskActivityLogs)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                return NotFound();
            }

            return View(task);
        }

        // =========================================================
        // AJAX: QUICK UPDATE STATUS
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> QuickUpdateStatus(int id, TaskStatus status)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null)
            {
                return Json(new { success = false, message = "Task not found." });
            }

            var oldStatusStr = task.Status.ToString();

            var isOverdue = (task.EndDate.HasValue && task.EndDate.Value <= DateTime.Now) || (task.DueDate.HasValue && task.DueDate.Value <= DateTime.Now);
            var hasJustification = !string.IsNullOrWhiteSpace(task.DelayReason);

            if (isOverdue && !hasJustification && status != TaskStatus.ToDo)
            {
                return Json(new { success = false, message = "Overdue Task Locked: Please provide a delay justification first before updating the status." });
            }

            task.Status = status;
            if (status == TaskStatus.Completed)
            {
                task.Progress = 100;
            }
            else if (status == TaskStatus.ToDo && task.Progress == 100)
            {
                task.Progress = 0;
            }

            await _context.SaveChangesAsync();

            var empIds = await _context.TaskEmployees.Where(te => te.TaskId == id).Select(te => te.EmployeeId).ToListAsync();
            await SyncTaskToTeamTasksAsync(task, empIds);

            await RecordTaskActivityLogsAsync(task.Id, task.TaskName, empIds, $"Status Changed to {status}", oldStatusStr, status.ToString(), task.Progress, task.DelayReason, task.ErrorDetails, task.ErrorScreenshotPath);

            return Json(new { 
                success = true, 
                message = $"Status updated to {status}", 
                taskId = task.Id, 
                newStatus = task.Status.ToString(), 
                newProgress = task.Progress 
            });
        }

        // =========================================================
        // AJAX: QUICK ERROR LOG & SCREENSHOT UPLOAD
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> QuickUploadErrorLog(int id, string? delayReason, string? errorDetails, IFormFile? screenshotFile)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task == null)
            {
                return Json(new { success = false, message = "Task not found." });
            }

            task.DelayReason = delayReason;
            task.ErrorDetails = errorDetails;

            if (screenshotFile != null && screenshotFile.Length > 0)
            {
                task.ErrorScreenshotPath = await SaveScreenshotFileAsync(screenshotFile);
            }

            await _context.SaveChangesAsync();

            var empIds = await _context.TaskEmployees.Where(te => te.TaskId == id).Select(te => te.EmployeeId).ToListAsync();
            await SyncTaskToTeamTasksAsync(task, empIds);

            await RecordTaskActivityLogsAsync(task.Id, task.TaskName, empIds, "Project Delay / Error Logged", task.Status.ToString(), task.Status.ToString(), task.Progress, task.DelayReason, task.ErrorDetails, task.ErrorScreenshotPath);

            return Json(new { 
                success = true, 
                message = "Error report and screenshot saved successfully.",
                screenshotPath = task.ErrorScreenshotPath,
                delayReason = task.DelayReason,
                errorDetails = task.ErrorDetails
            });
        }

        // =========================================================
        // HELPER: SYNC TASKBOARD STATUS TO TEAM TASKS
        // =========================================================
        private async Task SyncTaskToTeamTasksAsync(TaskItem task, List<int>? assignedEmployeeIds = null)
        {
            if (task == null || string.IsNullOrWhiteSpace(task.TaskName)) return;

            try
            {
                var taskNameLower = task.TaskName.Trim().ToLower();

                var matchingTeamTasks = await _context.TeamTasks
                    .Where(tt => tt.Title.ToLower() == taskNameLower || taskNameLower.Contains(tt.Title.ToLower()) || tt.Title.ToLower().Contains(taskNameLower))
                    .ToListAsync();

                if (matchingTeamTasks.Any())
                {
                    foreach (var tt in matchingTeamTasks)
                    {
                        if (task.Status == TaskStatus.Completed)
                        {
                            tt.Status = TeamTaskStatus.Completed;
                            if (!tt.CompletedAt.HasValue)
                            {
                                tt.CompletedAt = DateTime.Now;
                            }
                            tt.IncompleteReason = null;
                        }
                        else if (task.Status == TaskStatus.InProgress || task.Status == TaskStatus.InReview)
                        {
                            tt.Status = TeamTaskStatus.InProgress;
                            if (!string.IsNullOrWhiteSpace(task.DelayReason))
                            {
                                tt.IncompleteReason = task.DelayReason;
                            }
                        }
                        else if (task.Status == TaskStatus.ToDo)
                        {
                            if (!string.IsNullOrWhiteSpace(task.DelayReason))
                            {
                                tt.Status = TeamTaskStatus.NotCompleted;
                                tt.IncompleteReason = task.DelayReason;
                            }
                            else
                            {
                                tt.Status = TeamTaskStatus.Pending;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(task.DelayReason))
                        {
                            tt.IncompleteReason = task.DelayReason;
                        }

                        tt.Priority = task.Priority;
                        tt.ProjectId = task.ProjectId ?? tt.ProjectId;
                    }

                    await _context.SaveChangesAsync();
                }
                else if (assignedEmployeeIds != null && assignedEmployeeIds.Any())
                {
                    foreach (var empId in assignedEmployeeIds)
                    {
                        var teamMember = await _context.TeamMembers
                            .Include(tm => tm.Team)
                            .FirstOrDefaultAsync(tm => tm.EmployeeId == empId);

                        if (teamMember != null && teamMember.Team != null)
                        {
                            var teamTaskStatus = task.Status switch
                            {
                                TaskStatus.Completed => TeamTaskStatus.Completed,
                                TaskStatus.InProgress => TeamTaskStatus.InProgress,
                                TaskStatus.InReview => TeamTaskStatus.InProgress,
                                _ => !string.IsNullOrWhiteSpace(task.DelayReason) ? TeamTaskStatus.NotCompleted : TeamTaskStatus.Pending
                            };

                            var newTeamTask = new TeamTask
                            {
                                TeamId = teamMember.TeamId,
                                AssignedByLeaderId = teamMember.Team.TeamLeaderId,
                                AssignedToEmployeeId = empId,
                                Title = task.TaskName,
                                Description = task.Description ?? task.TaskName,
                                Priority = task.Priority,
                                Status = teamTaskStatus,
                                IncompleteReason = task.DelayReason,
                                ProjectId = task.ProjectId,
                                StartDate = task.StartDate ?? task.CreatedAt,
                                EndDate = task.EndDate ?? task.DueDate,
                                DueDate = task.DueDate ?? task.EndDate,
                                CreatedAt = task.CreatedAt,
                                CompletedAt = task.Status == TaskStatus.Completed ? DateTime.Now : null
                            };

                            _context.TeamTasks.Add(newTeamTask);
                        }
                    }

                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing task to team tasks: {ex.Message}");
            }
        }

        // =========================================================
        // AJAX: DYNAMICALLY CREATE COMPANY
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> QuickCreateCompany(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { success = false, message = "Company name is required." });
            }

            var trimmedName = name.Trim();
            var existing = await _context.Companies.FirstOrDefaultAsync(c => c.Name.ToLower() == trimmedName.ToLower());
            if (existing != null)
            {
                return Json(new { success = true, id = existing.Id, name = existing.Name, isExisting = true });
            }

            var company = new Company { Name = trimmedName };
            _context.Companies.Add(company);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = company.Id, name = company.Name, isExisting = false });
        }

        // =========================================================
        // AJAX: DYNAMICALLY CREATE EMPLOYEE
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> QuickCreateEmployee(string name)
        {
            if (!_sessionService.IsAdmin())
            {
                return Json(new { success = false, message = "Access Denied: Only Administrator has permission to add new employees." });
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { success = false, message = "Employee name is required." });
            }

            var trimmedName = name.Trim();
            var existing = await _context.Employees.FirstOrDefaultAsync(e => e.Name.ToLower() == trimmedName.ToLower());
            if (existing != null)
            {
                return Json(new { success = true, id = existing.Id, name = existing.Name, isExisting = true });
            }

            var employee = new Employee 
            { 
                Name = trimmedName,
                Designation = "Software Engineer",
                Department = "Engineering",
                CreatedAt = DateTime.Now,
                IsActive = true
            };
            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = employee.Id, name = employee.Name, isExisting = false });
        }

        // =========================================================
        // AJAX: HR OVERDUE EMAIL NOTIFICATION ENDPOINTS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetOverdueTaskPreview()
        {
            var now = DateTime.Now;
            var today = DateTime.Today;
            var overdueTasks = await _context.Tasks
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .Where(t => t.Status != TaskStatus.Completed && 
                            ((t.EndDate.HasValue && t.EndDate.Value <= now) || 
                             (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) || 
                             !string.IsNullOrEmpty(t.DelayReason)))
                .AsNoTracking()
                .Select(t => new
                {
                    id = t.Id,
                    name = t.TaskName,
                    priority = t.Priority.ToString(),
                    status = t.Status.ToString(),
                    progress = t.Progress,
                    dueDate = t.EndDate.HasValue ? t.EndDate.Value.ToString("MMM dd, h:mm tt") : (t.DueDate.HasValue ? t.DueDate.Value.ToString("MMM dd, yyyy") : ""),
                    companies = string.Join(", ", t.TaskCompanies.Select(tc => tc.Company.Name)),
                    employees = string.Join(", ", t.TaskEmployees.Select(te => te.Employee.Name)),
                    delayReason = t.DelayReason,
                    errorDetails = t.ErrorDetails,
                    hasScreenshot = !string.IsNullOrEmpty(t.ErrorScreenshotPath)
                })
                .ToListAsync();

            return Json(new { count = overdueTasks.Count, tasks = overdueTasks });
        }

        [HttpPost]
        public async Task<IActionResult> SendOverdueHrNotification(string? toEmail = null, string? ccEmail = null, string? bccEmail = null)
        {
            var now = DateTime.Now;
            var today = DateTime.Today;
            var overdueTasks = await _context.Tasks
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .Where(t => t.Status != TaskStatus.Completed && 
                            ((t.EndDate.HasValue && t.EndDate.Value <= now) || 
                             (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) || 
                             !string.IsNullOrEmpty(t.DelayReason)))
                .ToListAsync();

            if (!overdueTasks.Any())
            {
                return Json(new { success = false, message = "No uncompleted overdue tasks found to report." });
            }

            var sent = await _emailService.SendOverdueTasksNotificationToHrAsync(overdueTasks, toEmail, ccEmail, bccEmail);
            if (sent)
            {
                return Json(new { 
                    success = true, 
                    count = overdueTasks.Count, 
                    message = $"HR email notification sent successfully for {overdueTasks.Count} uncompleted task(s)." 
                });
            }

            return Json(new { success = false, message = "Failed to send email notification to HR." });
        }

        // =========================================================
        // FILE UPLOAD HELPER
        // =========================================================

        private async Task<string> SaveScreenshotFileAsync(IFormFile file)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "screenshots");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"screenshot_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/screenshots/{uniqueFileName}";
        }

        // =========================================================
        // LOAD HELPER DROPDOWNS
        // =========================================================

        private async Task LoadCompaniesAndEmployees(TaskViewModel model)
        {
            model.Companies = await _context.Companies
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name,
                    Selected = model.SelectedCompanyIds.Contains(c.Id)
                })
                .ToListAsync();

            model.Employees = await _context.Employees
                .AsNoTracking()
                .OrderBy(e => e.Name)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Name,
                    Selected = model.SelectedEmployeeIds.Contains(e.Id)
                })
                .ToListAsync();

            model.Projects = await _context.Projects
                .AsNoTracking()
                .OrderBy(p => p.ProjectName)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = p.ProjectName,
                    Selected = model.ProjectId.HasValue && model.ProjectId.Value == p.Id
                })
                .ToListAsync();

            model.PriorityList = Enum.GetValues(typeof(TaskPriority))
                .Cast<TaskPriority>()
                .Select(p => new SelectListItem
                {
                    Value = ((int)p).ToString(),
                    Text = p.ToString(),
                    Selected = model.Priority == p
                })
                .ToList();

            model.StatusList = Enum.GetValues(typeof(TaskStatus))
                .Cast<TaskStatus>()
                .Select(s => new SelectListItem
                {
                    Value = ((int)s).ToString(),
                    Text = s == TaskStatus.ToDo ? "To Do" :
                           s == TaskStatus.InProgress ? "In Progress" :
                           s == TaskStatus.InReview ? "In Review" : "Completed",
                    Selected = model.Status == s
                })
                .ToList();
        }

        // =========================================================
        // EMPLOYEE APPRECIATION & WEEKLY ANALYSIS
        // =========================================================

        // GET: /Task/Appreciation
        public async Task<IActionResult> Appreciation()
        {
            var employees = await _context.Employees
                .Include(e => e.TaskEmployees)
                    .ThenInclude(te => te.Task)
                        .ThenInclude(t => t.TaskCompanies)
                            .ThenInclude(tc => tc.Company)
                .AsNoTracking()
                .ToListAsync();

            var allTasks = await _context.Tasks.AsNoTracking().ToListAsync();

            var leaderboard = new List<EmployeeAppreciationViewModel>();

            foreach (var emp in employees)
            {
                var assignedTasks = emp.TaskEmployees.Select(te => te.Task).Where(t => t != null).ToList();
                int totalAssigned = assignedTasks.Count;
                int completedCount = assignedTasks.Count(t => t.Status == TaskStatus.Completed);
                
                // Perfect Tasks: Completed, 100% progress, and no reported delay reason
                var perfectTasks = assignedTasks
                    .Where(t => t.Status == TaskStatus.Completed && t.Progress >= 100 && string.IsNullOrWhiteSpace(t.DelayReason))
                    .Select(t => t.TaskName)
                    .ToList();
                int perfectCount = perfectTasks.Count;

                double completionRate = totalAssigned > 0 ? Math.Round((double)completedCount / totalAssigned * 100, 1) : 0;
                double perfectRate = totalAssigned > 0 ? Math.Round((double)perfectCount / totalAssigned * 100, 1) : 0;

                var companies = assignedTasks
                    .SelectMany(t => t.TaskCompanies)
                    .Select(tc => tc.Company?.Name)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .OfType<string>()
                    .Distinct()
                    .ToList();

                leaderboard.Add(new EmployeeAppreciationViewModel
                {
                    EmployeeId = emp.Id,
                    EmployeeName = emp.Name,
                    TotalAssignedTasks = totalAssigned,
                    CompletedTasksCount = completedCount,
                    PerfectTasksCount = perfectCount,
                    CompletionRate = completionRate,
                    PerfectRate = perfectRate,
                    AssignedCompanies = companies,
                    PerfectlyCompletedTasks = perfectTasks
                });
            }

            // Order leaderboard by PerfectTasksCount DESC, then CompletedTasksCount DESC
            leaderboard = leaderboard
                .OrderByDescending(e => e.PerfectTasksCount)
                .ThenByDescending(e => e.CompletedTasksCount)
                .ThenByDescending(e => e.CompletionRate)
                .ToList();

            // Inspiring engineering and growth mindset quotes
            var generalQuotes = new List<MotivationalQuoteItem>
            {
                new MotivationalQuoteItem
                {
                    Quote = "Great things are not done by impulse, but by a series of small things brought together.",
                    Author = "Vincent Van Gogh",
                    Category = "Consistency & Mastery",
                    Icon = "bi-stars"
                },
                new MotivationalQuoteItem
                {
                    Quote = "It’s not that I’m so smart, it’s just that I stay with problems longer.",
                    Author = "Albert Einstein",
                    Category = "Tenacity & Engineering",
                    Icon = "bi-lightbulb-fill"
                },
                new MotivationalQuoteItem
                {
                    Quote = "Success is the sum of small efforts, repeated day in and day out.",
                    Author = "Robert Collier",
                    Category = "Dedication & Growth",
                    Icon = "bi-trophy-fill"
                },
                new MotivationalQuoteItem
                {
                    Quote = "Continuous improvement is better than delayed perfection.",
                    Author = "Mark Twain",
                    Category = "Agility & Progress",
                    Icon = "bi-lightning-charge-fill"
                },
                new MotivationalQuoteItem
                {
                    Quote = "Every master was once a beginner. Every champion refused to give up.",
                    Author = "Robin Sharma",
                    Category = "Potential & Resilience",
                    Icon = "bi-rocket-takeoff-fill"
                },
                new MotivationalQuoteItem
                {
                    Quote = "Teamwork divides the task and multiplies the success.",
                    Author = "Team Philosophy",
                    Category = "Collaboration",
                    Icon = "bi-people-fill"
                }
            };

            var encouragingAffirmations = new[]
            {
                ("It’s not that I’m so smart, it’s just that I stay with problems longer.", "Albert Einstein", "Tenacity Anchor", "Tackling complex problem-solving with steady determination."),
                ("Every master was once a beginner. Keep experimenting and building.", "Robin Sharma", "Rising Pioneer", "Actively expanding skillsets and building tomorrow's breakthrough solutions."),
                ("Progress over perfection. Every challenge is a stepping stone to mastery.", "Carol Dweck", "Growth Champion", "Showing great resilience and persistent effort on team deliverables."),
                ("Success is built upon curiosity, dedication, and mutual team support.", "Grace Hopper", "Foundation Builder", "Valuable team cornerstone driving progress with genuine dedication."),
                ("Great developers aren't born; they are forged through perseverance.", "Martin Fowler", "Resilience Star", "Facing intricate challenges with courage and continuous focus."),
                ("Quality is not an act, it is a habit cultivated day by day.", "Aristotle", "Craftsmanship Advocate", "Dedicated to refined execution and continuous craftsmanship.")
            };

            // Assign uplifting titles, quotes and positive recognition to every employee
            for (int i = 0; i < leaderboard.Count; i++)
            {
                var item = leaderboard[i];
                item.Rank = i + 1;

                if (i == 0 && item.PerfectTasksCount > 0)
                {
                    item.IsStarPerformer = true;
                    item.AwardTitle = "⭐ Star Performer of the Cycle";
                    item.AwardDescription = "Recognized for exemplary craftsmanship, flawless execution, and zero recorded project delays.";
                    item.BadgeGradient = "linear-gradient(135deg, #f59e0b 0%, #d97706 100%)";
                    item.MotivationalQuote = "Excellence is never an accident. It is always the result of high intention and sincere effort.";
                    item.MotivationalAuthor = "Aristotle";
                    item.EncouragementTag = "Excellence Vanguard";
                    item.GrowthMindsetNote = "Inspiring the entire engineering team with flawless benchmark execution.";
                }
                else if (i == 1 && item.CompletedTasksCount > 0)
                {
                    item.AwardTitle = "🏆 Quality & Craftsmanship Master";
                    item.AwardDescription = "Demonstrated top-tier precision, outstanding reliability, and steadfast commitment.";
                    item.BadgeGradient = "linear-gradient(135deg, #0ea5e9 0%, #0284c7 100%)";
                    item.MotivationalQuote = "Quality means doing it right when no one is looking.";
                    item.MotivationalAuthor = "Henry Ford";
                    item.EncouragementTag = "Quality Craftsman";
                    item.GrowthMindsetNote = "Consistently raising the engineering standard for all team members.";
                }
                else if (i == 2 && item.CompletedTasksCount > 0)
                {
                    item.AwardTitle = "⚡ Velocity & Impact Leader";
                    item.AwardDescription = "Driving momentum with high velocity, proactive collaboration, and dedicated problem-solving.";
                    item.BadgeGradient = "linear-gradient(135deg, #f97316 0%, #ea580c 100%)";
                    item.MotivationalQuote = "Action is the foundational key to all success.";
                    item.MotivationalAuthor = "Pablo Picasso";
                    item.EncouragementTag = "Velocity Innovator";
                    item.GrowthMindsetNote = "Energizing sprint velocity through persistent execution.";
                }
                else if (item.CompletedTasksCount > 0)
                {
                    var aff = encouragingAffirmations[i % encouragingAffirmations.Length];
                    item.AwardTitle = $"🚀 {aff.Item3}";
                    item.AwardDescription = aff.Item4;
                    item.BadgeGradient = "linear-gradient(135deg, #6366f1 0%, #4f46e5 100%)";
                    item.MotivationalQuote = aff.Item1;
                    item.MotivationalAuthor = aff.Item2;
                    item.EncouragementTag = aff.Item3;
                    item.GrowthMindsetNote = "Valuable teammate consistently contributing to our collective sprint goals.";
                }
                else
                {
                    // Uplifting encouragement for employees who are currently learning, overcoming roadblocks, or starting new tasks
                    var aff = encouragingAffirmations[i % encouragingAffirmations.Length];
                    item.AwardTitle = "🌱 Emerging Talent & Explorer";
                    item.AwardDescription = "Embracing growth and tackling challenging workflows with positive momentum.";
                    item.BadgeGradient = "linear-gradient(135deg, #10b981 0%, #059669 100%)";
                    item.MotivationalQuote = aff.Item1;
                    item.MotivationalAuthor = aff.Item2;
                    item.EncouragementTag = "Rising Contributor";
                    item.GrowthMindsetNote = "Every challenge solved today is a masterclass for tomorrow's breakthroughs!";
                }
            }

            var starPerformer = leaderboard.FirstOrDefault(e => e.IsStarPerformer) ?? leaderboard.FirstOrDefault();

            int totalTasks = allTasks.Count;
            int totalCompleted = allTasks.Count(t => t.Status == TaskStatus.Completed);
            int totalPerfect = allTasks.Count(t => t.Status == TaskStatus.Completed && t.Progress >= 100 && string.IsNullOrWhiteSpace(t.DelayReason));
            double systemPerfection = totalTasks > 0 ? Math.Round((double)totalPerfect / totalTasks * 100, 1) : 0;

            var allLogs = await _context.TaskActivityLogs
                .OrderByDescending(l => l.LoggedAt)
                .Take(500)
                .AsNoTracking()
                .ToListAsync();

            var pageModel = new WeeklyAppreciationPageViewModel
            {
                Leaderboard = leaderboard,
                StarPerformer = starPerformer,
                TotalSystemTasks = totalTasks,
                TotalCompletedTasks = totalCompleted,
                TotalPerfectTasks = totalPerfect,
                OverallSystemPerfectionRate = systemPerfection,
                AllActivityLogs = allLogs,
                DailyInspirations = generalQuotes
            };

            return View(pageModel);
        }

        // =========================================================
        // HR AUDIT LOG HELPER
        // =========================================================

        private async Task RecordTaskActivityLogsAsync(int taskId, string taskName, IEnumerable<int> employeeIds, string actionType, string? oldStatus, string? newStatus, int progress, string? delayReason, string? errorDetails, string? screenshotPath)
        {
            try
            {
                var empList = employeeIds != null && employeeIds.Any()
                    ? await _context.Employees.Where(e => employeeIds.Contains(e.Id)).ToListAsync()
                    : new List<Employee>();

                if (empList.Any())
                {
                    foreach (var emp in empList)
                    {
                        _context.TaskActivityLogs.Add(new TaskActivityLog
                        {
                            TaskId = taskId,
                            TaskName = taskName,
                            EmployeeId = emp.Id,
                            EmployeeName = emp.Name,
                            ActionType = actionType,
                            OldStatus = oldStatus,
                            NewStatus = newStatus,
                            Progress = progress,
                            DelayReason = delayReason,
                            ErrorDetails = errorDetails,
                            ErrorScreenshotPath = screenshotPath,
                            LoggedAt = DateTime.Now
                        });
                    }
                }
                else
                {
                    _context.TaskActivityLogs.Add(new TaskActivityLog
                    {
                        TaskId = taskId,
                        TaskName = taskName,
                        EmployeeId = null,
                        EmployeeName = "System / Unassigned",
                        ActionType = actionType,
                        OldStatus = oldStatus,
                        NewStatus = newStatus,
                        Progress = progress,
                        DelayReason = delayReason,
                        ErrorDetails = errorDetails,
                        ErrorScreenshotPath = screenshotPath,
                        LoggedAt = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error recording activity log: {ex.Message}");
            }
        }

        // =========================================================
        // EXPORT DAILY TASKSHEET EXCEL
        // =========================================================
        // =========================================================
        // EXPORT DAILY / RANGE TASKSHEET EXCEL
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> ExportDailyTasksheet(DateTime? startDate, DateTime? endDate, string filter = "all")
        {
            await ProcessUncompletedTaskRolloverAsync();

            var today = DateTime.Today;
            var query = _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.TaskCompanies).ThenInclude(tc => tc.Company)
                .Include(t => t.TaskEmployees).ThenInclude(te => te.Employee)
                .AsNoTracking();

            filter = filter?.ToLower() ?? "all";

            // Filter by Date Range if specified
            if (startDate.HasValue && endDate.HasValue)
            {
                var start = startDate.Value.Date;
                var end = endDate.Value.Date;
                query = query.Where(t => (t.CreatedAt.Date >= start && t.CreatedAt.Date <= end) ||
                                         (t.DueDate.HasValue && t.DueDate.Value.Date >= start && t.DueDate.Value.Date <= end) ||
                                         (t.StartDate.HasValue && t.StartDate.Value.Date >= start && t.StartDate.Value.Date <= end));
            }
            else if (startDate.HasValue)
            {
                var start = startDate.Value.Date;
                query = query.Where(t => t.CreatedAt.Date >= start ||
                                         (t.DueDate.HasValue && t.DueDate.Value.Date >= start) ||
                                         (t.StartDate.HasValue && t.StartDate.Value.Date >= start));
            }
            else if (endDate.HasValue)
            {
                var end = endDate.Value.Date;
                query = query.Where(t => t.CreatedAt.Date <= end ||
                                         (t.DueDate.HasValue && t.DueDate.Value.Date <= end) ||
                                         (t.StartDate.HasValue && t.StartDate.Value.Date <= end));
            }

            // Filter by Status / Priority Category
            if (filter == "today")
            {
                query = query.Where(t => !t.DueDate.HasValue || t.DueDate.Value.Date == today || t.CreatedAt.Date == today);
            }
            else if (filter == "inprogress")
            {
                query = query.Where(t => t.Status == TaskStatus.InProgress);
            }
            else if (filter == "overdue")
            {
                var now = DateTime.Now;
                query = query.Where(t => t.Status != TaskStatus.Completed && 
                                         ((t.EndDate.HasValue && t.EndDate.Value <= now) || 
                                          (t.DueDate.HasValue && (t.DueDate.Value <= now || t.DueDate.Value.Date < today)) || 
                                          !string.IsNullOrEmpty(t.DelayReason)));
            }
            else if (filter == "completed")
            {
                query = query.Where(t => t.Status == TaskStatus.Completed);
            }
            else if (filter == "urgent")
            {
                query = query.Where(t => t.Priority == TaskPriority.Urgent || t.Priority == TaskPriority.High);
            }

            var tasks = await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Priority).ToListAsync();

            var builder = new System.Text.StringBuilder();
            // UTF-8 BOM for Microsoft Excel recognition
            builder.Append('\uFEFF');

            // Daily Tasksheet Excel Header Row
            builder.AppendLine("Task ID,Date,Task Name,Description,Priority,Status,Progress (%),Start Time,End Time,Total Hours,Companies,Assigned Employees,Delay / Overdue Reason,Error Details");

            foreach (var t in tasks)
            {
                var id = t.Id;
                var dateStr = t.DueDate.HasValue ? t.DueDate.Value.ToString("yyyy-MM-dd") : t.CreatedAt.ToString("yyyy-MM-dd");
                var name = $"\"{t.TaskName.Replace("\"", "\"\"")}\"";
                var desc = $"\"{(t.Description ?? "").Replace("\"", "\"\"")}\"";
                var priority = t.Priority.ToString();
                var status = t.Status.ToString();
                var progress = t.Progress;
                var startTime = t.StartDate.HasValue ? t.StartDate.Value.ToString("hh:mm tt") : "-";
                var endTime = t.EndDate.HasValue ? t.EndDate.Value.ToString("hh:mm tt") : "-";

                string totalHours = "-";
                if (t.StartDate.HasValue && t.EndDate.HasValue && t.EndDate.Value >= t.StartDate.Value)
                {
                    var span = t.EndDate.Value - t.StartDate.Value;
                    totalHours = $"{span.Hours}h {span.Minutes}m";
                }

                var companies = $"\"{string.Join(", ", t.TaskCompanies.Select(c => c.Company?.Name).Where(n => !string.IsNullOrEmpty(n))).Replace("\"", "\"\"")}\"";
                var employees = $"\"{string.Join(", ", t.TaskEmployees.Select(e => e.Employee?.Name).Where(n => !string.IsNullOrEmpty(n))).Replace("\"", "\"\"")}\"";
                var delayReason = $"\"{(t.DelayReason ?? "").Replace("\"", "\"\"")}\"";
                var errorDetails = $"\"{(t.ErrorDetails ?? "").Replace("\"", "\"\"")}\"";

                builder.AppendLine($"{id},{dateStr},{name},{desc},{priority},{status},{progress}%,{startTime},{endTime},{totalHours},{companies},{employees},{delayReason},{errorDetails}");
            }

            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(builder.ToString());
            string fileName;
            if (startDate.HasValue && endDate.HasValue)
            {
                fileName = $"Auxinzio_Tasksheet_{startDate.Value:yyyyMMdd}_to_{endDate.Value:yyyyMMdd}.csv";
            }
            else
            {
                fileName = $"Auxinzio_Total_Tasksheet_{DateTime.Today:yyyy-MM-dd}.csv";
            }

            return File(buffer, "text/csv; charset=utf-8", fileName);
        }
    }
}