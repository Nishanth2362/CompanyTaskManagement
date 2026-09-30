namespace CompanyTaskManagement.Infrastructure.Services
{
    public static class PasswordHasher
    {
        public static string HashPassword(string password) =>
            CompanyTaskManagement.Shared.Security.PasswordHasher.HashPassword(password);

        public static bool VerifyPassword(string hash, string password) =>
            CompanyTaskManagement.Shared.Security.PasswordHasher.VerifyPassword(hash, password);
    }
}
