namespace CompanyTaskManagement.Application.Interfaces.Services
{
    public interface ICurrentUserService
    {
        string? UserId { get; }
        string? UserName { get; }
        string? Role { get; }
        int? EmployeeId { get; }
        string? IpAddress { get; }
        bool IsAdmin { get; }
        bool IsAdminOrHr { get; }
        bool IsEmployee { get; }
    }
}
