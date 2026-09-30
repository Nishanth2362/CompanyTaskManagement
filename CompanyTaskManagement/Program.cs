using CompanyTaskManagement.Application.Extensions;
using CompanyTaskManagement.Extensions;
using CompanyTaskManagement.Infrastructure.Configurations;
using CompanyTaskManagement.Infrastructure.Extensions;
using CompanyTaskManagement.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add MVC services with Global Authorization Filter (Secure by Default)
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

// HTTPS Redirection
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    options.HttpsPort = 7222;
});

// Clean Architecture Layers
builder.Services.AddApplicationLayer();
builder.Services.AddInfrastructure(builder.Configuration);


// HttpContext and Session
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Cookie Authentication (Production-grade Claims Authentication)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
    options.AddPolicy("AdminOrHR", p => p.RequireRole("Admin", "HR"));
});

// Legacy UserSessionService adapter
builder.Services.AddScoped<IUserSessionService, UserSessionService>();

// Email Settings & Legacy Email Service mapping
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<CompanyTaskManagement.Services.IEmailService, CompanyTaskManagement.Services.EmailService>();

// Background Workers
builder.Services.AddHostedService<OverdueTaskNotifierService>();
builder.Services.AddHostedService<CompanyTaskManagement.Infrastructure.Services.TaskRolloverBackgroundService>();

var app = builder.Build();

// Auto-initialize DB schema & seed data via Infrastructure DatabaseSeeder
await app.Services.InitializeDatabaseAsync();

// HTTP Request Pipeline
app.UseCustomExceptionHandling(app.Environment);

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Root URL directly loads the Authentication (Login) page
app.MapControllerRoute(
    name: "root",
    pattern: "",
    defaults: new { controller = "Account", action = "Login" });

// Standard MVC Route for Controllers and Actions (Secured by Global AuthorizeFilter)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.Run();