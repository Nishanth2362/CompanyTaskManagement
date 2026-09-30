using System;
using System.IO;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.Employees.Queries.GetAll;
using CompanyTaskManagement.Application.Features.Grievance.Commands;
using CompanyTaskManagement.Application.Features.Grievance.Queries;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Controllers
{
    [Authorize]
    public class GrievanceController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GrievanceController> _logger;

        public GrievanceController(
            IMediator mediator,
            IEmailService emailService,
            IWebHostEnvironment environment,
            ILogger<GrievanceController> logger)
        {
            _mediator = mediator;
            _emailService = emailService;
            _environment = environment;
            _logger = logger;
        }

        // =========================================================
        // GET: /Grievance
        // Grievance Portal & HR Case Dashboard
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Index(string filter = "all", string? search = null, string? category = null)
        {
            var result = await _mediator.Send(new GetGrievancesQuery
            {
                Filter = filter,
                Search = search,
                Category = category
            });

            var data = result.Data ?? new GrievancesDashboardData();

            ViewBag.CurrentFilter = filter;
            ViewBag.Search = search;
            ViewBag.SelectedCategory = category;
            ViewBag.AllCount = data.AllCount;
            ViewBag.SubmittedCount = data.SubmittedCount;
            ViewBag.InReviewCount = data.InReviewCount;
            ViewBag.ResolvedCount = data.ResolvedCount;
            ViewBag.UrgentCount = data.UrgentCount;

            ViewBag.Categories = new System.Collections.Generic.List<string>
            {
                "Workplace Environment",
                "Workload & Deadlines",
                "Interpersonal Conflict",
                "Compensation & Payroll",
                "Technical & Hardware Issue",
                "Harassment & Conduct",
                "Process & Management",
                "Internship Experience",
                "General Feedback"
            };

            return View(data.Complaints);
        }

        // =========================================================
        // GET: /Grievance/Submit
        // Confidential Submission Form
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Submit()
        {
            var employeesResult = await _mediator.Send(new GetAllEmployeesQuery());
            ViewBag.Employees = employeesResult.Data;
            return View(new HrComplaint { Priority = "Medium", Category = "Workplace Environment" });
        }

        // =========================================================
        // POST: /Grievance/Submit
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(HrComplaint model, IFormFile? attachmentFile)
        {
            if (!model.IsAnonymous && model.EmployeeId == null && string.IsNullOrWhiteSpace(model.SubmitterName))
            {
                ModelState.AddModelError(nameof(model.SubmitterName), "Please enter your name or check 'Submit Anonymously'.");
            }

            if (!ModelState.IsValid)
            {
                var employeesResult = await _mediator.Send(new GetAllEmployeesQuery());
                ViewBag.Employees = employeesResult.Data;
                return View(model);
            }

            string? uploadedPath = null;
            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                uploadedPath = await SaveGrievanceAttachmentAsync(attachmentFile);
            }

            var result = await _mediator.Send(new SubmitGrievanceCommand
            {
                Complaint = model,
                AttachmentPath = uploadedPath
            });

            var savedComplaint = result.Data;

            // Notify HR via email service
            try
            {
                var hrEmailBody = $@"A new employee grievance complaint has been submitted:
Ticket Number: {savedComplaint.TicketNumber}
Category: {savedComplaint.Category}
Priority: {savedComplaint.Priority}
Submitter: {(savedComplaint.IsAnonymous ? "Anonymous Contributor" : savedComplaint.SubmitterName)}
Subject: {savedComplaint.Subject}

Please log into the Company Task Management Portal to review this incident.";

                await _emailService.SendEmailAsync("hr@auxinz.io", $"[Confidential] New HR Grievance Ticket - {savedComplaint.TicketNumber}", hrEmailBody);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send HR alert email for grievance {Ticket}", savedComplaint.TicketNumber);
            }

            TempData["Success"] = $"Your grievance has been submitted securely under Ticket #{savedComplaint.TicketNumber}. Human Resources has been notified.";
            return RedirectToAction(nameof(Confirmation), new { ticket = savedComplaint.TicketNumber });
        }

        // =========================================================
        // GET: /Grievance/Confirmation
        // =========================================================
        [HttpGet]
        public IActionResult Confirmation(string ticket)
        {
            ViewBag.Ticket = ticket;
            return View();
        }

        // =========================================================
        // GET: /Grievance/Details/5
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var result = await _mediator.Send(new GetGrievanceByIdQuery(id));
            if (!result.Succeeded || result.Data == null)
            {
                return NotFound();
            }

            return View(result.Data);
        }

        // =========================================================
        // POST: /Grievance/UpdateStatus
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status, string? resolutionSummary)
        {
            var resolvedBy = User.Identity?.Name ?? "HR Administration";
            var result = await _mediator.Send(new UpdateGrievanceStatusCommand
            {
                Id = id,
                Status = status,
                ResolutionSummary = resolutionSummary,
                ResolvedBy = resolvedBy
            });

            if (result.Succeeded)
            {
                TempData["Success"] = result.Messages.Count > 0 ? result.Messages[0] : "Status updated successfully.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // =========================================================
        // POST: /Grievance/AddInternalNote
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddInternalNote(int id, string authorName, string noteText)
        {
            if (string.IsNullOrWhiteSpace(noteText))
            {
                TempData["Error"] = "Note text cannot be empty.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var author = !string.IsNullOrWhiteSpace(authorName) ? authorName : (User.Identity?.Name ?? "HR Investigator");
            var result = await _mediator.Send(new AddGrievanceInternalNoteCommand
            {
                Id = id,
                AuthorName = author,
                NoteText = noteText
            });

            if (result.Succeeded)
            {
                TempData["Success"] = "Internal note added.";
            }
            else
            {
                TempData["Error"] = string.Join("; ", result.Messages);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // =========================================================
        // Private File Helper
        // =========================================================

        private async Task<string> SaveGrievanceAttachmentAsync(IFormFile file)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "grievances");
            Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"grievance_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/grievances/{uniqueFileName}";
        }
    }
}
