using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Enums;
using TaskManager.Models;

namespace TaskManager.Repositories
{
    public class ProjectRepository : BaseRepository<Project>, IProjectRepository
    {
        public ProjectRepository(TaskManagerContext context) : base(context)
        {
        }

        public async Task<Project?> GetDetailsAsync(Guid uuid)
        {
            return await _dbSet
                .Include(p => p.User)
                .Include(p => p.Users)
                .Include(p => p.ProjectTasks)
                    .ThenInclude(t => t.Users)
                .Include(p => p.ProjectTasks)
                    .ThenInclude(t => t.User)
                .AsSplitQuery()
                .FirstOrDefaultAsync(p => p.Uuid == uuid);
        }

        public async Task<(IEnumerable<Project> Items, int TotalCount)> GetPagedAsync(
            int page, int pageSize, ProjectSortBy? sortBy)
        {
            IQueryable<Project> query = _dbSet
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.Users)
                .Include(p => p.ProjectTasks)
                .AsSplitQuery();

            IOrderedQueryable<Project> ordered = sortBy switch
            {
                ProjectSortBy.DueDate => query.OrderBy(p => p.DueDate),
                ProjectSortBy.CreatedAt => query.OrderByDescending(p => p.CreatedAt),
                _ => query.OrderBy(p => p.Status).ThenBy(p => p.DueDate)    // default
            };

            return await PaginateAsync(ordered.ThenBy(p => p.Id), page, pageSize);
        }

        public async Task<bool> NameExistsAsync(string name)
        {
            return await _dbSet.AnyAsync(p => p.Name == name);
        }
    }
}
