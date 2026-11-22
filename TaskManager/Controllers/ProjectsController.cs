using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskManager.Core;
using TaskManager.DTO;
using TaskManager.Enums;
using TaskManager.Services;

namespace TaskManager.Controllers
{
    [ApiController]
    [Route("api/v1/projects")]
    public class ProjectsController : ControllerBase
    {
        private readonly IProjectService _projectService;


        public ProjectsController(IProjectService projectService)
        {
            _projectService = projectService;
           
        }

        /// <summary>
        /// Gets a paginated list of projects. Any authenticated user can view them.
        /// </summary>
        /// <param name="pageNumber">The page number (1-based). Default is 1.</param>
        /// <param name="pageSize">The number of projects per page (1–100). Default is 9.</param>
        /// <param name="sortBy">Optional sort field: Status, DueDate or CreatedAt.</param>
        /// <returns>A page of projects with pagination info.</returns>
        /// <response code="200">Returns the requested page of projects.</response>
        /// <response code="400">If a query parameter is invalid (e.g. an unknown sortBy value).</response>
        /// <response code="401">If the request is not authenticated.</response>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(PaginatedResult<ProjectReadOnlyDTO>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<PaginatedResult<ProjectReadOnlyDTO>>> GetAllProjects(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 9,
            [FromQuery] ProjectSortBy? sortBy = null
            )
        {
            var result = await _projectService.GetAllProjectsAsync(pageNumber, pageSize, sortBy);
            return Ok(result);
        }

        /// <summary>
        /// Gets a project's details by its UUID. Any authenticated user can view any project.
        /// </summary>
        /// <param name="uuid">The project's UUID.</param>
        /// <returns>The project's details.</returns>
        /// <response code="200">Returns the requested project.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="404">If no project exists with the given UUID.</response>
        [HttpGet("{uuid:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(ProjectDetailsDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProjectDetailsDTO>> GetProjectByUuid(Guid uuid)
        {
            var result = await _projectService.GetProjectByUuidAsync(uuid);
            return Ok(result);
        }

        /// <summary>
        /// Creates a project (requires CREATE_PROJECT). The creator is added as a member automatically.
        /// </summary>
        /// <param name="dto">The project's name, description, member UUIDs and due date.</param>
        /// <returns>The created project.</returns>
        /// <response code="201">Returns the created project.</response>
        /// <response code="400">If the request body is invalid or one of the member users does not exist.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the caller does not have the CREATE_PROJECT permission.</response>
        /// <response code="409">If a project with the same name already exists.</response>
        [HttpPost]
        [Authorize(Policy = "CREATE_PROJECT")]
        [ProducesResponseType(typeof(ProjectReadOnlyDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<ProjectReadOnlyDTO>> CreateProject(CreateProjectDTO dto)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var project = await _projectService.CreateProjectAsync(callerUuid, dto);
            return CreatedAtAction(nameof(GetProjectByUuid), new { uuid = project.Uuid }, project);
        }

        /// <summary>
        /// Updates a project (requires EDIT_PROJECT). Removed members also lose their tasks in the project: tasks they created are deleted, and they are removed from the other tasks.
        /// </summary>
        /// <param name="uuid">The project's UUID.</param>
        /// <param name="dto">The new name, description, due date, status and member UUIDs. The member list replaces the current one, and an omitted due date clears it.</param>
        /// <returns>The updated project.</returns>
        /// <response code="200">Returns the updated project.</response>
        /// <response code="400">If the request body is invalid or one of the member users does not exist.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the caller does not have the EDIT_PROJECT permission.</response>
        /// <response code="404">If no project exists with the given UUID.</response>
        [HttpPut("{uuid:guid}")]
        [Authorize(Policy = "EDIT_PROJECT")]
        [ProducesResponseType(typeof(ProjectReadOnlyDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProjectReadOnlyDTO>> UpdateProject(Guid uuid, UpdateProjectDTO dto)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var project = await _projectService.UpdateProjectAsync(uuid, callerUuid, dto);
            return Ok(project);
        }

        /// <summary>
        /// Deletes a project and all its tasks (soft delete, requires DELETE_PROJECT).
        /// </summary>
        /// <param name="uuid">The project's UUID.</param>
        /// <response code="204">If the project was deleted successfully.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the caller does not have the DELETE_PROJECT permission.</response>
        /// <response code="404">If no project exists with the given UUID.</response>
        [HttpDelete("{uuid:guid}")]
        [Authorize(Policy = "DELETE_PROJECT")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteProject(Guid uuid)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _projectService.DeleteProjectAsync(uuid, callerUuid);
            return NoContent();

        }

        /// <summary>
        /// Adds the current user to a project as a member (requires EDIT_PROJECT).
        /// </summary>
        /// <param name="uuid">The project's UUID.</param>
        /// <returns>The updated project.</returns>
        /// <response code="200">Returns the project the user joined.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the caller does not have the EDIT_PROJECT permission.</response>
        /// <response code="404">If no project exists with the given UUID.</response>
        /// <response code="409">If the user is already a member of the project.</response>
        [HttpPost("{uuid:guid}/join")]
        [Authorize(Policy = "EDIT_PROJECT")]
        [ProducesResponseType(typeof(ProjectReadOnlyDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProjectReadOnlyDTO>> JoinProject(Guid uuid)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _projectService.JoinProjectAsync(uuid, callerUuid);
            return Ok(result);
        }

        /// <summary>
        /// Removes the current user from a project (requires EDIT_PROJECT). Tasks the user created in the project are deleted, and the user is removed from the other tasks.
        /// </summary>
        /// <param name="uuid">The project's UUID.</param>
        /// <returns>The updated project.</returns>
        /// <response code="200">Returns the project the user left.</response>
        /// <response code="400">If the user is not a member of the project.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the caller does not have the EDIT_PROJECT permission.</response>
        /// <response code="404">If no project exists with the given UUID.</response>
        [HttpPost("{uuid:guid}/leave")]
        [Authorize(Policy = "EDIT_PROJECT")]
        [ProducesResponseType(typeof(ProjectReadOnlyDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProjectReadOnlyDTO>> LeaveProject(Guid uuid)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _projectService.LeaveProjectAsync(uuid, callerUuid);
            return Ok(result);
        }

    } 
}
