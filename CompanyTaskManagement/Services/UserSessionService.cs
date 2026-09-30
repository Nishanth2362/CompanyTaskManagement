using CompanyTaskManagement.Models;
using Microsoft.AspNetCore.Http;

namespace CompanyTaskManagement.Services
{
    public class UserSessionService : IUserSessionService
    {
        private const string SessionKeyRole = "TaskFlow_UserRole";
        private const string SessionKeyEmployeeId = "TaskFlow_EmployeeId";
        private const string SessionKeyEmployeeName = "TaskFlow_EmployeeName";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserSessionService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public UserRole GetCurrentRole()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var user = _httpContextAccessor.HttpContext?.User;

            if (session != null)
            {
                var roleStr = session.GetString(SessionKeyRole);
                if (Enum.TryParse<UserRole>(roleStr, out var role))
                {
                    return role;
                }
            }

            if (user?.Identity?.IsAuthenticated == true)
            {
                if (user.IsInRole("Admin")) return UserRole.Admin;
                if (user.IsInRole("HR")) return UserRole.HR;
                if (user.IsInRole("Employee")) return UserRole.Employee;
            }

            return UserRole.Employee;
        }

        public int? GetCurrentEmployeeId()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            return session?.GetInt32(SessionKeyEmployeeId);
        }

        public string GetCurrentEmployeeName()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            return session?.GetString(SessionKeyEmployeeName) ?? "Team Member";
        }

        public bool IsAdmin() => GetCurrentRole() == UserRole.Admin;

        public bool IsHr() => GetCurrentRole() == UserRole.HR;

        public bool IsAdminOrHr()
        {
            var role = GetCurrentRole();
            return role == UserRole.Admin || role == UserRole.HR;
        }

        public bool IsEmployee() => GetCurrentRole() == UserRole.Employee;

        public void SetRole(UserRole role, int? employeeId = null, string? employeeName = null)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return;

            session.SetString(SessionKeyRole, role.ToString());

            if (role == UserRole.Employee && employeeId.HasValue)
            {
                session.SetInt32(SessionKeyEmployeeId, employeeId.Value);
                session.SetString(SessionKeyEmployeeName, employeeName ?? "Employee");
            }
            else
            {
                session.Remove(SessionKeyEmployeeId);
                session.Remove(SessionKeyEmployeeName);
            }
        }
    }
}
