using TaskManager.DTO;

namespace TaskManager.Services
{
    public interface ITaskService
    {
        // MANAGER or ADMIN. The creator must be a member of the project and is added to the task members automatically.
        // Every uuid in dto.MembersUuids must be a member of the project.
        Task<TaskReadOnlyDTO> CreateTaskAsync(Guid creatorUuid, CreateTaskDTO dto);

        // Only the task's creator (callerUuid must match the creator, otherwise EntityForbiddenException).
        Task DeleteTaskAsync(Guid uuid, Guid callerUuid);

        // Only the task's creator. dto.MembersUuids replaces the whole member list (all must be project members).
        Task<TaskReadOnlyDTO> UpdateTaskAsync(Guid uuid, Guid callerUuid, UpdateTaskDTO dto);

        // Everyone.
        Task<TaskReadOnlyDTO> GetTaskByUuidAsync(Guid uuid);
    }
}
