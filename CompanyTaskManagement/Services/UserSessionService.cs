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
            if (session == null) return UserRole.Admin;

            var roleStr = session.GetString(SessionKeyRole);
            if (Enum.TryParse<UserRole>(roleStr, out var role))
            {
                return role;
            }

            return UserRole.Admin; // Default to Admin for full experience out-of-the-box
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
