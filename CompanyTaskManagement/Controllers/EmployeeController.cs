using System;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.Employees.Commands.AddEdit;
using CompanyTaskManagement.Application.Features.Employees.Commands.AddIntern;
using CompanyTaskManagement.Application.Features.Employees.Commands.Delete;
using CompanyTaskManagement.Application.Features.Employees.Queries.GetById;
using CompanyTaskManagement.Application.Features.Employees.Queries.GetDirectory;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Controllers
{
    [Authorize]
    public class EmployeeController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUserSessionService _sessionService;
        private readonly ILogger<EmployeeController> _logger;

        public EmployeeController(
            IMediator mediator,
            IUserSessionService sessionService,
            ILogger<EmployeeController> logger)
        {
            _mediator = mediator;
            _sessionService = sessionService;
            _logger = logger;
        }

        // =========================================================
        // INDEX (Accessible to All Roles)
        // =========================================================
        public async Task<IActionResult> Index(string department = "all", string search = "")
        {
            var result = await _mediator.Send(new GetEmployeeDirectoryQuery
            {
                Department = department,
                Search = search
            });

            var (employees, metrics) = result.Data;

            ViewBag.SelectedDepartment = string.IsNullOrWhiteSpace(department) ? "all" : department;
            ViewBag.Search = search;
            ViewBag.TotalEmployees = metrics?.TotalEmployees ?? 0;
            ViewBag.FrontendCount = metrics?.FrontendCount ?? 0;
            ViewBag.BackendCount = metrics?.BackendCount ?? 0;
            ViewBag.DesignCount = metrics?.DesignCount ?? 0;
            ViewBag.TesterCount = metrics?.TesterCount ?? 0;
            ViewBag.InternsCount = metrics?.InternsCount ?? 0;
            ViewBag.SeniorMentors = metrics?.SeniorMentors;

            ViewBag.IsAdmin = _sessionService.IsAdmin();
            ViewBag.CurrentRole = _sessionService.GetCurrentRole();

            return View(employees);
        }

        // =========================================================
        // DETAILS (Accessible to All Roles)
        // =========================================================
        public async Task<IActionResult> Details(int id)
        {
            var result = await _mediator.Send(new GetEmployeeByIdQuery(id));
            if (!result.Succeeded || result.Data == null)
            {
                return NotFound();
            }

            ViewBag.IsAdmin = _sessionService.IsAdmin();
            return View(result.Data);
        }

        // =========================================================
        // ADD INTERN - POST (ADMIN ONLY)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddIntern(string name, string? email, string? phone, string domain, string? mentorName, DateTime? joinedDate, string? notes)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to add new interns.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _mediator.Send(new AddInternCommand
            {
                Name = name,
                Email = email,
                Phone = phone,
                Domain = domain,
                MentorName = mentorName,
                JoinedDate = joinedDate,
                Notes = notes
            });

            if (result.Succeeded)
            {
                TempData["Success"] = $"🎉 Intern '{name.Trim()}' added successfully to Directory & Internship Hub by Administrator.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index), new { department = "Intern" });
        }

        // =========================================================
        // CREATE - GET (ADMIN ONLY)
        // =========================================================
        [HttpGet]
        public IActionResult Create()
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to add new employees.";
                return RedirectToAction(nameof(Index));
            }

            var model = new Employee
            {
                Department = "Engineering",
                Designation = "Software Engineer",
                IsActive = true
            };

            return View(model);
        }

        // =========================================================
        // CREATE - POST (ADMIN ONLY)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Employee model)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to add new employees.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var command = new AddEditEmployeeCommand
            {
                Id = 0,
                Name = model.Name,
                Email = model.Email,
                CompanyName = string.IsNullOrWhiteSpace(model.CompanyName) ? "Auxinzio" : model.CompanyName.Trim(),
                Designation = model.Designation ?? "Software Engineer",
                Department = model.Department ?? "Engineering",
                Phone = model.Phone,
                IsActive = model.IsActive
            };

            var result = await _mediator.Send(command);
            if (result.Succeeded)
            {
                TempData["Success"] = $"Employee '{model.Name}' added successfully by Administrator.";
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
                TempData["Error"] = "Access Denied: Only Administrator has permission to edit employees.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _mediator.Send(new GetEmployeeByIdQuery(id));
            if (!result.Succeeded || result.Data == null)
            {
                return NotFound();
            }

            return View(result.Data);
        }

        // =========================================================
        // EDIT - POST (ADMIN ONLY)
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Employee model)
        {
            if (!_sessionService.IsAdmin())
            {
                TempData["Error"] = "Access Denied: Only Administrator has permission to edit employees.";
                return RedirectToAction(nameof(Index));
            }

            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var command = new AddEditEmployeeCommand
            {
                Id = id,
                Name = model.Name,
                Email = model.Email,
                CompanyName = string.IsNullOrWhiteSpace(model.CompanyName) ? "Auxinzio" : model.CompanyName.Trim(),
                Designation = model.Designation ?? "Software Engineer",
                Department = model.Department ?? "Engineering",
                Phone = model.Phone,
                IsActive = model.IsActive
            };

            var result = await _mediator.Send(command);
            if (result.Succeeded)
            {
                TempData["Success"] = $"Employee '{model.Name}' updated successfully.";
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
                TempData["Error"] = "Access Denied: Only Administrator has permission to remove employees.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _mediator.Send(new DeleteEmployeeCommand(id));
            if (result.Succeeded)
            {
                TempData["Success"] = "Employee removed from the directory successfully.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
