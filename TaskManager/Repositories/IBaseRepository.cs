using TaskManager.Models;

namespace TaskManager.Repositories
{
    public interface IBaseRepository<T>
    {
        Task<T?> GetByUuidAsync(Guid uuid);
        void Add(T entity);
        void Update(T entity);                    // sets UpdatedAt
        Task<bool> SoftDeleteByUuidAsync(Guid uuid); // sets IsDeleted + DeletedAt, false if not found
        Task<(IEnumerable<T> Items, int TotalCount)> PaginateAsync(
        IOrderedQueryable<T> query, int page, int pageSize);
    }
}