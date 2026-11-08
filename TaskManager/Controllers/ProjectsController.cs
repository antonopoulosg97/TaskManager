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

        [HttpPost]
        [Authorize(Policy = "CREATE_PROJECT")]
        [ProducesResponseType(typeof(ProjectReadOnlyDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProjectReadOnlyDTO>> CreateProject(CreateProjectDTO dto)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var project = await _projectService.CreateProjectAsync(callerUuid, dto);
            return CreatedAtAction(nameof(GetProjectByUuid), new { uuid = project.Uuid }, project);
        }


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
