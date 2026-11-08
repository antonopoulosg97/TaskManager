using TaskManager.Enums;
using TaskManager.Models;

namespace TaskManager.Repositories
{
    public interface IUserRepository : IBaseRepository<User>
    {
        Task<User?> GetByUsernameAsync(string username);              // includes Role + Capabilities, for login
        Task<bool> UsernameExistsAsync(string username);
        Task<IEnumerable<User>> GetByUuidsAsync(IEnumerable<Guid> uuids);
        Task<IEnumerable<User>> GetByProjectAsync(Guid projectUuid);  // project members
        Task<(IEnumerable<User> Items, int TotalCount)> GetPagedAsync(
            int page, int pageSize, UserSortBy? sortBy);
    }
}
