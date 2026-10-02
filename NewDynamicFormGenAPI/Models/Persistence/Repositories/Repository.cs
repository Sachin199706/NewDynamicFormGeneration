using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NewDynamicFormGenAPI.Models.Interfaces;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace FormGen.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Generic repository implementation providing data access operations for any entity type.
    /// </summary>
    /// <typeparam name="T">The entity type managed by this repository.</typeparam>
    /// <remarks>
    /// <para>
    /// Repository&lt;T&gt; implements IRepository&lt;T&gt; using Entity Framework Core DbSet operations.
    /// It provides both simple CRUD operations and advanced query composition through IQueryable,
    /// enabling flexible data access patterns while maintaining abstraction from EF Core internals.
    /// </para>
    /// </remarks>
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly FormGenDbContext _context;
        private readonly DbSet<T> _set;

        /// <summary>
        /// Initializes a new instance of the <see cref="Repository{T}"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public Repository(FormGenDbContext context)
        {
            _context = context;
            _set = context.Set<T>();
        }

        /// <inheritdoc/>
        public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);

        /// <inheritdoc/>
        public async Task<List<T>> GetAllAsync() => await _set.ToListAsync();

        /// <inheritdoc/>
        public async Task<List<T>> GetAllAsync(Expression<Func<T, bool>> predicate) => await _set.Where(predicate).ToListAsync();

        /// <inheritdoc/>
        public IQueryable<T> Query() => _set.AsQueryable();

        /// <inheritdoc/>
        public async Task AddAsync(T entity) => await _set.AddAsync(entity);

        /// <inheritdoc/>
        public void Update(T entity) => _set.Update(entity);

        /// <inheritdoc/>
        public void Remove(T entity) => _set.Remove(entity);
    }

    /// <summary>
    /// Implementation of the Unit of Work pattern coordinating multiple repositories and transaction management.
    /// </summary>
    /// <remarks>
    /// <para>
    /// UnitOfWork manages repository instances and ensures all changes are persisted in a single atomic transaction.
    /// It is scoped to the lifetime of a single business operation or HTTP request in dependency injection.
    /// </para>
    /// <para>
    /// Repository instances are cached per entity type to ensure consistent identity within a unit of work instance.
    /// </para>
    /// </remarks>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly FormGenDbContext _context;
        private readonly ConcurrentDictionary<Type, object> _repositories = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitOfWork"/> class.
        /// </summary>
        /// <param name="context">The database context.</param>
        public UnitOfWork(FormGenDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public IRepository<TEntity> Repository<TEntity>() where TEntity : class
        {
            return (IRepository<TEntity>)_repositories.GetOrAdd(
                typeof(TEntity), _ => new Repository<TEntity>(_context));
        }

        /// <inheritdoc/>
        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

        /// <summary>
        /// Begins a database transaction for coordinated multi-statement operations.
        /// </summary>
        /// <returns>A disposable transaction object that supports rollback and commit.</returns>
        /// <remarks>
        /// <para>
        /// This method enables explicit transaction management for scenarios requiring
        /// multiple database operations to succeed atomically. Dispose the returned transaction
        /// to commit it, or call Rollback() to discard changes.
        /// </para>
        /// <para>
        /// Standard usage via SaveChangesAsync() handles transaction implicitly; use this method
        /// only for explicit transaction control or nested transactions.
        /// </para>
        /// </remarks>
        public async Task<IDisposable> BeginTransactionAsync()
        {
            IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();
            return tx;
        }
    }
}
