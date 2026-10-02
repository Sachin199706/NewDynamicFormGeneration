using System.Linq.Expressions;

namespace NewDynamicFormGenAPI.Models.Interfaces;

/// <summary>
/// Defines generic data access operations for a specific entity type.
/// </summary>
/// <typeparam name="T">The entity type managed by this repository.</typeparam>
/// <remarks>
/// <para>
/// IRepository&lt;T&gt; provides a data access abstraction layer, enabling loose coupling between
/// business logic and persistence implementation. Implementations typically use Entity Framework Core
/// or other ORM technologies to execute queries against the database.
/// </para>
/// <para>
/// Repository methods support both synchronous and asynchronous patterns. Asynchronous methods
/// avoid blocking threads and are preferred for scalable web applications.
/// </para>
/// </remarks>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Retrieves a single entity by its primary key identifier.
    /// </summary>
    /// <param name="id">The primary key value to search for.</param>
    /// <returns>The entity if found; otherwise <c>null</c>.</returns>
    /// <remarks>
    /// This is typically used to load an entity for viewing or editing by ID.
    /// </remarks>
    Task<T?> GetByIdAsync(int id);

    /// <summary>
    /// Retrieves all entities of the specified type from the database.
    /// </summary>
    /// <returns>A list containing all entities of type T.</returns>
    /// <remarks>
    /// Use caution with large datasets as this method loads all entities into memory.
    /// Consider using pagination or query filters for large tables.
    /// </remarks>
    Task<List<T>> GetAllAsync();

    /// <summary>
    /// Retrieves all entities matching a specified LINQ predicate filter.
    /// </summary>
    /// <param name="predicate">A lambda expression specifying the filter condition (e.g., x => x.IsActive == true).</param>
    /// <returns>A list of entities matching the filter condition.</returns>
    /// <remarks>
    /// This method enables simple filtering scenarios. For complex queries, use <see cref="Query()"/> instead.
    /// </remarks>
    Task<List<T>> GetAllAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Returns an IQueryable for building complex LINQ queries with filtering, ordering, and projections.
    /// </summary>
    /// <returns>An IQueryable provider for advanced query composition.</returns>
    /// <remarks>
    /// <para>
    /// This method returns a query that is not yet executed against the database. Developers can
    /// compose additional LINQ operations (Where, OrderBy, Select, Include, etc.) before
    /// calling a terminal operation like ToListAsync() or FirstOrDefaultAsync().
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// var activeUsers = await repository.Query()
    ///     .Where(x => x.IsActive)
    ///     .OrderBy(x => x.Name)
    ///     .ToListAsync();
    /// </code>
    /// </para>
    /// </remarks>
    IQueryable<T> Query();

    /// <summary>
    /// Adds a new entity to the data store (marks it for insertion).
    /// </summary>
    /// <param name="entity">The entity to insert.</param>
    /// <returns>A completed task indicating the entity has been staged for insertion.</returns>
    /// <remarks>
    /// <para>
    /// This method does not immediately persist the entity to the database.
    /// The entity is marked for insertion and persisted when <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </para>
    /// <para>
    /// This deferred commit pattern allows multiple operations to be collected and executed
    /// in a single database transaction for consistency and performance.
    /// </para>
    /// </remarks>
    Task AddAsync(T entity);

    /// <summary>
    /// Updates an existing entity in the data store (marks it for update).
    /// </summary>
    /// <param name="entity">The entity with changed values to update.</param>
    /// <remarks>
    /// <para>
    /// This method does not immediately persist changes to the database.
    /// The entity is marked for update and persisted when <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </para>
    /// <para>
    /// The entity must already exist in the database; this method is for modifying existing records.
    /// </para>
    /// </remarks>
    void Update(T entity);

    /// <summary>
    /// Deletes an entity from the data store (marks it for deletion).
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    /// <remarks>
    /// <para>
    /// This method does not immediately remove the entity from the database.
    /// The entity is marked for deletion and removed when <see cref="IUnitOfWork.SaveChangesAsync"/> is called.
    /// </para>
    /// <para>
    /// The entity must already exist in the database and be loaded into the current session.
    /// </para>
    /// </remarks>
    void Remove(T entity);
}

/// <summary>
/// Defines the contract for coordinating multiple repositories and managing database transactions.
/// </summary>
/// <remarks>
/// <para>
/// IUnitOfWork implements the Unit of Work pattern, providing:
/// <list type="bullet">
///   <item><description>Access to repositories for different entity types</description></item>
///   <item><description>Transaction management for ACID compliance</description></item>
///   <item><description>Coordinated persistence of multiple entity changes</description></item>
/// </list>
/// </para>
/// <para>
/// This pattern enables business logic to work with multiple repositories while maintaining
/// data consistency through explicit transaction boundaries and atomic save operations.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Gets or creates a repository for the specified entity type.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to manage.</typeparam>
    /// <returns>A repository instance for the specified entity type.</returns>
    /// <remarks>
    /// Implementations typically cache repository instances per entity type to ensure
    /// consistent identity within a UnitOfWork instance.
    /// </remarks>
    IRepository<TEntity> Repository<TEntity>() where TEntity : class;

    /// <summary>
    /// Persists all pending changes (inserts, updates, deletes) to the database.
    /// </summary>
    /// <returns>The number of entities inserted, updated, or deleted.</returns>
    /// <remarks>
    /// <para>
    /// This method commits all changes staged by Add/Update/Remove operations across all repositories
    /// in a single atomic transaction. If any error occurs, all changes are rolled back.
    /// </para>
    /// <para>
    /// This is typically called after business logic has completed successfully,
    /// ensuring the "commit everything at once" principle for transaction consistency.
    /// </para>
    /// </remarks>
    Task<int> SaveChangesAsync();

    /// <summary>
    /// Begins an explicit database transaction for coordinating multiple operations.
    /// </summary>
    /// <returns>An IDisposable transaction handle. Dispose to commit or call Rollback() before dispose to cancel.</returns>
    /// <remarks>
    /// <para>
    /// This method enables explicit transaction management when implicit transactions are insufficient.
    /// Use this for scenarios requiring:
    /// <list type="bullet">
    ///   <item><description>Nested or conditional transaction logic</description></item>
    ///   <item><description>Explicit rollback on specific conditions</description></item>
    ///   <item><description>Long-running operations with multiple database roundtrips</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Example usage:
    /// <code>
    /// using (var transaction = await unitOfWork.BeginTransactionAsync())
    /// {
    ///     // Perform database operations
    ///     // SaveChangesAsync() implicitly commits within the transaction
    ///     // Dispose of transaction to finalize
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    Task<IDisposable> BeginTransactionAsync();
}
