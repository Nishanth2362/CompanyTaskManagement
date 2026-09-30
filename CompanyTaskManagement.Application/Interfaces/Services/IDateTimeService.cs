using System;

namespace CompanyTaskManagement.Application.Interfaces.Services
{
    public interface IDateTimeService
    {
        DateTime NowUtc { get; }
        DateTime Now { get; }
        DateTime Today { get; }
    }
}
