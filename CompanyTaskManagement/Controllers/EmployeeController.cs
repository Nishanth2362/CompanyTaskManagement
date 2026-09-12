using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using CompanyTaskManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserSessionService _sessionService;

        public EmployeeController(ApplicationDbContext context, IUserSessionService sessionService)
        {
            _context = context;
            _sessionService = sessionService;
        }

        // =========================================================
        // INDEX (Accessible to All Roles)
        // =========================================================
        public async Task<IActionResult> Index(string department = "all", string search = "")
        {
            var query = _context.Employees
                .Include(e => e.TaskEmployees)
                    .ThenInclude(te => te.Task)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(e => e.Name.ToLower().Contains(s) ||
                                         (e.Designation != null && e.Designation.ToLower().Contains(s)) ||
                                         (e.Department != null && e.Department.ToLower().Contains(s)) ||
                                         (e.Email != null && e.Email.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(department) && department.ToLower() != "all")
            {
                if (department.Equals("Internship", StringComparison.OrdinalIgnoreCase) || department.Equals("Interns", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(e => (e.Department != null && e.Department.ToLower() == "internship") ||
                                             (e.Designation != null && e.Designation.ToLower().Contains("intern")));
                }
                else
                {
                    query = query.Where(e => e.Department != null && e.Department.ToLower() == department.ToLower());
                }
            }

            var employees = await query.OrderBy(e => e.Name).ToListAsync();
            var allEmployees = await _context.Employees.Include(e => e.TaskEmployees).AsNoTracking().ToListAsync();

            ViewBag.SelectedDepartment = string.IsNullOrWhiteSpace(department) ? "all" : department;
            ViewBag.Search = search;
            ViewBag.TotalEmployees = allEmployees.Count;
            ViewBag.FrontendCount = allEmployees.Count(e => e.Department == "Frontend");
            ViewBag.BackendCount = allEmployees.Count(e => e.Department == "Backend" || e.Department == "Engineering");
            ViewBag.DesignCount = allEmployees.Count(e => e.Department == "UI / UX Designer" || e.Department == "Product & Design");
            ViewBag.TesterCount = allEmployees.Count(e => e.Department == "Tester" || e.Department == "Quality Assurance");
            ViewBag.InternsCount = allEmployees.Count(e => (e.Department != null && (e.Department.Equals("Intern", StringComparison.OrdinalIgnoreCase) || e.Department.Equals("Internship", StringComparison.OrdinalIgnoreCase))) || (e.Designation != null && e.Designation.ToLower().Contains("intern")));

            // Senior team leads for mentor assignment dropdown
            ViewBag.SeniorMentors = allEmployees
                .Where(e => e.Department != "Internship" && !(e.Designation != null && e.Designation.ToLower().Contains("intern")))
                .OrderBy(e => e.Name)
                .ToList();

            ViewBag.IsAdmin = _sessionService.IsAdmin();
            ViewBag.CurrentRole = _sessionService.GetCurrentRole();

            return View(employees);
        }

        // =========================================================
        // DETAILS (Accessible to All Roles)
        // =========================================================
        public async Task<IActionResult> Details(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.TaskEmployees)
                    .ThenInclude(te => te.Task)
                        .ThenInclude(t => t.Project)
                .Include(e => e.TaskEmployees)
                    .ThenInclude(te => te.Task)
                        .ThenInclude(t => t.TaskCompanies)
                            .ThenInclude(tc => tc.Company)
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            ViewBag.IsAdmin = _sessionService.IsAdmin();
            return View(employee);
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

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["Error"] = "Intern name is required.";
                return RedirectToAction(nameof(Index));
            }

            var trimmedName = name.Trim();
            var existingEmp = await _context.Employees.FirstOrDefaultAsync(e => e.Name.ToLower() == trimmedName.ToLower());
            
            if (existingEmp == null)
            {
                var newEmp = new Employee
                {
                    Name = trimmedName,
                    Email = !string.IsNullOrWhiteSpace(email) ? email.Trim() : $"{trimmedName.ToLower().Replace(" ", ".")}@auxinz.io",
                    Phone = phone,
                    Department = "Internship",
                    Designation = $"Software Intern ({domain})",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
                _context.Employees.Add(newEmp);
            }
            else
            {
                existingEmp.Department = "Internship";
                if (string.IsNullOrWhiteSpace(existingEmp.Designation) || !existingEmp.Designation.Contains("Intern"))
                {
                    existingEmp.Designation = $"Software Intern ({domain})";
                }
            }

            // Sync with InternshipMembers table
            var existingMember = await _context.InternshipMembers.FirstOrDefaultAsync(m => m.Name.ToLower() == trimmedName.ToLower());
            if (existingMember == null)
            {
                _context.InternshipMembers.Add(new InternshipMember
                {
                    Name = trimmedName,
                    Email = !string.IsNullOrWhiteSpace(email) ? email.Trim() : $"{trimmedName.ToLower().Replace(" ", ".")}@auxinz.io",
                    Role = "Intern",
                    Domain = string.IsNullOrWhiteSpace(domain) ? "Backend .NET / C#" : domain,
                    MentorName = mentorName,
                    Status = "Active",
                    JoinedDate = joinedDate ?? DateTime.Today,
                    Notes = notes
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"🎉 Intern '{trimmedName}' added successfully to Directory & Internship Hub by Administrator.";
            return RedirectToAction(nameof(Index), new { department = "Internship" });
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

            var trimmedName = model.Name.Trim();
            var existing = await _context.Employees.FirstOrDefaultAsync(e => e.Name.ToLower() == trimmedName.ToLower());
            if (existing != null)
            {
                ModelState.AddModelError("Name", $"An employee with the name '{trimmedName}' already exists.");
                return View(model);
            }

            model.Name = trimmedName;
            model.CreatedAt = DateTime.Now;
            _context.Employees.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Employee '{model.Name}' added successfully by Administrator.";
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
                TempData["Error"] = "Access Denied: Only Administrator has permission to edit employees.";
                return RedirectToAction(nameof(Index));
            }

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
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

            var employee = await _context.Employees.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }

            var trimmedName = model.Name.Trim();
            var existing = await _context.Employees.FirstOrDefaultAsync(e => e.Id != id && e.Name.ToLower() == trimmedName.ToLower());
            if (existing != null)
            {
                ModelState.AddModelError("Name", $"Another employee with the name '{trimmedName}' already exists.");
                return View(model);
            }

            employee.Name = trimmedName;
            employee.Email = model.Email?.Trim();
            employee.Designation = model.Designation?.Trim();
            employee.Department = model.Department?.Trim();
            employee.Phone = model.Phone?.Trim();
            employee.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Employee '{employee.Name}' updated successfully.";
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
                TempData["Error"] = "Access Denied: Only Administrator has permission to remove employees.";
                return RedirectToAction(nameof(Index));
            }

            var employee = await _context.Employees
                .Include(e => e.TaskEmployees)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            var empName = employee.Name;
            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Employee '{empName}' removed from the directory.";
            return RedirectToAction(nameof(Index));
        }
    }
}
