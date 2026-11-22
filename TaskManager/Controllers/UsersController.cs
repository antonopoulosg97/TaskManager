using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskManager.Core;
using TaskManager.DTO;
using TaskManager.Enums;
using TaskManager.Exceptions;
using TaskManager.Services;

namespace TaskManager.Controllers
{
    [ApiController]
    [Route("api/v1/users")]
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }


        /// <summary>
        /// Gets a user by their UUID.
        /// </summary>
        /// <param name="uuid">The user's UUID.</param>
        /// <returns>The user's details.</returns>
        /// <response code="200">Returns the requested user.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the user lacks permission to view the user.</response>
        /// <response code="404">If no user exists with the given UUID.</response>
        [HttpGet("{uuid:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(UserReadOnlyDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserReadOnlyDTO>> GetUserByUuid(Guid uuid)
        {
            EnsureCanViewUser(uuid);
            var user = await _userService.GetUserByUuidAsync(uuid);
            return Ok(user);

        }

        /// <summary>
        /// Gets a paginated list of users.
        /// </summary>
        /// <param name="pageNumber">The page number (1-based). Default is 1.</param>
        /// <param name="pageSize">The number of users per page. Default is 10.</param>
        /// <param name="sortBy">Optional sort field: Lastname, Role or CreatedAt.</param>
        /// <returns>A page of users with pagination info.</returns>
        /// <response code="200">Returns the requested page of users.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the user does not have the MANAGE_USERS permission.</response>
        [HttpGet]
        [Authorize(Policy = "MANAGE_USERS")]
        [ProducesResponseType(typeof(PaginatedResult<UserReadOnlyDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<PaginatedResult<UserReadOnlyDTO>>> GetAllUsers(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] UserSortBy? sortBy = null)
        {
            var result = await _userService.GetAllAsync(pageNumber, pageSize, sortBy);
            return Ok(result);
        }

        /// <summary>
        /// Updates the current user's profile.
        /// </summary>
        /// <param name="dto">The new first name, last name and phone number. All three are replaced.</param>
        /// <returns>The updated user.</returns>
        /// <response code="200">Returns the updated user.</response>
        /// <response code="400">If the request body is invalid.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="404">If the user from the token no longer exists.</response>
        [HttpPut("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserReadOnlyDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserReadOnlyDTO>> UpdateProfile([FromBody] UpdateUserDTO dto)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _userService.UpdateAsync(callerUuid, dto);
            return Ok(result);
        }

        /// <summary>
        /// Changes the current user's password.
        /// </summary>
        /// <param name="changePassword">The current (old) password and the new password.</param>
        /// <response code="204">If the password was changed successfully.</response>
        /// <response code="400">If the body is invalid, the old password is incorrect, or the new password is the same as the old one.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="404">If the user from the token no longer exists.</response>
        [HttpPut("me/password")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> UpdatePassword(ChangePasswordDTO changePassword)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _userService.ChangePasswordAsync(callerUuid, changePassword);
            return NoContent();
        }

        /// <summary>
        /// Updates any user's profile and role (admin only).
        /// </summary>
        /// <param name="uuid">The UUID of the user to update.</param>
        /// <param name="dto">The new first name, last name, phone number and role id. All four are replaced.</param>
        /// <returns>The updated user.</returns>
        /// <response code="200">Returns the updated user.</response>
        /// <response code="400">If the body is invalid, the user </response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the caller does not have the MANAGE_USERS permission.</response>
        /// <response code="404">If no user exists with the given UUID.</response>
        [HttpPut("{uuid:guid}")]
        [Authorize(Policy = "MANAGE_USERS")]
        [ProducesResponseType(typeof(UserReadOnlyDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserReadOnlyDTO>> AdminUpdate(Guid uuid, [FromBody] AdminUpdateUserDTO dto)
        {
            var result = await _userService.AdminUpdateAsync(uuid, dto);
            return Ok(result);
        }


        /// <summary>
        /// Deletes a user (soft delete, admin only). The user's projects and tasks are transferred to the admin who deletes them.
        /// </summary>
        /// <param name="uuid">The UUID of the user to delete.</param>
        /// <response code="204">If the user was deleted successfully.</response>
        /// <response code="400">If the user is the last admin.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the caller does not have the MANAGE_USERS permission.</response>
        /// <response code="404">If no user exists with the given UUID.</response>
        [HttpDelete("{uuid:guid}")]
        [Authorize(Policy = "MANAGE_USERS")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteUser(Guid uuid)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _userService.DeleteAsync(uuid, callerUuid);
            return NoContent();
        }




        private void EnsureCanViewUser(Guid targetUuid)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            bool isOwnProfile = callerUuid == targetUuid;
            bool isAdmin = User.HasClaim("capability", "MANAGE_USERS");

            if (!isOwnProfile && !isAdmin)
                throw new EntityForbiddenException("User", "You can only view your own profile.");
        }
    }


}
