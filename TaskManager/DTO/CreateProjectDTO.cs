using System.ComponentModel.DataAnnotations;

namespace TaskManager.DTO
{
    public record CreateProjectDTO
    {
        [Required(ErrorMessage = "The {0} field is required.")]
        [StringLength(60, MinimumLength = 2, ErrorMessage = "The project name must be between 2 and 60 characters.")]
        public string? Name { get; set; }
        [StringLength(400, ErrorMessage = "The project description must be maximum 400 characters.")]
        public string? Description { get; set; }
        public HashSet<Guid> MembersUuids { get; set; } = new();
        public DateOnly? DueDate { get; set; }
    }
}
