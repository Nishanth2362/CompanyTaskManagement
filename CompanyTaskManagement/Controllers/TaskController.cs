using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.Companies.Commands.AddEdit;
using CompanyTaskManagement.Application.Features.Companies.Commands.Delete;
using CompanyTaskManagement.Application.Features.Companies.Queries.GetAll;
using CompanyTaskManagement.Application.Features.Employees.Commands.AddEdit;
using CompanyTaskManagement.Application.Features.Employees.Commands.Delete;
using CompanyTaskManagement.Application.Features.Employees.Queries.GetAll;
using CompanyTaskManagement.Application.Features.Tasks.Commands.AddEdit;
using CompanyTaskManagement.Application.Features.Tasks.Commands.Delete;
using CompanyTaskManagement.Application.Features.Tasks.Commands.LogActivity;
using CompanyTaskManagement.Application.Features.Tasks.Commands.QuickError;
using CompanyTaskManagement.Application.Features.Tasks.Commands.QuickStatus;
using CompanyTaskManagement.Application.Features.Tasks.Commands.Sync;
using CompanyTaskManagement.Application.Features.Tasks.Queries.Appreciation;
using CompanyTaskManagement.Application.Features.Tasks.Queries.Export;
using CompanyTaskManagement.Application.Features.Tasks.Queries.GetById;
using CompanyTaskManagement.Application.Features.Tasks.Queries.GetFiltered;
using CompanyTaskManagement.Application.Features.Tasks.Queries.GetOverdue;
using CompanyTaskManagement.Application.Features.Tasks.Queries.GetPaged;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Domain.Enums;
using CompanyTaskManagement.Models;
using CompanyTaskManagement.Services;
using CompanyTaskManagement.ViewModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using TaskStatus = CompanyTaskManagement.Domain.Enums.TaskStatus;

namespace CompanyTaskManagement.Controllers
{
    [Authorize]
    public class TaskController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly IUserSessionService _sessionService;
        private readonly ILogger<TaskController> _logger;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _memoryCache;

        public TaskController(
            IMediator mediator,
            IEmailService emailService,
            IWebHostEnvironment environment,
            IUserSessionService sessionService,
            ILogger<TaskController> logger,
            Microsoft.Extensions.Caching.Memory.IMemoryCache memoryCache)
        {
            _mediator = mediator;
            _emailService = emailService;
            _environment = environment;
            _sessionService = sessionService;
            _logger = logger;
            _memoryCache = memoryCache;
        }

        // =========================================================
        // INDEX
        // =========================================================

        // GET: /Task
        public async Task<IActionResult> Index(string filter = "all")
        {
            var currentEmpId = _sessionService.GetCurrentEmployeeId();

            // 1. Efficient DB-Level Metrics via MediatR
            var metricsResult = await _mediator.Send(new GetTaskMetricsQuery { CurrentEmployeeId = currentEmpId });
            var metrics = metricsResult.Data ?? new TaskDashboardMetrics();

            ViewBag.CurrentFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.ToLower();
            ViewBag.AllCount = metrics.AllCount;
            ViewBag.TodayCount = metrics.TodayCount;
            ViewBag.InProgressCount = metrics.InProgressCount;
            ViewBag.OverdueCount = metrics.OverdueCount;
            ViewBag.CompletedCount = metrics.CompletedCount;
            ViewBag.UrgentCount = metrics.UrgentCount;
            ViewBag.MyTasksCount = metrics.MyTasksCount;

            // Role Context for Views
            ViewBag.CurrentRole = _sessionService.GetCurrentRole();
            ViewBag.IsAdminOrHr = _sessionService.IsAdminOrHr();
            ViewBag.IsEmployee = _sessionService.IsEmployee();
            ViewBag.CurrentEmployeeId = currentEmpId;
            ViewBag.CurrentEmployeeName = _sessionService.GetCurrentEmployeeName();

            // 2. Fetch filtered tasks via MediatR
            var tasksResult = await _mediator.Send(new GetFilteredTasksQuery
            {
                Filter = filter,
                CurrentEmployeeId = currentEmpId
            });

            var filteredTasks = tasksResult.Data ?? new List<TaskItem>();
            return View(filteredTasks);
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

            var now = DateTime.Now;
            var nowTrimmed = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

            var model = new TaskViewModel
            {
                StartDate = nowTrimmed,
                EndDate = nowTrimmed.AddDays(7),
                DueDate = nowTrimmed.AddDays(7),
                Priority = TaskPriority.Medium,
                Status = TaskStatus.ToDo,
                Progress = 0
            };

            await LoadCompaniesAndEmployeesAsync(model);
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
                await LoadCompaniesAndEmployeesAsync(model);
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

            var command = new AddEditTaskCommand
            {
                Id = 0,
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
                CompanyIds = model.SelectedCompanyIds,
                EmployeeIds = model.SelectedEmployeeIds
            };

            var result = await _mediator.Send(command);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join("; ", result.Messages);
                await LoadCompaniesAndEmployeesAsync(model);
                return View(model);
            }

            var taskId = result.Data;

            // Sync with team tasks
            await _mediator.Send(new SyncTaskToTeamCommand
            {
                TaskId = taskId,
                AssignedEmployeeIds = model.SelectedEmployeeIds
            });

            // Activity Log
            await _mediator.Send(new LogTaskActivityCommand
            {
                TaskId = taskId,
                TaskName = model.TaskName,
                EmployeeId = _sessionService.GetCurrentEmployeeId(),
                EmployeeName = _sessionService.GetCurrentEmployeeName() ?? "Admin/HR",
                ActionType = "Task Created",
                OldStatus = null,
                NewStatus = model.Status.ToString(),
                Progress = model.Progress,
                DelayReason = model.DelayReason,
                ErrorDetails = model.ErrorDetails,
                ScreenshotPath = uploadedScreenshotPath
            });

            TempData["Success"] = $"Task \"{model.TaskName}\" created successfully.";
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

            var result = await _mediator.Send(new GetTaskByIdQuery { Id = id });
            if (!result.Succeeded || result.Data == null)
            {
                return NotFound();
            }

            var task = result.Data;

            var model = new TaskViewModel
            {
                Id = task.Id,
                TaskName = task.TaskName,
                Description = task.Description,
                Priority = task.Priority,
                Status = task.Status,
                ProjectId = task.ProjectId,
                StartDate = task.StartDate,
                EndDate = task.EndDate,
                DueDate = task.DueDate,
                Progress = task.Progress,
                DelayReason = task.DelayReason,
                ErrorDetails = task.ErrorDetails,
                ErrorScreenshotPath = task.ErrorScreenshotPath,
                SelectedCompanyIds = task.TaskCompanies.Select(tc => tc.CompanyId).ToList(),
                SelectedEmployeeIds = task.TaskEmployees.Select(te => te.EmployeeId).ToList()
            };

            await LoadCompaniesAndEmployeesAsync(model);
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
                await LoadCompaniesAndEmployeesAsync(model);
                return View(model);
            }

            string? uploadedScreenshotPath = model.ErrorScreenshotPath;
            if (model.ErrorScreenshotFile != null && model.ErrorScreenshotFile.Length > 0)
            {
                uploadedScreenshotPath = await SaveScreenshotFileAsync(model.ErrorScreenshotFile);
            }

            if (model.Status == TaskStatus.Completed)
            {
                model.Progress = 100;
            }

            var command = new AddEditTaskCommand
            {
                Id = id,
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
                CompanyIds = model.SelectedCompanyIds,
                EmployeeIds = model.SelectedEmployeeIds
            };

            var updateResult = await _mediator.Send(command);
            if (!updateResult.Succeeded)
            {
                TempData["Error"] = string.Join("; ", updateResult.Messages);
                await LoadCompaniesAndEmployeesAsync(model);
                return View(model);
            }

            // Sync with TeamTasks
            await _mediator.Send(new SyncTaskToTeamCommand
            {
                TaskId = id,
                AssignedEmployeeIds = model.SelectedEmployeeIds
            });

            // Activity Log
            await _mediator.Send(new LogTaskActivityCommand
            {
                TaskId = id,
                TaskName = model.TaskName,
                EmployeeId = _sessionService.GetCurrentEmployeeId(),
                EmployeeName = _sessionService.GetCurrentEmployeeName() ?? "Admin/HR",
                ActionType = "Task Updated",
                OldStatus = null,
                NewStatus = model.Status.ToString(),
                Progress = model.Progress,
                DelayReason = model.DelayReason,
                ErrorDetails = model.ErrorDetails,
                ScreenshotPath = uploadedScreenshotPath
            });

            TempData["Success"] = $"Task \"{model.TaskName}\" updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // DELETE - POST
        // =========================================================

        // POST: /Task/Delete/1
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                TempData["Error"] = "Access Denied: Only Admin and HR can delete tasks.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _mediator.Send(new DeleteTaskCommand { Id = id });
            if (result.Succeeded)
            {
                TempData["Success"] = "Task deleted successfully.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // DETAILS
        // =========================================================

        // GET: /Task/Details/1
        public async Task<IActionResult> Details(int id)
        {
            var result = await _mediator.Send(new GetTaskByIdQuery { Id = id });
            if (!result.Succeeded || result.Data == null)
            {
                return NotFound();
            }

            return View(result.Data);
        }

        // =========================================================
        // QUICK STATUS UPDATE (AJAX)
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> QuickUpdateStatus(int id, TaskStatus status)
        {
            var currentEmpId = _sessionService.GetCurrentEmployeeId();
            var currentEmpName = _sessionService.GetCurrentEmployeeName();

            var result = await _mediator.Send(new QuickUpdateTaskStatusCommand
            {
                TaskId = id,
                Status = status,
                CurrentEmployeeId = currentEmpId,
                CurrentEmployeeName = currentEmpName
            });

            if (!result.Succeeded)
            {
                return Json(new { success = false, message = string.Join("; ", result.Messages) });
            }

            // Sync with TeamTask
            await _mediator.Send(new SyncTaskToTeamCommand { TaskId = id });

            return Json(new
            {
                success = true,
                newStatus = status.ToString(),
                progress = status == TaskStatus.Completed ? 100 : (int?)null,
                message = $"Status updated to {status}."
            });
        }

        // =========================================================
        // QUICK UPLOAD ERROR LOG (AJAX)
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> QuickUploadErrorLog(int id, string? delayReason, string? errorDetails, IFormFile? screenshotFile)
        {
            string? uploadedPath = null;
            if (screenshotFile != null && screenshotFile.Length > 0)
            {
                uploadedPath = await SaveScreenshotFileAsync(screenshotFile);
            }

            var currentEmpId = _sessionService.GetCurrentEmployeeId();
            var currentEmpName = _sessionService.GetCurrentEmployeeName();

            var result = await _mediator.Send(new QuickUploadErrorLogCommand
            {
                TaskId = id,
                DelayReason = delayReason,
                ErrorDetails = errorDetails,
                ScreenshotPath = uploadedPath,
                CurrentEmployeeId = currentEmpId,
                CurrentEmployeeName = currentEmpName
            });

            if (!result.Succeeded)
            {
                return Json(new { success = false, message = string.Join("; ", result.Messages) });
            }

            // Sync with TeamTask
            await _mediator.Send(new SyncTaskToTeamCommand { TaskId = id });

            return Json(new
            {
                success = true,
                delayReason = delayReason,
                errorDetails = errorDetails,
                screenshotPath = uploadedPath,
                message = "Task log updated successfully."
            });
        }

        // =========================================================
        // AJAX: DYNAMICALLY CREATE COMPANY
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> QuickCreateCompany(string name)
        {
            var result = await _mediator.Send(new AddEditCompanyCommand { Id = 0, Name = name });
            if (result.Succeeded)
            {
                return Json(new { success = true, id = result.Data, name = name.Trim() });
            }

            return Json(new { success = false, message = string.Join("; ", result.Messages) });
        }

        // =========================================================
        // AJAX: DYNAMICALLY CREATE EMPLOYEE
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> QuickCreateEmployee(string name)
        {
            var result = await _mediator.Send(new AddEditEmployeeCommand
            {
                Id = 0,
                Name = name.Trim(),
                CompanyName = "Auxinzio",
                Designation = "Software Engineer",
                Department = "Engineering",
                IsActive = true
            });

            if (result.Succeeded)
            {
                return Json(new { success = true, id = result.Data, name = name.Trim() });
            }

            return Json(new { success = false, message = string.Join("; ", result.Messages) });
        }

        // =========================================================
        // AJAX: DELETE COMPANY
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> QuickDeleteCompany(int id)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                return Json(new { success = false, message = "Access Denied: Only Admin and HR can delete companies." });
            }

            var result = await _mediator.Send(new DeleteCompanyCommand(id));
            return Json(new { success = result.Succeeded, message = string.Join("; ", result.Messages) });
        }

        // =========================================================
        // AJAX: DELETE EMPLOYEE
        // =========================================================

        [HttpPost]
        public async Task<IActionResult> QuickDeleteEmployee(int id)
        {
            if (!_sessionService.IsAdminOrHr())
            {
                return Json(new { success = false, message = "Access Denied: Only Admin and HR can delete employees." });
            }

            var result = await _mediator.Send(new DeleteEmployeeCommand(id));
            return Json(new { success = result.Succeeded, message = string.Join("; ", result.Messages) });
        }

        // =========================================================
        // AJAX: HR OVERDUE EMAIL NOTIFICATION ENDPOINTS
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> GetOverdueTaskPreview()
        {
            var result = await _mediator.Send(new GetOverdueTaskPreviewQuery());
            var tasks = result.Data ?? new List<OverdueTaskPreviewDto>();
            return Json(new { count = tasks.Count, tasks = tasks });
        }

        [HttpPost]
        public async Task<IActionResult> SendOverdueHrNotification(string? toEmail = null, string? ccEmail = null, string? bccEmail = null)
        {
            var result = await _mediator.Send(new GetOverdueTasksEntitiesQuery());
            var overdueTasks = result.Data ?? new List<TaskItem>();

            if (!overdueTasks.Any())
            {
                return Json(new { success = false, message = "No uncompleted overdue tasks found to report." });
            }

            var sent = await _emailService.SendOverdueTasksNotificationToHrAsync(overdueTasks, toEmail, ccEmail, bccEmail);
            if (sent)
            {
                return Json(new
                {
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

        private async Task LoadCompaniesAndEmployeesAsync(TaskViewModel model)
        {
            var companiesResult = await _mediator.Send(new GetAllCompaniesQuery());
            var employeesResult = await _mediator.Send(new GetAllEmployeesQuery());

            var companies = companiesResult.Data ?? new List<Company>();
            var employees = employeesResult.Data ?? new List<Employee>();

            model.Companies = companies
                .OrderBy(c => c.Name)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.Name,
                    Selected = model.SelectedCompanyIds.Contains(c.Id)
                })
                .ToList();

            model.Employees = employees
                .OrderBy(e => e.Name)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = e.Name,
                    Selected = model.SelectedEmployeeIds.Contains(e.Id)
                })
                .ToList();
        }

        // =========================================================
        // GET: /Task/Appreciation
        // =========================================================

        public async Task<IActionResult> Appreciation()
        {
            const string cacheKey = "WallOfFame_Cache_Key";
            if (_memoryCache.TryGetValue(cacheKey, out WeeklyAppreciationPageViewModel? cachedModel) && cachedModel != null)
            {
                return View(cachedModel);
            }

            var result = await _mediator.Send(new GetAppreciationLeaderboardQuery());
            var data = result.Data ?? new WeeklyAppreciationDataDto();

            var leaderboard = new List<EmployeeAppreciationViewModel>();
            for (int i = 0; i < data.Leaderboard.Count; i++)
            {
                var dto = data.Leaderboard[i];
                var rank = i + 1;
                leaderboard.Add(new EmployeeAppreciationViewModel
                {
                    EmployeeId = dto.EmployeeId,
                    EmployeeName = dto.EmployeeName,
                    TotalAssignedTasks = dto.TotalAssignedTasks,
                    CompletedTasksCount = dto.CompletedTasksCount,
                    PerfectTasksCount = dto.PerfectTasksCount,
                    CompletionRate = dto.CompletionRate,
                    PerfectRate = dto.PerfectRate,
                    Rank = rank,
                    IsStarPerformer = rank == 1,
                    AwardTitle = rank switch
                    {
                        1 => "👑 Star Performer & Quality Champion",
                        2 => "🥈 Distinguished Executioner",
                        3 => "🥉 Stellar Contributor",
                        _ => "🌟 Valued Core Pillar"
                    },
                    AwardDescription = rank switch
                    {
                        1 => "Highest number of flawless deliveries with 100% precision.",
                        2 => "Consistent high-tier performance and reliable deliverables.",
                        3 => "Strong delivery consistency and dedication across portfolios.",
                        _ => "Continuous dedication, craftsmanship, and commitment."
                    },
                    BadgeGradient = rank switch
                    {
                        1 => "linear-gradient(135deg, #f59e0b, #b45309)",
                        2 => "linear-gradient(135deg, #94a3b8, #64748b)",
                        3 => "linear-gradient(135deg, #d97706, #78350f)",
                        _ => "linear-gradient(135deg, #3b82f6, #1d4ed8)"
                    },
                    AssignedCompanies = dto.AssignedCompanies,
                    PerfectlyCompletedTasks = dto.PerfectlyCompletedTasks,
                    MotivationalQuote = "Continuous improvement is better than delayed perfection.",
                    MotivationalAuthor = "Mark Twain",
                    GrowthMindsetNote = "Steadfast Excellence",
                    EncouragementTag = rank == 1 ? "Top Performer" : "Valued Team Pillar"
                });
            }

            var model = new WeeklyAppreciationPageViewModel
            {
                Leaderboard = leaderboard,
                StarPerformer = leaderboard.FirstOrDefault(),
                TotalSystemTasks = data.TotalSystemTasks,
                TotalCompletedTasks = data.TotalCompletedTasks,
                TotalPerfectTasks = data.TotalPerfectTasks,
                OverallSystemPerfectionRate = data.OverallSystemPerfectionRate,
                AllActivityLogs = data.AllActivityLogs,
                DailyInspirations = data.DailyInspirations.Select(q => new MotivationalQuoteItem
                {
                    Quote = q.Quote,
                    Author = q.Author,
                    Category = q.Category,
                    Icon = q.Icon
                }).ToList()
            };

            _memoryCache.Set(cacheKey, model, TimeSpan.FromSeconds(30));
            return View(model);
        }

        // =========================================================
        // EXPORT DAILY / RANGE TASKSHEET EXCEL
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> ExportDailyTasksheet(DateTime? startDate, DateTime? endDate, string filter = "all")
        {
            var result = await _mediator.Send(new ExportDailyTasksheetQuery
            {
                StartDate = startDate,
                EndDate = endDate,
                Filter = filter
            });

            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join("; ", result.Messages);
                return RedirectToAction(nameof(Index));
            }

            var (data, fileName) = result.Data;
            return File(data, "text/csv; charset=utf-8", fileName);
        }
    }
}
