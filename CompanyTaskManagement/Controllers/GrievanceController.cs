using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using CompanyTaskManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Controllers
{
    public class GrievanceController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<GrievanceController> _logger;

        public GrievanceController(ApplicationDbContext context, IEmailService emailService, IWebHostEnvironment environment, ILogger<GrievanceController> logger)
        {
            _context = context;
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
            var query = _context.HrComplaints
                .Include(c => c.Employee)
                .AsNoTracking()
                .AsQueryable();

            // Category filter
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(c => c.Category == category);
            }

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower().Trim();
                query = query.Where(c => c.Subject.ToLower().Contains(s) 
                                      || c.TicketNumber.ToLower().Contains(s) 
                                      || (c.SubmitterName != null && c.SubmitterName.ToLower().Contains(s))
                                      || c.Description.ToLower().Contains(s));
            }

            var allList = await query.ToListAsync();

            int allCount = allList.Count;
            int submittedCount = allList.Count(c => c.Status == "Submitted");
            int inReviewCount = allList.Count(c => c.Status == "Under Review" || c.Status == "In Investigation");
            int resolvedCount = allList.Count(c => c.Status == "Resolved");
            int urgentCount = allList.Count(c => c.Priority == "Urgent" && c.Status != "Resolved" && c.Status != "Dismissed");

            List<HrComplaint> filtered;
            switch (filter.ToLower())
            {
                case "submitted":
                    filtered = allList.Where(c => c.Status == "Submitted").OrderByDescending(c => c.CreatedAt).ToList();
                    break;
                case "inreview":
                    filtered = allList.Where(c => c.Status == "Under Review" || c.Status == "In Investigation").OrderByDescending(c => c.CreatedAt).ToList();
                    break;
                case "resolved":
                    filtered = allList.Where(c => c.Status == "Resolved" || c.Status == "Dismissed").OrderByDescending(c => c.ResolvedAt ?? c.CreatedAt).ToList();
                    break;
                case "urgent":
                    filtered = allList.Where(c => c.Priority == "Urgent").OrderByDescending(c => c.CreatedAt).ToList();
                    break;
                default:
                    filter = "all";
                    filtered = allList.OrderByDescending(c => c.CreatedAt).ToList();
                    break;
            }

            ViewBag.CurrentFilter = filter;
            ViewBag.Search = search;
            ViewBag.SelectedCategory = category;
            ViewBag.AllCount = allCount;
            ViewBag.SubmittedCount = submittedCount;
            ViewBag.InReviewCount = inReviewCount;
            ViewBag.ResolvedCount = resolvedCount;
            ViewBag.UrgentCount = urgentCount;

            ViewBag.Categories = new List<string>
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

            return View(filtered);
        }

        // =========================================================
        // GET: /Grievance/Submit
        // Confidential Submission Form
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Submit()
        {
            var employees = await _context.Employees
                .OrderBy(e => e.Name)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Employees = employees;
            return View(new HrComplaint { Priority = "Medium", Category = "Workplace Environment" });
        }

        // =========================================================
        // POST: /Grievance/Submit
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(HrComplaint model, IFormFile? attachmentFile)
        {
            if (model.IsAnonymous)
            {
                model.EmployeeId = null;
                model.SubmitterName = "Anonymous Contributor";
                model.SubmitterEmail = null;
            }
            else
            {
                if (model.EmployeeId.HasValue)
                {
                    var emp = await _context.Employees.FindAsync(model.EmployeeId.Value);
                    if (emp != null && string.IsNullOrWhiteSpace(model.SubmitterName))
                    {
                        model.SubmitterName = emp.Name;
                    }
                }

                if (string.IsNullOrWhiteSpace(model.SubmitterName))
                {
                    ModelState.AddModelError(nameof(model.SubmitterName), "Please enter your name or check 'Submit Anonymously'.");
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Employees = await _context.Employees.OrderBy(e => e.Name).ToListAsync();
                return View(model);
            }

            if (attachmentFile != null && attachmentFile.Length > 0)
            {
                model.AttachmentPath = await SaveGrievanceAttachmentAsync(attachmentFile);
            }

            model.CreatedAt = DateTime.Now;
            model.Status = "Submitted";
            if (string.IsNullOrWhiteSpace(model.TicketNumber))
            {
                model.TicketNumber = $"HRG-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
            }

            _context.HrComplaints.Add(model);
            await _context.SaveChangesAsync();

            // Send notification to HR via email service
            try
            {
                await _emailService.SendGrievanceNotificationToHrAsync(model);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not send HR email notification for complaint {Ticket}", model.TicketNumber);
            }

            TempData["Success"] = $"Your confidential grievance has been registered under Ticket #{model.TicketNumber}. HR has been notified.";
            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // GET: /Grievance/Details/1
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var complaint = await _context.HrComplaints
                .Include(c => c.Employee)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (complaint == null)
            {
                return NotFound();
            }

            return View(complaint);
        }

        // =========================================================
        // POST: /Grievance/UpdateStatus
        // HR Action / Status / Notes Update
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, string status, string? notes, string? investigatorName)
        {
            var complaint = await _context.HrComplaints.FindAsync(id);
            if (complaint == null)
            {
                return NotFound();
            }

            complaint.Status = status;
            if (!string.IsNullOrWhiteSpace(notes))
            {
                complaint.HrResponseNotes = notes;
            }
            if (!string.IsNullOrWhiteSpace(investigatorName))
            {
                complaint.HrInvestigatorName = investigatorName;
            }

            if (status == "Resolved" || status == "Dismissed")
            {
                complaint.ResolvedAt = DateTime.Now;
            }
            else
            {
                complaint.ResolvedAt = null;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Ticket #{complaint.TicketNumber} status updated to \"{status}\".";

            return RedirectToAction(nameof(Details), new { id = complaint.Id });
        }

        // =========================================================
        // POST: /Grievance/Delete/1
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var complaint = await _context.HrComplaints.FindAsync(id);
            if (complaint != null)
            {
                _context.HrComplaints.Remove(complaint);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Ticket #{complaint.TicketNumber} deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<string> SaveGrievanceAttachmentAsync(IFormFile file)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "grievances");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

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
