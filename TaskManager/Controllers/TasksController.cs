using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaskManager.DTO;
using TaskManager.Models;
using TaskManager.Services;

namespace TaskManager.Controllers
{
    [ApiController]
    [Route("api/v1/tasks")]
    public class TasksController : ControllerBase
    {

        private readonly ITaskService _taskService;

        public TasksController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        /// <summary>
        /// Gets a task by its UUID. Any authenticated user can view any task.
        /// </summary>
        /// <param name="uuid">The task's UUID.</param>
        /// <returns>The task's details.</returns>
        /// <response code="200">Returns the requested task.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="404">If no task exists with the given UUID.</response>
        [HttpGet("{uuid:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(TaskReadOnlyDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TaskReadOnlyDTO>> GetTaskByUuid(Guid uuid)
        {
            var task = await _taskService.GetTaskByUuidAsync(uuid);
            return Ok(task);
        }

        /// <summary>
        /// Creates a task in a project (requires MANAGE_TASKS). The creator is added as a member automatically, and priority defaults to Medium.
        /// </summary>
        /// <param name="dto">The task's project UUID, name, description, priority, member UUIDs and due date.</param>
        /// <returns>The created task.</returns>
        /// <response code="201">Returns the created task.</response>
        /// <response code="400">If the request body is invalid.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the caller does not have MANAGE_TASKS, is not a member of the project, or a listed member is not a project member.</response>
        /// <response code="404">If the project or one of the member users does not exist.</response>
        /// <response code="409">If a task with the same name already exists in the project.</response>
        [HttpPost]
        [Authorize(Policy = "MANAGE_TASKS")]
        [ProducesResponseType(typeof(TaskReadOnlyDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<TaskReadOnlyDTO>> CreateTask(CreateTaskDTO dto)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var task = await _taskService.CreateTaskAsync(callerUuid, dto);
            return CreatedAtAction(nameof(GetTaskByUuid), new { uuid = task.Uuid }, task);
        }

        /// <summary>
        /// Updates a task (requires MANAGE_TASKS). Only the task's creator or an admin can update it.
        /// </summary>
        /// <param name="uuid">The task's UUID.</param>
        /// <param name="dto">The new name, description, priority, status, due date and member UUIDs. The member list replaces the current one (the creator always stays a member), and an omitted due date clears it.</param>
        /// <returns>The updated task.</returns>
        /// <response code="200">Returns the updated task.</response>
        /// <response code="400">If the request body is invalid.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the caller does not have MANAGE_TASKS, is neither the task's creator nor an admin, or a listed member is not a project member.</response>
        /// <response code="404">If the task or one of the member users does not exist.</response>
        [HttpPut("{uuid:guid}")]
        [Authorize(Policy = "MANAGE_TASKS")]
        [ProducesResponseType(typeof(TaskReadOnlyDTO), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TaskReadOnlyDTO>> UpdateTask (Guid uuid,UpdateTaskDTO dto)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _taskService.UpdateTaskAsync(uuid, callerUuid, dto);
            return Ok(result);
        }

        /// <summary>
        /// Deletes a task (soft delete, requires MANAGE_TASKS). Only the task's creator or an admin can delete it.
        /// </summary>
        /// <param name="uuid">The task's UUID.</param>
        /// <response code="204">If the task was deleted successfully.</response>
        /// <response code="401">If the request is not authenticated.</response>
        /// <response code="403">If the caller does not have MANAGE_TASKS, or is neither the task's creator nor an admin.</response>
        /// <response code="404">If no task exists with the given UUID.</response>
        [HttpDelete("{uuid:guid}")]
        [Authorize(Policy = "MANAGE_TASKS")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> DeleteTask(Guid uuid)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _taskService.DeleteTaskAsync(uuid, callerUuid);
            return NoContent();
        }
    }
}
