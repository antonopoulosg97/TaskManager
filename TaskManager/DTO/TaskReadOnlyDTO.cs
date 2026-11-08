using TaskManager.Enums;
namespace TaskManager.DTO
{
    public record TaskReadOnlyDTO
    {
        public Guid Uuid { get; set; }
        public Guid ProjectUuid { get; set; } 
        public string Name { get; set; } = null!;
        public string? Description { get; set; } 
        public ProjectTaskPriority Priority { get; set; }
        public WorkStatus Status { get; set; }
        public DateOnly? DueDate { get; set; }
        public List<UserSummaryDTO> Members { get; set; } = new();
        public UserSummaryDTO CreatedBy { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public string ProjectName { get; set; } = null!;

    }
}
