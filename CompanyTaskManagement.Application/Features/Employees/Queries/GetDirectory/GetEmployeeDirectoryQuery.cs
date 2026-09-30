using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Entities;
using CompanyTaskManagement.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CompanyTaskManagement.Application.Features.Employees.Queries.GetDirectory
{
    public class EmployeeDirectoryMetrics
    {
        public int TotalEmployees { get; set; }
        public int FrontendCount { get; set; }
        public int BackendCount { get; set; }
        public int DesignCount { get; set; }
        public int TesterCount { get; set; }
        public int InternsCount { get; set; }
        public List<Employee> SeniorMentors { get; set; } = new();
    }

    public class GetEmployeeDirectoryQuery : IRequest<Result<(List<Employee> Employees, EmployeeDirectoryMetrics Metrics)>>
    {
        public string Department { get; set; } = "all";
        public string Search { get; set; } = string.Empty;
    }

    internal class GetEmployeeDirectoryQueryHandler : IRequestHandler<GetEmployeeDirectoryQuery, Result<(List<Employee> Employees, EmployeeDirectoryMetrics Metrics)>>
    {
        private readonly IUnitOfWork<int> _unitOfWork;

        public GetEmployeeDirectoryQueryHandler(IUnitOfWork<int> unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<(List<Employee> Employees, EmployeeDirectoryMetrics Metrics)>> Handle(GetEmployeeDirectoryQuery request, CancellationToken cancellationToken)
        {
            var baseQuery = _unitOfWork.Repository<Employee>().Entities
                .Include(e => e.TaskEmployees)
                    .ThenInclude(te => te.Task)
                .AsNoTracking();

            var query = baseQuery;

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var s = request.Search.Trim().ToLower();
                query = query.Where(e => e.Name.ToLower().Contains(s) ||
                                         (e.Designation != null && e.Designation.ToLower().Contains(s)) ||
                                         (e.Department != null && e.Department.ToLower().Contains(s)) ||
                                         (e.Email != null && e.Email.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(request.Department) && request.Department.ToLower() != "all")
            {
                var dept = request.Department.ToLower();
                if (dept == "intern" || dept == "internship" || dept == "interns")
                {
                    query = query.Where(e => (e.Department != null && (e.Department.ToLower() == "intern" || e.Department.ToLower() == "internship" || e.Department.ToLower() == "interns")) ||
                                             (e.Designation != null && e.Designation.ToLower().Contains("intern")));
                }
                else
                {
                    query = query.Where(e => e.Department != null && e.Department.ToLower() == dept);
                }
            }

            var employees = await query.OrderBy(e => e.Name).ToListAsync(cancellationToken);
            var allEmployees = await baseQuery.ToListAsync(cancellationToken);

            var seniorMentors = allEmployees
                .Where(e => e.Department != "Internship" && e.Department != "Intern" && !(e.Designation != null && e.Designation.ToLower().Contains("intern")))
                .OrderBy(e => e.Name)
                .ToList();

            var metrics = new EmployeeDirectoryMetrics
            {
                TotalEmployees = allEmployees.Count,
                FrontendCount = allEmployees.Count(e => e.Department == "Frontend"),
                BackendCount = allEmployees.Count(e => e.Department == "Backend" || e.Department == "Engineering"),
                DesignCount = allEmployees.Count(e => e.Department == "UI / UX Designer" || e.Department == "Product & Design"),
                TesterCount = allEmployees.Count(e => e.Department == "Tester" || e.Department == "Quality Assurance"),
                InternsCount = allEmployees.Count(e => (e.Department != null && (e.Department.Equals("Intern", StringComparison.OrdinalIgnoreCase) || e.Department.Equals("Internship", StringComparison.OrdinalIgnoreCase) || e.Department.Equals("Interns", StringComparison.OrdinalIgnoreCase))) || (e.Designation != null && e.Designation.ToLower().Contains("intern"))),
                SeniorMentors = seniorMentors
            };

            return await Result<(List<Employee> Employees, EmployeeDirectoryMetrics Metrics)>.SuccessAsync((employees, metrics));
        }
    }
}
