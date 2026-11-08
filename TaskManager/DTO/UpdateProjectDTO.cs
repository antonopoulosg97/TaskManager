using System.ComponentModel.DataAnnotations;
using TaskManager.Enums;

namespace TaskManager.DTO
{
    public record UpdateProjectDTO
    {
        [Required(ErrorMessage = "The {0} field is required.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "The project name must be between 2 and 60 characters.")]
        public string? Name { get; set; }
        [StringLength(400, ErrorMessage = "The project description must be maximum 400 characters.")]
        public string? Description { get; set; }
        public DateOnly? DueDate { get; set; }
        // The full member list after the update (replaces the current one).
        public HashSet<Guid> MembersUuids { get; set; } = new();
        [Required(ErrorMessage = "The {0} field is required.")]
        [EnumDataType(typeof(WorkStatus), ErrorMessage = "Invalid status.")]
        public WorkStatus? Status { get; set; }
    }
}

