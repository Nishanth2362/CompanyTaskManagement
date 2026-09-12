using CompanyTaskManagement.Models;

namespace CompanyTaskManagement.Services
{
    public interface IUserSessionService
    {
        UserRole GetCurrentRole();
        int? GetCurrentEmployeeId();
        string GetCurrentEmployeeName();
        bool IsAdmin();
        bool IsHr();
        bool IsAdminOrHr();
        bool IsEmployee();
        void SetRole(UserRole role, int? employeeId = null, string? employeeName = null);
    }
}
