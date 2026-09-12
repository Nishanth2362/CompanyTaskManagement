using CompanyTaskManagement.Data;
using CompanyTaskManagement.Models;
using CompanyTaskManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace CompanyTaskManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserSessionService _sessionService;
        private readonly ApplicationDbContext _context;

        public AccountController(IUserSessionService sessionService, ApplicationDbContext context)
        {
            _sessionService = sessionService;
            _context = context;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SwitchRole(string role, int? employeeId, string? returnUrl)
        {
            if (Enum.TryParse<UserRole>(role, out var parsedRole))
            {
                string? employeeName = null;
                if (parsedRole == UserRole.Employee && employeeId.HasValue)
                {
                    var employee = await _context.Employees.FindAsync(employeeId.Value);
                    employeeName = employee?.Name ?? "Employee";
                }

                _sessionService.SetRole(parsedRole, employeeId, employeeName);

                if (parsedRole == UserRole.Admin)
                {
                    TempData["Success"] = "Switched to Administrator role (Full access to all tasks, companies, and assignments).";
                }
                else if (parsedRole == UserRole.HR)
                {
                    TempData["Success"] = "Switched to HR Manager role (Authorized for task assignments and human resources).";
                }
                else
                {
                    TempData["Success"] = $"Switched to Employee view: {employeeName ?? "Team Member"}. (Task creation restricted to Admin/HR).";
                }
            }

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Task");
        }
    }
}
