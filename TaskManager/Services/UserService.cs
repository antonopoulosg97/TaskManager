using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TaskManager.Core;
using TaskManager.Data;
using TaskManager.DTO;
using TaskManager.Enums;
using TaskManager.Exceptions;
using TaskManager.Models;
using TaskManager.Repositories;
using TaskManager.Security;

namespace TaskManager.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly TaskManagerContext _taskManagerContext;
        private readonly ILogger<UserService> _logger;
        private readonly IEncryptionUtil _encryptionUtil;
        private readonly ITokenService _tokenService;
        private readonly IMapper _mapper;

        public UserService(
        IUserRepository userRepository,
        TaskManagerContext taskManagerContext,
        ILogger<UserService> logger,
        IEncryptionUtil encryptionUtil,
        ITokenService tokenService,
        IMapper mapper)
        {
            _userRepository = userRepository;
            _taskManagerContext = taskManagerContext;
            _logger = logger;
            _encryptionUtil = encryptionUtil;
            _tokenService = tokenService;
            _mapper = mapper;
        }

        public async Task<UserReadOnlyDTO> AdminUpdateAsync(Guid uuid, AdminUpdateUserDTO dto)
        {
            User user = await _userRepository.GetByUuidAsync(uuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {uuid} not found.");

            Role newRole = await _taskManagerContext.Roles.FirstOrDefaultAsync(r => r.Id == dto.RoleId)
                ?? throw new InvalidArgumentException("Role", $"Role with id {dto.RoleId} does not exist.");

            // The last admin must not lose the ADMIN role, otherwise nobody can manage the system
            int adminRoleId = await _taskManagerContext.Roles
                .Where(r => r.Name == "ADMIN")
                .Select(r => r.Id)
                .SingleAsync();

            if (user.RoleId == adminRoleId && newRole.Id != adminRoleId)
            {
                bool otherAdminExists = await _taskManagerContext.Users
                    .AnyAsync(u => u.RoleId == adminRoleId && u.Uuid != uuid);

                if (!otherAdminExists)
                    throw new InvalidArgumentException("Role", "Cannot remove the ADMIN role from the last admin.");
            }

            user.Firstname = dto.Firstname!;
            user.Lastname = dto.Lastname!;
            user.PhoneNumber = dto.PhoneNumber;
            user.Role = newRole;   // also sets RoleId, and gives the mapper Role.Name

            _userRepository.Update(user);
            await _taskManagerContext.SaveChangesAsync();

            _logger.LogInformation("User {Uuid} updated by admin", uuid);

            return _mapper.Map<UserReadOnlyDTO>(user);
        }

        public async Task ChangePasswordAsync(Guid uuid, ChangePasswordDTO dto)
        {
            User user = await _userRepository.GetByUuidAsync(uuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {uuid} not found.");
            if (!_encryptionUtil.IsValidPassword(dto.OldPassword!, user.Password))
                throw new InvalidArgumentException("Password", "Old password is incorrect.");
            if (_encryptionUtil.IsValidPassword(dto.NewPassword!, user.Password))
                throw new InvalidArgumentException("Password", "New password must be different from the old one.");

            user.Password = _encryptionUtil.Encrypt(dto.NewPassword!);
                    _userRepository.Update(user);
            
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("User {Uuid} changed password", uuid);
        }

        public async Task DeleteAsync(Guid uuid, Guid callerUuid)
        {
            User user = await _userRepository.GetByUuidAsync(uuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {uuid} not found.");
            int adminRoleId = await _taskManagerContext.Roles
                .Where(r => r.Name == "ADMIN")
                .Select(r => r.Id)
                .SingleAsync();

            if (user.RoleId == adminRoleId)
            {
                bool otherAdminExists = await _taskManagerContext.Users
                    .AnyAsync(u => u.RoleId == adminRoleId && u.Uuid != uuid);

                if (!otherAdminExists)
                    throw new InvalidArgumentException("Role", "Cannot remove the ADMIN role from the last admin.");
            }

            // The deleted user's projects and tasks pass to the admin who deletes him.
            // If an admin deletes himself, they pass to another admin (guaranteed by the check above).
            User newOwner = callerUuid != uuid
                ? await _userRepository.GetByUuidAsync(callerUuid)
                    ?? throw new EntityNotFoundException("User", $"User with uuid {callerUuid} not found.")
                : await _taskManagerContext.Users.FirstAsync(u => u.RoleId == adminRoleId && u.Uuid != uuid);

            List<Project> projects = await _taskManagerContext.Projects
                .Include(p => p.Users)
                .Where(p => p.UserId == user.Id)
                .ToListAsync();

            foreach (Project project in projects)
            {
                project.User = newOwner;
                if (!project.Users.Any(u => u.Id == newOwner.Id))
                    project.Users.Add(newOwner);
            }

            List<ProjectTask> tasks = await _taskManagerContext.ProjectTasks
                .Include(t => t.Users)
                .Include(t => t.Project)
                    .ThenInclude(p => p.Users)
                .Where(t => t.UserId == user.Id)
                .ToListAsync();

            foreach (ProjectTask task in tasks)
            {
                task.User = newOwner;
                if (!task.Users.Any(u => u.Id == newOwner.Id))
                    task.Users.Add(newOwner);
                if (!task.Project.Users.Any(u => u.Id == newOwner.Id))
                    task.Project.Users.Add(newOwner);   // task members must be project members
            }

            await _userRepository.SoftDeleteByUuidAsync(uuid);

            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("User {Uuid} deleted; projects and tasks passed to {NewOwnerUuid}", uuid, newOwner.Uuid);
        }

        public async Task<PaginatedResult<UserReadOnlyDTO>> GetAllAsync(int page, int pageSize, UserSortBy? sortBy)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var (users, totalCount) = await _userRepository.GetPagedAsync(page, pageSize, sortBy);


            List<UserReadOnlyDTO> data = _mapper.Map<List<UserReadOnlyDTO>>(users);

            return new PaginatedResult<UserReadOnlyDTO>(data, totalCount, page, pageSize);
        }

        public async Task<UserReadOnlyDTO> GetUserByUuidAsync(Guid uuid)
        {
            User user = await _userRepository.GetByUuidAsync(uuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {uuid} not found.");
            _logger.LogInformation("User {Uuid} retrieved", uuid);
            return _mapper.Map<UserReadOnlyDTO>(user);
        }

        public async Task<TokenDTO> SignInAsync(SignInDTO dto)
        {
            User? user = await _userRepository.GetByUsernameAsync(dto.Username!);

            if (user is null || !_encryptionUtil.IsValidPassword(dto.Password!, user.Password))
            {
                _logger.LogWarning("Failed sign-in attempt for username {Username}", dto.Username);
                throw new EntityNotAuthorizedException("User", "Invalid username or password.");
            }
            _logger.LogInformation("User {Uuid} signed in", user.Uuid);
            return _tokenService.GenerateToken(user);
        }

        public async Task<UserReadOnlyDTO> SignUpAsync(SignUpDTO dto)
        {

            if (await _userRepository.UsernameExistsAsync(dto.Username!))
                throw new EntityAlreadyExistsException("User", $"Username {dto.Username} already exists.");

            Role employeeRole = await _taskManagerContext.Roles
                .SingleAsync(r => r.Name == "EMPLOYEE");
            User user = new()
            {
                Username = dto.Username!,
                Password = _encryptionUtil.Encrypt(dto.Password!),
                Firstname = dto.Firstname!,
                Lastname = dto.Lastname!,
                PhoneNumber = dto.PhoneNumber,
                Role = employeeRole
            };

            _userRepository.Add(user);
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("User {Uuid} signed up", user.Uuid);
            return _mapper.Map<UserReadOnlyDTO>(user);   
        }

        public async Task<UserReadOnlyDTO> UpdateAsync(Guid uuid, UpdateUserDTO dto)
        {
            User user = await _userRepository.GetByUuidAsync(uuid)
                ?? throw new EntityNotFoundException("User", $"User with uuid {uuid} not found.");
            user.Firstname = dto.Firstname!;
            user.Lastname = dto.Lastname!;
            user.PhoneNumber = dto.PhoneNumber;
            _userRepository.Update(user);
            await _taskManagerContext.SaveChangesAsync();
            _logger.LogInformation("User {Uuid} updated", uuid);
            return _mapper.Map<UserReadOnlyDTO>(user);

        }
    }
}
