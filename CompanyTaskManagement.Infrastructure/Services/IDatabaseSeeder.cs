using System.Threading.Tasks;

namespace CompanyTaskManagement.Infrastructure.Services
{
    public interface IDatabaseSeeder
    {
        Task InitializeAsync();
    }
}
