using System;
using System.Security.Claims;
using CompanyTaskManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Http;

namespace CompanyTaskManagement.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? UserId =>
            _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? _httpContextAccessor.HttpContext?.Session?.GetString("EmployeeId");

        public string? UserName =>
            _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Name)
            ?? _httpContextAccessor.HttpContext?.Session?.GetString("EmployeeName");

        public string? Role =>
            _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Role)
            ?? _httpContextAccessor.HttpContext?.Session?.GetString("UserRole")
            ?? "Admin";

        public int? EmployeeId
        {
            get
            {
                var val = _httpContextAccessor.HttpContext?.User?.FindFirstValue("EmployeeId")
                          ?? _httpContextAccessor.HttpContext?.Session?.GetString("EmployeeId");
                return int.TryParse(val, out var id) ? id : null;
            }
        }

        public string? IpAddress =>
            _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

        public bool IsAdmin =>
            string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase);

        public bool IsAdminOrHr =>
            string.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Role, "HR", StringComparison.OrdinalIgnoreCase);

        public bool IsEmployee =>
            string.Equals(Role, "Employee", StringComparison.OrdinalIgnoreCase);
    }
}
