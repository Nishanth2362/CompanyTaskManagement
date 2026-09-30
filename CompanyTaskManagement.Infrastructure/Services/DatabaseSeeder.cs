using System;
using System.Linq;
using System.Threading.Tasks;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Infrastructure.Services
{
    public class DatabaseSeeder : IDatabaseSeeder
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DatabaseSeeder> _logger;

        public DatabaseSeeder(ApplicationDbContext context, ILogger<DatabaseSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _context.Database.EnsureCreatedAsync();

                // Safely ensure columns exist for older DB instances
                var alterSql = @"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'Priority')
                        ALTER TABLE [Tasks] ADD [Priority] INT NOT NULL DEFAULT 1;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'Status')
                        ALTER TABLE [Tasks] ADD [Status] INT NOT NULL DEFAULT 0;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'DueDate')
                        ALTER TABLE [Tasks] ADD [DueDate] DATETIME2 NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'CreatedAt')
                        ALTER TABLE [Tasks] ADD [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE();

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'Progress')
                        ALTER TABLE [Tasks] ADD [Progress] INT NOT NULL DEFAULT 0;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'DelayReason')
                        ALTER TABLE [Tasks] ADD [DelayReason] NVARCHAR(500) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'ErrorDetails')
                        ALTER TABLE [Tasks] ADD [ErrorDetails] NVARCHAR(1000) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'ErrorScreenshotPath')
                        ALTER TABLE [Tasks] ADD [ErrorScreenshotPath] NVARCHAR(500) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'ProjectId')
                        ALTER TABLE [Tasks] ADD [ProjectId] INT NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'StartDate')
                        ALTER TABLE [Tasks] ADD [StartDate] DATETIME2 NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Tasks]') AND name = 'EndDate')
                        ALTER TABLE [Tasks] ADD [EndDate] DATETIME2 NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'Email')
                        ALTER TABLE [Employees] ADD [Email] NVARCHAR(150) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'CompanyName')
                        ALTER TABLE [Employees] ADD [CompanyName] NVARCHAR(100) NULL DEFAULT 'Auxinzio';

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'Designation')
                        ALTER TABLE [Employees] ADD [Designation] NVARCHAR(100) NULL DEFAULT 'Software Engineer';

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'Department')
                        ALTER TABLE [Employees] ADD [Department] NVARCHAR(100) NULL DEFAULT 'Engineering';

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'Phone')
                        ALTER TABLE [Employees] ADD [Phone] NVARCHAR(50) NULL;

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'CreatedAt')
                        ALTER TABLE [Employees] ADD [CreatedAt] DATETIME2 NOT NULL DEFAULT GETDATE();

                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Employees]') AND name = 'IsActive')
                        ALTER TABLE [Employees] ADD [IsActive] BIT NOT NULL DEFAULT 1;

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserAccounts')
                    BEGIN
                        CREATE TABLE [UserAccounts] (
                            [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [Email] NVARCHAR(150) NOT NULL UNIQUE,
                            [FullName] NVARCHAR(100) NOT NULL,
                            [PasswordHash] NVARCHAR(MAX) NOT NULL,
                            [Role] NVARCHAR(50) NOT NULL DEFAULT 'Employee',
                            [EmployeeId] INT NULL,
                            [IsActive] BIT NOT NULL DEFAULT 1,
                            [LastLoginAt] DATETIME2 NULL,
                            [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                        );
                    END

                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TaskActivityLogs_LoggedAt' AND object_id = OBJECT_ID(N'[TaskActivityLogs]'))
                        CREATE NONCLUSTERED INDEX [IX_TaskActivityLogs_LoggedAt] ON [TaskActivityLogs] ([LoggedAt] DESC);

                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Tasks_CreatedAt' AND object_id = OBJECT_ID(N'[Tasks]'))
                        CREATE NONCLUSTERED INDEX [IX_Tasks_CreatedAt] ON [Tasks] ([CreatedAt] DESC);

                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Tasks_Status' AND object_id = OBJECT_ID(N'[Tasks]'))
                        CREATE NONCLUSTERED INDEX [IX_Tasks_Status] ON [Tasks] ([Status]);
                ";

                try
                {
                    await _context.Database.ExecuteSqlRawAsync(alterSql);
                }
                catch
                {
                    // Catch SQL exceptions gracefully
                }

                // Seed Default User Accounts
                if (!await _context.UserAccounts.AnyAsync())
                {
                    var firstEmployee = await _context.Employees.FirstOrDefaultAsync();

                    await _context.UserAccounts.AddRangeAsync(
                        new UserAccount
                        {
                            Email = "admin@auxinz.io",
                            FullName = "Auxinz Administrator",
                            PasswordHash = PasswordHasher.HashPassword("Admin@123"),
                            Role = "Admin",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        },
                        new UserAccount
                        {
                            Email = "hr@auxinz.io",
                            FullName = "HR Operations Lead",
                            PasswordHash = PasswordHasher.HashPassword("Hr@123"),
                            Role = "HR",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        },
                        new UserAccount
                        {
                            Email = "employee@auxinz.io",
                            FullName = firstEmployee?.Name ?? "Lead Engineer",
                            PasswordHash = PasswordHasher.HashPassword("User@123"),
                            Role = "Employee",
                            EmployeeId = firstEmployee?.Id,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        }
                    );

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Default Admin, HR, and Employee user accounts seeded successfully.");
                }

                // Ensure all employees in the directory have a linked UserAccount so anyone can log in with their email/name
                var allEmployees = await _context.Employees.ToListAsync();
                var existingEmails = (await _context.UserAccounts.Select(u => u.Email.ToLower()).ToListAsync())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var emp in allEmployees)
                {
                    if (await _context.UserAccounts.AnyAsync(u => u.EmployeeId == emp.Id))
                    {
                        continue;
                    }

                    var cleanName = emp.Name.Trim().ToLower().Replace(" ", ".");
                    var candidateEmail = !string.IsNullOrWhiteSpace(emp.Email) && !existingEmails.Contains(emp.Email.Trim().ToLower())
                        ? emp.Email.Trim().ToLower()
                        : $"{cleanName}@auxinz.io";

                    if (existingEmails.Contains(candidateEmail))
                    {
                        candidateEmail = $"{cleanName}{emp.Id}@auxinz.io";
                    }

                    existingEmails.Add(candidateEmail);

                    await _context.UserAccounts.AddAsync(new UserAccount
                    {
                        Email = candidateEmail,
                        FullName = emp.Name.Trim(),
                        PasswordHash = PasswordHasher.HashPassword("User@123"),
                        Role = "Employee",
                        EmployeeId = emp.Id,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                await _context.SaveChangesAsync();

                // Seed Projects if none exist
                if (!await _context.Projects.AnyAsync())
                {
                    await _context.Projects.AddRangeAsync(
                        new Project
                        {
                            ProjectName = "Auxinzio Enterprise Task & AI Suite",
                            ClientCompany = "Auxinzio",
                            Description = "Next-generation multi-company task management, role-based workflows, and automated tracking.",
                            Status = "In Progress",
                            Priority = "Critical",
                            StartDate = DateTime.Today.AddDays(-14),
                            TargetEndDate = DateTime.Today.AddDays(45),
                            Budget = 75000,
                            LeadManagerName = "Srithar",
                            CreatedAt = DateTime.UtcNow.AddDays(-14)
                        },
                        new Project
                        {
                            ProjectName = "Ameobatronics Robotics Automation Engine",
                            ClientCompany = "Ameobatronics",
                            Description = "Industrial IoT and firmware coordination platform for warehouse automation.",
                            Status = "In Progress",
                            Priority = "High",
                            StartDate = DateTime.Today.AddDays(-30),
                            TargetEndDate = DateTime.Today.AddDays(60),
                            Budget = 120000,
                            LeadManagerName = "Mujimal",
                            CreatedAt = DateTime.UtcNow.AddDays(-30)
                        }
                    );
                    await _context.SaveChangesAsync();
                }

                _logger.LogInformation("Database initialized and verified successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Database initialization warning: {Message}", ex.Message);
            }
        }
    }
}
