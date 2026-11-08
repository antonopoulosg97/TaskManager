using AutoMapper;
using TaskManager.Core;
using TaskManager.Data;
using TaskManager.DTO;
using TaskManager.Enums;
using TaskManager.Exceptions;
using TaskManager.Models;
using TaskManager.Repositories;

namespace TaskManager.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _projectRepository;
        private readonly IUserRepository _userRepository;
        private readonly IMapper _mapper;
        private readonly ILogger<ProjectService> _logger;
        private readonly TaskManagerContext _taskManagerContext;


        public ProjectService(
            IProjectRepository projectRepository,
            IUserRepository userRepository,
            IMapper mapper,
            ILogger<ProjectService> logger,
            TaskManagerContext taskManagerContext)
        {
            _projectRepository = projectRepository;
            _userRepository = userRepository;
            _mapper = mapper;
            _logger = logger;
            _taskManagerContext = taskManagerContext;
        }

        public async Task<ProjectReadOnlyDTO> CreateProjectAsync(Guid creatorUuid, CreateProjectDTO dto)
        {
            User user = await _userRepository.GetByUuidAsync(creatorUuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {creatorUuid} not found.");

            if (!user.Role.Capabilities.Any(c => c.Name == "CREATE_PROJECT"))
                throw new EntityForbiddenException("Project", "You do not have permission to create projects.");
            if (await _projectRepository.NameExistsAsync(dto.Name!))
                throw new EntityAlreadyExistsException("Project", $"A project named {dto.Name} already exists.");

            List<User> members = (await _userRepository.GetByUuidsAsync(dto.MembersUuids)).ToList();
            if (members.Count != dto.MembersUuids.Count)
                throw new InvalidArgumentException("Members", "One or more members do not exist.");

            if (!members.Any(m => m.Id == user.Id))
                members.Add(user);

            Project project = new()
            {
                Name = dto.Name!,
                Description = dto.Description,
                DueDate = dto.DueDate,
                User = user,
                Users = members
            };
            _projectRepository.Add(project);
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("Project {Uuid} created by user {UserUuid}", project.Uuid, creatorUuid);
            return _mapper.Map<ProjectReadOnlyDTO>(project);

        }

        public async Task DeleteProjectAsync(Guid uuid, Guid callerUuid)
        {
            User caller = await _userRepository.GetByUuidAsync(callerUuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {callerUuid} not found.");

            if (!caller.Role.Capabilities.Any(c => c.Name == "DELETE_PROJECT"))
                throw new EntityForbiddenException("Project", "You do not have permission to delete projects.");

            Project project = await _projectRepository.GetDetailsAsync(uuid)
                ?? throw new EntityNotFoundException("Project", $"Project with uuid {uuid} not found.");

            foreach (ProjectTask task in project.ProjectTasks)
            {
                task.SoftDelete();
            }

            project.SoftDelete();
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("Project {Uuid} deleted", uuid);
        }

        public async Task<PaginatedResult<ProjectReadOnlyDTO>> GetAllProjectsAsync(int page, int pageSize, ProjectSortBy? sortBy)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var (projects, totalCount) = await _projectRepository.GetPagedAsync(page, pageSize, sortBy);

            List<ProjectReadOnlyDTO> data = _mapper.Map<List<ProjectReadOnlyDTO>>(projects);
            return new PaginatedResult<ProjectReadOnlyDTO>(data, totalCount, page, pageSize);
        }

        public async Task<ProjectDetailsDTO> GetProjectByUuidAsync(Guid uuid)
        {
            Project project = await _projectRepository.GetDetailsAsync(uuid)
                ?? throw new EntityNotFoundException("Project", $"Project with uuid {uuid} not found.");
            return _mapper.Map<ProjectDetailsDTO>(project);
        }

        public async Task<ProjectReadOnlyDTO> JoinProjectAsync(Guid projectUuid, Guid callerUuid)
        {
            User user = await _userRepository.GetByUuidAsync(callerUuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {callerUuid} not found.");
            if (!user.Role.Capabilities.Any(c => c.Name == "EDIT_PROJECT"))
                throw new EntityForbiddenException("Project", "Only managers can join projects.");

            Project project = await _projectRepository.GetDetailsAsync(projectUuid)
                ?? throw new EntityNotFoundException("Project", $"Project with uuid {projectUuid} not found.");

            if (project.Users.Any(u => u.Id == user.Id))
                throw new EntityAlreadyExistsException("Member", "You are already a member of this project.");

            project.Users.Add(user);
            _projectRepository.Update(project);
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("User {UserUuid} joined project {Uuid}", callerUuid, projectUuid);
            return _mapper.Map<ProjectReadOnlyDTO>(project);
        }

        public async Task<ProjectReadOnlyDTO> LeaveProjectAsync(Guid projectUuid, Guid callerUuid)
        {
            User user = await _userRepository.GetByUuidAsync(callerUuid)
                 ?? throw new EntityNotFoundException("User", $"User with uuid {callerUuid} not found.");

            if (!user.Role.Capabilities.Any(c => c.Name == "EDIT_PROJECT"))
                throw new EntityForbiddenException("Project", "Only managers can leave projects.");

            Project project = await _projectRepository.GetDetailsAsync(projectUuid)
                 ?? throw new EntityNotFoundException("Project", $"Project with uuid {projectUuid} not found.");

            if (!project.Users.Any(u => u.Id == user.Id))
                throw new InvalidArgumentException("Member", "You are not a member of this project.");

            RemoveMember(project, user);
            _projectRepository.Update(project);
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("User {UserUuid} left project {Uuid}", callerUuid, projectUuid);
            return _mapper.Map<ProjectReadOnlyDTO>(project);
        }

        public async Task<ProjectReadOnlyDTO> UpdateProjectAsync(Guid uuid, Guid callerUuid, UpdateProjectDTO dto)
        {

            User caller = await _userRepository.GetByUuidAsync(callerUuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {callerUuid} not found.");

            if (!caller.Role.Capabilities.Any(c => c.Name == "EDIT_PROJECT"))
                throw new EntityForbiddenException("Project", "You do not have permission to edit projects.");
            Project project = await _projectRepository.GetDetailsAsync(uuid)
                ?? throw new EntityNotFoundException("Project", $"Project with uuid {uuid} not found.");

            project.Name = dto.Name!;
            project.Description = dto.Description;
            project.DueDate = dto.DueDate;
            project.Status = dto.Status!.Value;

            List<User> newMembers = (await _userRepository.GetByUuidsAsync(dto.MembersUuids)).ToList();
            if (newMembers.Count != dto.MembersUuids.Count)
                throw new InvalidArgumentException("Members", "One or more members do not exist.");
            List<User> removed = project.Users
            .Where(u => !dto.MembersUuids.Contains(u.Uuid))
            .ToList();
            
            foreach (User user in removed)
                RemoveMember(project, user);
            foreach (User user in newMembers.Where(u => !project.Users.Any(m => m.Id == u.Id)))
                project.Users.Add(user);

            _projectRepository.Update(project);
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("Project {Uuid} updated", uuid);

            return _mapper.Map<ProjectReadOnlyDTO>(project);

        }

        private static void RemoveMember(Project project, User user)
        {
            project.Users.Remove(user);

            foreach (ProjectTask task in project.ProjectTasks)
            {
                if (task.UserId == user.Id)
                    task.SoftDelete();
                else
                    task.Users.Remove(user);
            }
        }
    }
}
