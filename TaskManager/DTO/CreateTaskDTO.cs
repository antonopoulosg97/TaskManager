

using System.ComponentModel.DataAnnotations;
using TaskManager.Enums;

namespace TaskManager.DTO
{
    public record CreateTaskDTO
    {

        [Required(ErrorMessage = "The {0} field is required.")]
        public Guid? ProjectUuid { get; set; }
        [Required(ErrorMessage = "The {0} field is required.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "The task name must be between 2 and 60 characters.")]
        public string? Name { get; set; }
        [StringLength(400, ErrorMessage = "The task description must be maximum 400 characters.")]
        public string? Description { get; set; }
        [EnumDataType(typeof(ProjectTaskPriority), ErrorMessage = "Invalid priority.")]
        public ProjectTaskPriority? Priority { get; set; }
        public HashSet<Guid> MembersUuids { get; set; } = new();
        public DateOnly? DueDate { get; set; }
    }
}
