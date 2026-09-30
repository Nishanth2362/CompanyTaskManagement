using System;
using CompanyTaskManagement.Application.Interfaces.Services;

namespace CompanyTaskManagement.Infrastructure.Services
{
    public class DateTimeService : IDateTimeService
    {
        public DateTime NowUtc => DateTime.UtcNow;
        public DateTime Now => DateTime.Now;
        public DateTime Today => DateTime.Today;
    }
}
