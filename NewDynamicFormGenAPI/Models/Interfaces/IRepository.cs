namespace NewDynamicFormGenAPI.Models.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    IQueryable<T> Query();
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    IRepository<TEntity> Repository<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync();
    Task<IDisposable> BeginTransactionAsync();
}
