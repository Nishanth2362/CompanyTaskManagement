using System;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Interfaces.Services;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Infrastructure.Configurations;
using CompanyTaskManagement.Infrastructure.Contexts;
using CompanyTaskManagement.Infrastructure.Repositories;
using CompanyTaskManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyTaskManagement.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Database
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    sqlOptions => sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)
                );
                options.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            // Memory Cache
            services.AddMemoryCache();

            // Repositories & Unit of Work
            services.AddRepositories();

            // Email & System Services
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services.AddTransient<IMailService, MailService>();
            services.AddTransient<IDateTimeService, DateTimeService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<IDatabaseSeeder, DatabaseSeeder>();

            return services;
        }

        public static IServiceCollection AddRepositories(this IServiceCollection services)
        {
            services.AddTransient(typeof(IRepositoryAsync<,>), typeof(RepositoryAsync<,>));
            services.AddTransient(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));
            return services;
        }

        public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
            await seeder.InitializeAsync();
        }
    }
}
