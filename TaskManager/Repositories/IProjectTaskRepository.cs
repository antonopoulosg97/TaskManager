using TaskManager.Models;

namespace TaskManager.Repositories
{
    public interface IProjectTaskRepository : IBaseRepository<ProjectTask>
    {
        Task<ProjectTask?> GetDetailsAsync(Guid uuid);
        Task<bool> NameExistsAsync(int projectId, string name);
    }
}
