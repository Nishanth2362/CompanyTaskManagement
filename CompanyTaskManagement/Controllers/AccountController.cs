using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Features.Identity.Commands.ForgotPassword;
using CompanyTaskManagement.Application.Features.Identity.Commands.Register;
using CompanyTaskManagement.Application.Features.Identity.Queries.GetEmployeeName;
using CompanyTaskManagement.Application.Features.Identity.Queries.ValidateLogin;
using CompanyTaskManagement.Domain.Enums;
using CompanyTaskManagement.Services;
using CompanyTaskManagement.ViewModels;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Controllers
{
    public class AccountController : Controller
    {
        private readonly IMediator _mediator;
        private readonly IUserSessionService _sessionService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IMediator mediator,
            IUserSessionService sessionService,
            ILogger<AccountController> logger)
        {
            _mediator = mediator;
            _sessionService = sessionService;
            _logger = logger;
        }

        // =========================================================
        // LOGIN (GET & POST)
        // =========================================================

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(returnUrl) && User.Identity?.IsAuthenticated == true)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                HttpContext.Session.Clear();
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _mediator.Send(new ValidateLoginQuery
            {
                Email = model.Email,
                Password = model.Password
            });

            if (!result.Succeeded || result.Data == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email address or password.");
                return View(model);
            }

            var user = result.Data;

            // Create Claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            };

            if (user.EmployeeId.HasValue)
            {
                claims.Add(new Claim("EmployeeId", user.EmployeeId.Value.ToString()));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Sync with Session for backward compatibility with legacy views
            if (Enum.TryParse<UserRole>(user.Role, out var roleEnum))
            {
                _sessionService.SetRole(roleEnum, user.EmployeeId, user.FullName);
            }

            _logger.LogInformation("User {Email} logged in with role {Role}.", user.Email, user.Role);

            if (!string.IsNullOrEmpty(model.ReturnUrl) 
                && Url.IsLocalUrl(model.ReturnUrl) 
                && !model.ReturnUrl.Equals("/", StringComparison.OrdinalIgnoreCase)
                && !model.ReturnUrl.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(model.ReturnUrl);
            }

            return RedirectToAction("Index", "Task");
        }

        // =========================================================
        // SIGN UP / REGISTER (GET & POST)
        // =========================================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _mediator.Send(new RegisterUserCommand
            {
                Email = model.Email,
                FullName = model.GetEffectiveFullName(),
                Password = model.Password,
                Role = model.Role,
                Phone = model.Phone,
                Department = model.Department,
                Designation = model.Designation
            });

            if (!result.Succeeded)
            {
                ModelState.AddModelError("Email", string.Join("; ", result.Messages));
                return View(model);
            }

            TempData["SuccessMessage"] = result.Messages.Count > 0 ? result.Messages[0] : "Registration successful! Please sign in below.";
            return RedirectToAction("Login", "Account");
        }

        // =========================================================
        // FORGOT PASSWORD (GET & POST)
        // =========================================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _mediator.Send(new ForgotPasswordCommand
            {
                Email = model.Email,
                NewPassword = model.NewPassword
            });

            if (!result.Succeeded)
            {
                ModelState.AddModelError("Email", string.Join("; ", result.Messages));
                return View(model);
            }

            TempData["SuccessMessage"] = result.Messages.Count > 0 ? result.Messages[0] : "Password updated successfully!";
            return RedirectToAction("Login", "Account");
        }

        // =========================================================
        // LOGOUT
        // =========================================================

        [HttpPost]
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            _logger.LogInformation("User signed out.");
            return RedirectToAction(nameof(Login));
        }

        // =========================================================
        // ACCESS DENIED
        // =========================================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // =========================================================
        // ROLE SWITCHER (SECURED: ADMIN ONLY)
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SwitchRole(string role, int? employeeId, string? returnUrl)
        {
            var currentRole = _sessionService.GetCurrentRole();
            if (User.Identity?.IsAuthenticated == true && !User.IsInRole("Admin") && currentRole != UserRole.Admin)
            {
                TempData["Error"] = "Security Warning: Only Administrators are authorized to switch role perspectives.";
                return Redirect(returnUrl ?? "/Task");
            }

            if (Enum.TryParse<UserRole>(role, out var parsedRole))
            {
                string? employeeName = null;
                if (parsedRole == UserRole.Employee && employeeId.HasValue)
                {
                    var nameResult = await _mediator.Send(new GetEmployeeNameQuery(employeeId.Value));
                    employeeName = nameResult.Data ?? "Employee";
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

            return Redirect(returnUrl ?? "/Task");
        }
    }
}
