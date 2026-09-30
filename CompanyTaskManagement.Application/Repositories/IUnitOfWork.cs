using System;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Domain.Contract;

namespace CompanyTaskManagement.Application.Repositories
{
    public interface IUnitOfWork<TId> : IDisposable
    {
        IRepositoryAsync<T, TId> Repository<T>() where T : class, IEntity<TId>;

        Task<int> Commit(CancellationToken cancellationToken = default);

        Task<int> CommitAndRemoveCache(CancellationToken cancellationToken = default, params string[] cacheKeys);

        Task Rollback();
    }
}
