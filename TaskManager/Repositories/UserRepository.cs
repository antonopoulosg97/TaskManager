using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Enums;
using TaskManager.Models;

namespace TaskManager.Repositories
{
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(TaskManagerContext context) : base(context)
        {
        }

        public override async Task<User?> GetByUuidAsync(Guid uuid)
        {
            return await _dbSet
                .Include(u => u.Role)
                    .ThenInclude(r => r.Capabilities)
                .FirstOrDefaultAsync(u => u.Uuid == uuid);
        }

        public async Task<IEnumerable<User>> GetByProjectAsync(Guid projectUuid)
        {
            return await _dbSet
                .AsNoTracking()
                .Where(u => u.Projects.Any(p => p.Uuid == projectUuid))
                .ToListAsync();
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _dbSet
                .Include(u => u.Role)
                    .ThenInclude(r => r.Capabilities)
                .FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<IEnumerable<User>> GetByUuidsAsync(IEnumerable<Guid> uuids)
        {
            return await _dbSet
                .Where(u => uuids.Contains(u.Uuid))
                .ToListAsync();
        }

        public async Task<bool> UsernameExistsAsync(string username)
        {
            return await _dbSet.AnyAsync(u => u.Username == username);
        }

        public async Task<(IEnumerable<User> Items, int TotalCount)> GetPagedAsync(
    int page, int pageSize, UserSortBy? sortBy)
        {
            IQueryable<User> query = _dbSet.AsNoTracking().Include(u => u.Role);

            IOrderedQueryable<User> ordered = sortBy switch
            {
                UserSortBy.Role => query.OrderBy(u => u.RoleId),
                UserSortBy.CreatedAt => query.OrderByDescending(u => u.CreatedAt),
                _ => query.OrderBy(u => u.Lastname)        
            };

            return await PaginateAsync(ordered.ThenBy(u => u.Id), page, pageSize);
        }
    }
}
