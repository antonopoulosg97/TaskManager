using TaskManager.Enums;
using TaskManager.Models;

namespace TaskManager.Repositories
{
    public interface IProjectRepository : IBaseRepository<Project>
    {
        Task<Project?> GetDetailsAsync(Guid uuid);        // includes creator, members and tasks
        Task<(IEnumerable<Project> Items, int TotalCount)> GetPagedAsync(
            int page, int pageSize, ProjectSortBy? sortBy);
        Task<bool> NameExistsAsync(string name);
    }
}
