using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using CompanyTaskManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Controllers
{
    public class ProjectController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserSessionService _sessionService;

        public ProjectController(ApplicationDbContext context, IUserSessionService sessionService)
        {
            _context = context;
            _sessionService = sessionService;
        }

        // =========================================================
        // INDEX (Accessible to Admin, HR, Employees)
        // =========================================================
        public async Task<IActionResult> Index(string status = "all", string search = "")
        {
            var query = _context.Projects.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(p => p.ProjectName.ToLower().Contains(s) ||
                                         p.ClientCompany.ToLower().Contains(s) ||
                                         p.Description.ToLower().Contains(s) ||
                                         p.LeadManagerName.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(status) && status.ToLower() != "all")
            {
                query = query.Where(p => p.Status.ToLower() == status.ToLower());
            }

            var projects = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
            var allProjects = await _context.Projects.AsNoTracking().ToListAsync();

            var taskCounts = await _context.Tasks
                .Where(t => t.ProjectId.HasValue)
                .GroupBy(t => t.ProjectId!.Value)
                .Select(g => new { ProjectId = g.Key, Total = g.Count(), Completed = g.Count(t => t.Status == Models.TaskStatus.Completed) })
                .ToDictionaryAsync(g => g.ProjectId, g => (Total: g.Total, Completed: g.Completed));
            ViewBag.ProjectTaskStats = taskCounts;

            ViewBag.SelectedStatus = string.IsNullOrWhiteSpace(status) ? "all" : status.ToLower();
            ViewBag.Search = search;
            ViewBag.TotalProjects = allProjects.Count;
            ViewBag.InProgressCount = allProjects.Count(p => p.Status == "In Progress");
            ViewBag.PlanningCount = allProjects.Count(p => p.Status == "Planning");
            ViewBag.CompletedCount = allProjects.Count(p => p.Status == "Completed");
            ViewBag.OnHoldCount = allProjects.Count(p => p.Status == "On Hold");
            ViewBag.TotalBudget = allProjects.Sum(p => p.Budget ?? 0);

            ViewBag.IsAdmin = _sessionService.IsAdmin();
            ViewBag.CurrentRole = _sessionService.GetCurrentRole();

            return View(projects);
        }

        // =========================================================
        // DETAILS (Accessible to All Roles)
        // =========================================================
        public async Task<IActionResult> Details(int id)
        {
            var project = await _context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (project == null)
            {
                return NotFound();
            }

            ViewBag.IsAdmin = _sessionService.IsAdmin();
            return View(project);
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

            ViewBag.Companies = await _context.Companies.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Employees = await _context.Employees.OrderBy(e => e.Name).ToListAsync();

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
                ViewBag.Companies = await _context.Companies.OrderBy(c => c.Name).ToListAsync();
                ViewBag.Employees = await _context.Employees.OrderBy(e => e.Name).ToListAsync();
                return View(model);
            }

            model.CreatedAt = DateTime.Now;
            _context.Projects.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Project \"{model.ProjectName}\" was created successfully by Administrator.";
            return RedirectToAction(nameof(Index));
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

            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            ViewBag.Companies = await _context.Companies.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Employees = await _context.Employees.OrderBy(e => e.Name).ToListAsync();

            return View(project);
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
                ViewBag.Companies = await _context.Companies.OrderBy(c => c.Name).ToListAsync();
                ViewBag.Employees = await _context.Employees.OrderBy(e => e.Name).ToListAsync();
                return View(model);
            }

            var existing = await _context.Projects.FindAsync(id);
            if (existing == null)
            {
                return NotFound();
            }

            existing.ProjectName = model.ProjectName;
            existing.ClientCompany = model.ClientCompany;
            existing.Description = model.Description;
            existing.Status = model.Status;
            existing.Priority = model.Priority;
            existing.StartDate = model.StartDate;
            existing.TargetEndDate = model.TargetEndDate;
            existing.Budget = model.Budget;
            existing.LeadManagerName = model.LeadManagerName;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Project \"{existing.ProjectName}\" updated successfully.";
            return RedirectToAction(nameof(Index));
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

            var project = await _context.Projects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            var name = project.ProjectName;
            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Project \"{name}\" deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
