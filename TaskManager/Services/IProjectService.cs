using TaskManager.Core;
using TaskManager.DTO;
using TaskManager.Enums;

namespace TaskManager.Services
{
    public interface IProjectService
    {
        // MANAGER or ADMIN. The creator is added to the members automatically.
        Task<ProjectReadOnlyDTO> CreateProjectAsync(Guid creatorUuid, CreateProjectDTO dto);

        // MANAGER or ADMIN (any manager). Soft-deletes the project's tasks too.
        Task DeleteProjectAsync(Guid uuid, Guid callerUuid);

        // MANAGER or ADMIN (any manager). dto.MembersUuids replaces the whole member list;
        // removed members are treated like LeaveProjectAsync (removed from tasks, their created tasks deleted).
        Task<ProjectReadOnlyDTO> UpdateProjectAsync(Guid uuid, Guid callerUuid, UpdateProjectDTO dto);

        // MANAGER or ADMIN, acts on the caller.
        Task<ProjectReadOnlyDTO> JoinProjectAsync(Guid projectUuid, Guid callerUuid);

        // MANAGER or ADMIN, acts on the caller. Removes the caller from the project's tasks
        // and deletes the tasks the caller created in this project.
        Task<ProjectReadOnlyDTO> LeaveProjectAsync(Guid projectUuid, Guid callerUuid);

        // Everyone.
        Task<ProjectDetailsDTO> GetProjectByUuidAsync(Guid uuid);
        Task<PaginatedResult<ProjectReadOnlyDTO>> GetAllProjectsAsync(int page, int pageSize, ProjectSortBy? sortBy);
    }
}
