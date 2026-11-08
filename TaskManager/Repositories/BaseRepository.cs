using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;


namespace TaskManager.Repositories
{
    public class BaseRepository<T> : IBaseRepository<T> where T : BaseEntity
    {

        protected readonly TaskManagerContext _context;
        protected readonly DbSet<T> _dbSet;

        public BaseRepository(TaskManagerContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public void Add(T entity)
        {
            _dbSet.Add(entity);    
        }

        public virtual async Task<T?> GetByUuidAsync(Guid uuid)
        {
            return await _dbSet.FirstOrDefaultAsync(e => e.Uuid == uuid);
        }

       


        public async Task<(IEnumerable<T> Items, int TotalCount)> PaginateAsync(
        IOrderedQueryable<T> query, int page, int pageSize)
        {
            int totalCount = await query.CountAsync();

            List<T> items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            return (items, totalCount);
        }



        public async Task<bool> SoftDeleteByUuidAsync(Guid uuid)
        {
            var entity = await _dbSet.FirstOrDefaultAsync(e => e.Uuid == uuid);
            if (entity is null) return false;
            entity.SoftDelete();
            return true; 
        }
        

        public void Update(T entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(entity);
        }
    }

  
}
