using TaskManager.Core;
using TaskManager.DTO;
using TaskManager.Enums;

namespace TaskManager.Services
{
    public interface IUserService
    {
        Task<UserReadOnlyDTO> SignUpAsync(SignUpDTO dto);
        Task<TokenDTO> SignInAsync(SignInDTO dto); 
        Task<UserReadOnlyDTO> GetUserByUuidAsync(Guid uuid);
        Task<PaginatedResult<UserReadOnlyDTO>> GetAllAsync(int page, int pageSize, UserSortBy? sortBy);
        Task<UserReadOnlyDTO> UpdateAsync(Guid uuid, UpdateUserDTO dto);
        Task<UserReadOnlyDTO> AdminUpdateAsync(Guid uuid, AdminUpdateUserDTO dto);
        Task ChangePasswordAsync(Guid uuid, ChangePasswordDTO dto);
        Task DeleteAsync(Guid uuid, Guid callerUuid);


    }
}
