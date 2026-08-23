using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using FormGen.Application.Interfaces;

namespace FormGen.Infrastructure.Persistence.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly FormGenDbContext _context;
        private readonly DbSet<T> _set;

        public Repository(FormGenDbContext context)
        {
            _context = context;
            _set = context.Set<T>();
        }

        public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);

        public async Task<List<T>> GetAllAsync() => await _set.ToListAsync();

        public IQueryable<T> Query() => _set.AsQueryable();

        public async Task AddAsync(T entity) => await _set.AddAsync(entity);

        public void Update(T entity) => _set.Update(entity);

        public void Remove(T entity) => _set.Remove(entity);
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly FormGenDbContext _context;
        private readonly ConcurrentDictionary<Type, object> _repositories = new();

        public UnitOfWork(FormGenDbContext context)
        {
            _context = context;
        }

        public IRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            return (IRepository<TEntity>)_repositories.GetOrAdd(
                typeof(TEntity), _ => new Repository<TEntity>(_context));
        }

        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

        public async Task<IDisposable> BeginTransactionAsync()
        {
            IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();
            return tx;
        }
    }
}
