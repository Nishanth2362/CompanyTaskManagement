using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using CompanyTaskManagement.Shared.Wrapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CompanyTaskManagement.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled Exception: {Message} | Trace: {TraceId}", ex.Message, httpContext.TraceIdentifier);
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            var isAjax = string.Equals(context.Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
                         || (context.Request.ContentType?.Contains("application/json") == true);

            if (isAjax)
            {
                context.Response.ContentType = "application/json";
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                var response = Result.Fail("An unexpected error occurred while processing your request. Please try again or contact IT.");
                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
            else
            {
                context.Response.Redirect("/Home/Error");
            }
        }
    }
}
