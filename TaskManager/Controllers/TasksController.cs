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

        [HttpPost]
        [Authorize(Policy = "MANAGE_TASKS")]
        [ProducesResponseType(typeof(TaskReadOnlyDTO), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]

        public async Task<ActionResult<TaskReadOnlyDTO>> CreateTask(CreateTaskDTO dto)
        {
            Guid callerUuid = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var task = await _taskService.CreateTaskAsync(callerUuid, dto);
            return CreatedAtAction(nameof(GetTaskByUuid), new { uuid = task.Uuid }, task);
        }


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
