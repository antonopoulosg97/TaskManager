using AutoMapper;
using System.Reflection.Metadata.Ecma335;
using TaskManager.Data;
using TaskManager.DTO;
using TaskManager.Enums;
using TaskManager.Exceptions;
using TaskManager.Models;
using TaskManager.Repositories;

namespace TaskManager.Services
{
    public class TaskService : ITaskService
    {
        private readonly IProjectTaskRepository _projectTaskRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<TaskService> _logger;
        private readonly TaskManagerContext _taskManagerContext;

        public TaskService(
            IProjectTaskRepository projectTaskRepository,
            IUserRepository userRepository,
            IMapper mapper,
            ILogger<TaskService> logger,
            TaskManagerContext taskManagerContext,
            IProjectRepository projectRepository)
        {
            _projectTaskRepository = projectTaskRepository;
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _mapper = mapper;
            _logger = logger;
            _taskManagerContext = taskManagerContext;
        }
        public async Task<TaskReadOnlyDTO> CreateTaskAsync(Guid creatorUuid, CreateTaskDTO dto)
        {
            User user = await _userRepository.GetByUuidAsync(creatorUuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {creatorUuid} not found.");

            if (!user.Role.Capabilities.Any(c => c.Name == "MANAGE_TASKS"))
                throw new EntityForbiddenException("Task", "You do not have permission to create tasks.");

            Project project = await _projectRepository.GetDetailsAsync(dto.ProjectUuid!.Value)
                ?? throw new EntityNotFoundException("Project", $"Project with uuid {dto.ProjectUuid} not found.");

            HashSet<int> projectMemberIds = project.Users.Select(u => u.Id).ToHashSet();

            if (!projectMemberIds.Contains(user.Id))
                throw new EntityForbiddenException("Task", "Only project members can create tasks in this project.");
            if (await _projectTaskRepository.NameExistsAsync(project.Id, dto.Name!))
                throw new EntityAlreadyExistsException("Task", $"A task named {dto.Name} already exists in this project.");

            List<User> members = (await _userRepository.GetByUuidsAsync(dto.MembersUuids)).ToList();

            if (members.Count != dto.MembersUuids.Count)
                throw new EntityNotFoundException("User", $"One or more users with the specified UUIDs not found.");

            if (members.Any(m => !projectMemberIds.Contains(m.Id)))
                throw new EntityForbiddenException("Task", "All task members must be members of the project.");

            if (!members.Any(m => m.Id == user.Id))
                members.Add(user); 

                ProjectTask task = new()
                {
                    Name = dto.Name!,
                    Description = dto.Description,
                    Priority = dto.Priority ?? ProjectTaskPriority.Medium,
                    DueDate = dto.DueDate,
                    Project = project,
                    User = user,
                    Users = members
                };
            _projectTaskRepository.Add(task);
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("Task {Uuid} created by user {UserUuid}", task.Uuid, creatorUuid);
            return _mapper.Map<TaskReadOnlyDTO>(task);

        }

            

        public async Task DeleteTaskAsync(Guid uuid, Guid callerUuid)
        {
            User caller = await _userRepository.GetByUuidAsync(callerUuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {callerUuid} not found.");

            if (!caller.Role.Capabilities.Any(c => c.Name == "MANAGE_TASKS"))
                throw new EntityForbiddenException("Task", "You do not have permission to delete tasks.");

            ProjectTask task = await _projectTaskRepository.GetDetailsAsync(uuid)
                ?? throw new EntityNotFoundException("Task", $"Task with uuid {uuid} not found.");

            bool isAdmin = caller.Role.Capabilities.Any(c => c.Name == "MANAGE_USERS");

            if (task.User.Uuid != callerUuid && !isAdmin)
                throw new EntityForbiddenException("Task", "Only the creator of the task can delete it.");


            task.SoftDelete();
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("Task {Uuid} deleted by user {UserUuid}", uuid, callerUuid);

        }

        public async Task<TaskReadOnlyDTO> GetTaskByUuidAsync(Guid uuid)
        {
            ProjectTask task = await _projectTaskRepository.GetDetailsAsync(uuid)
                ?? throw new EntityNotFoundException("Task", $"Task with uuid {uuid} not found.");
            return _mapper.Map<TaskReadOnlyDTO>(task);
        }

        public async Task<TaskReadOnlyDTO> UpdateTaskAsync(Guid uuid, Guid callerUuid, UpdateTaskDTO dto)
        {
            User caller = await _userRepository.GetByUuidAsync(callerUuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {callerUuid} not found.");

            if (!caller.Role.Capabilities.Any(c => c.Name == "MANAGE_TASKS"))
                throw new EntityForbiddenException("Task", "You do not have permission to update tasks.");

            ProjectTask task = await _projectTaskRepository.GetDetailsAsync(uuid)
                ?? throw new EntityNotFoundException("Task", $"Task with uuid {uuid} not found.");

            bool isAdmin = caller.Role.Capabilities.Any(c => c.Name == "MANAGE_USERS");

            if (task.User.Uuid != callerUuid && !isAdmin) 
                throw new EntityForbiddenException("Task", "Only the creator of the task can update it.");
            HashSet<int> projectMemberIds = (await _userRepository.GetByProjectAsync(task.Project.Uuid))
               .Select(u => u.Id)
               .ToHashSet();
            List<User> members = (await _userRepository.GetByUuidsAsync(dto.MembersUuids)).ToList();

            if (members.Count != dto.MembersUuids.Count)

                throw new EntityNotFoundException("User", "One or more users with the specified UUIDs not found.");
            if (members.Any(m => !projectMemberIds.Contains(m.Id)))
                throw new EntityForbiddenException("Task", "All task members must be members of the project.");

            if (!members.Any(m => m.Id == task.UserId))
                members.Add(task.User);   // creator always stays a member


            task.Name = dto.Name ?? task.Name;
            task.Description = dto.Description ?? task.Description;
            task.Priority = dto.Priority ?? task.Priority;
            task.Status = dto.Status ?? task.Status;
            task.DueDate = dto.DueDate;
            task.Users = members;
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("Task {Uuid} updated by user {UserUuid}", uuid, callerUuid);
            return _mapper.Map<TaskReadOnlyDTO>(task);
        }

    }
}
