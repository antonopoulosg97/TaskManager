using Microsoft.EntityFrameworkCore;
using TaskManager.Data;
using TaskManager.Models;

namespace TaskManager.Repositories
{
    public class ProjectTaskRepository : BaseRepository<ProjectTask>, IProjectTaskRepository
    {
        public ProjectTaskRepository(TaskManagerContext context) : base(context)
        {
        }

        public async Task<ProjectTask?> GetDetailsAsync(Guid uuid)
        {
            return await _dbSet
                .Include(t => t.User)
                .Include(t => t.Users)
                .Include(t => t.Project)
                .FirstOrDefaultAsync(t => t.Uuid == uuid);
        }

        public async Task<bool> NameExistsAsync(int projectId, string name)
        {
            return await _dbSet.AnyAsync(t => t.ProjectId == projectId && t.Name == name);
        }
    }
}
