using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using CompanyTaskManagement.Application.Repositories;
using CompanyTaskManagement.Domain.Contract;
using CompanyTaskManagement.Infrastructure.Contexts;
using Microsoft.Extensions.Caching.Memory;

namespace CompanyTaskManagement.Infrastructure.Repositories
{
    public class UnitOfWork<TId> : IUnitOfWork<TId>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMemoryCache? _cache;
        private Hashtable? _repositories;
        private bool _disposed;

        public UnitOfWork(ApplicationDbContext dbContext, IMemoryCache? cache = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cache = cache;
        }

        public IRepositoryAsync<T, TId> Repository<T>() where T : class, IEntity<TId>
        {
            _repositories ??= new Hashtable();
            var type = typeof(T).Name;

            if (!_repositories.ContainsKey(type))
            {
                var repositoryType = typeof(RepositoryAsync<,>);
                var repositoryInstance = Activator.CreateInstance(repositoryType.MakeGenericType(typeof(T), typeof(TId)), _dbContext);
                _repositories.Add(type, repositoryInstance);
            }

            return (IRepositoryAsync<T, TId>)_repositories[type]!;
        }

        public async Task<int> Commit(CancellationToken cancellationToken = default)
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<int> CommitAndRemoveCache(CancellationToken cancellationToken = default, params string[] cacheKeys)
        {
            var result = await _dbContext.SaveChangesAsync(cancellationToken);
            if (_cache != null && cacheKeys != null)
            {
                foreach (var cacheKey in cacheKeys)
                {
                    _cache.Remove(cacheKey);
                }
            }
            return result;
        }

        public Task Rollback()
        {
            _dbContext.ChangeTracker.Clear();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _dbContext.Dispose();
            }
            _disposed = true;
        }
    }
}
