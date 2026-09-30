using System;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.Companies.Queries.GetAll;
using CompanyTaskManagement.Application.Features.Employees.Queries.GetAll;
using CompanyTaskManagement.Application.Features.Projects.Commands.AddEdit;
using CompanyTaskManagement.Application.Features.Projects.Commands.Delete;
using CompanyTaskManagement.Application.Features.Projects.Queries.GetById;
using CompanyTaskManagement.Application.Features.Projects.Queries.GetDashboard;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUserSessionService _sessionService;
        private readonly ILogger<ProjectController> _logger;

        public ProjectController(
            IMediator mediator,
            IUserSessionService sessionService,
            ILogger<ProjectController> logger)
        {
            _mediator = mediator;
            _sessionService = sessionService;
            _logger = logger;
        }

        // =========================================================
        // INDEX (Accessible to Admin, HR, Employees)
        // =========================================================
        public async Task<IActionResult> Index(string status = "all", string search = "")
        {
            var result = await _mediator.Send(new GetProjectDashboardQuery
            {
                Status = status,
                Search = search
            });

            var (projects, metrics) = result.Data;

            ViewBag.ProjectTaskStats = metrics?.TaskStats;
            ViewBag.SelectedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLower();
            ViewBag.Search = search;
            ViewBag.TotalProjects = metrics?.TotalProjects ?? 0;
            ViewBag.InProgressCount = metrics?.InProgressCount ?? 0;
            ViewBag.PlanningCount = metrics?.PlanningCount ?? 0;
            ViewBag.CompletedCount = metrics?.CompletedCount ?? 0;
            ViewBag.OnHoldCount = metrics?.OnHoldCount ?? 0;
            ViewBag.TotalBudget = metrics?.TotalBudget ?? 0;

            ViewBag.IsAdmin = _sessionService.IsAdmin();
            ViewBag.CurrentRole = _sessionService.GetCurrentRole();

            return View(projects);
        }

        // =========================================================
        // DETAILS (Accessible to All Roles)
        // =========================================================
        public async Task<IActionResult> Details(int id)
        {
            var result = await _mediator.Send(new GetProjectByIdQuery(id));
            if (!result.Succeeded || result.Data == null)
            {
                return NotFound();
            }

            ViewBag.IsAdmin = _sessionService.IsAdmin();
            return View(result.Data);
        }

        // =========================================================
        // CREATE - GET (ADMIN ONLY)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to create new projects.";
                return RedirectToAction(nameof(Index));
            }

            var companiesRes = await _mediator.Send(new GetAllCompaniesQuery());
            var employeesRes = await _mediator.Send(new GetAllEmployeesQuery());

            ViewBag.Companies = companiesRes.Data;
            ViewBag.Employees = employeesRes.Data;

            var model = new Project
            {
                Status = "In Progress",
                Priority = "High",
                StartDate = DateTime.Today,
                TargetEndDate = DateTime.Today.AddDays(60)
            };

            return View(model);
        }

        // =========================================================
        // CREATE - POST (ADMIN ONLY)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project model)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to create new projects.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                var companiesRes = await _mediator.Send(new GetAllCompaniesQuery());
                var employeesRes = await _mediator.Send(new GetAllEmployeesQuery());
                ViewBag.Companies = companiesRes.Data;
                ViewBag.Employees = employeesRes.Data;
                return View(model);
            }

            var command = new AddEditProjectCommand
            {
                Id = 0,
                ProjectName = model.ProjectName,
                ClientCompany = model.ClientCompany,
                Description = model.Description,
                Status = model.Status,
                Priority = model.Priority,
                StartDate = model.StartDate,
                TargetEndDate = model.TargetEndDate,
                Budget = model.Budget,
                LeadManagerName = model.LeadManagerName
            };

            var result = await _mediator.Send(command);
            if (result.Succeeded)
            {
                TempData["Success"] = $"Project \"{model.ProjectName}\" was created successfully by Administrator.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = string.Join("; ", result.Messages);
            return View(model);
        }

        // =========================================================
        // EDIT - GET (ADMIN ONLY)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to edit projects.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _mediator.Send(new GetProjectByIdQuery(id));
            if (!result.Succeeded || result.Data == null)
            {
                return NotFound();
            }

            var companiesRes = await _mediator.Send(new GetAllCompaniesQuery());
            var employeesRes = await _mediator.Send(new GetAllEmployeesQuery());

            ViewBag.Companies = companiesRes.Data;
            ViewBag.Employees = employeesRes.Data;

            return View(result.Data);
        }

        // =========================================================
        // EDIT - POST (ADMIN ONLY)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Project model)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to edit projects.";
                return RedirectToAction(nameof(Index));
            }

            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                var companiesRes = await _mediator.Send(new GetAllCompaniesQuery());
                var employeesRes = await _mediator.Send(new GetAllEmployeesQuery());
                ViewBag.Companies = companiesRes.Data;
                ViewBag.Employees = employeesRes.Data;
                return View(model);
            }

            var command = new AddEditProjectCommand
            {
                Id = id,
                ProjectName = model.ProjectName,
                ClientCompany = model.ClientCompany,
                Description = model.Description,
                Status = model.Status,
                Priority = model.Priority,
                StartDate = model.StartDate,
                TargetEndDate = model.TargetEndDate,
                Budget = model.Budget,
                LeadManagerName = model.LeadManagerName
            };

            var result = await _mediator.Send(command);
            if (result.Succeeded)
            {
                TempData["Success"] = $"Project \"{model.ProjectName}\" updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            TempData["Error"] = string.Join("; ", result.Messages);
            return View(model);
        }

        // =========================================================
        // DELETE - POST (ADMIN ONLY)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator can delete projects.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _mediator.Send(new DeleteProjectCommand(id));
            if (result.Succeeded)
            {
                TempData["Success"] = "Project deleted successfully.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
