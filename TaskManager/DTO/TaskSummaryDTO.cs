using TaskManager.Enums;

namespace TaskManager.DTO
{
    public record TaskSummaryDTO
    {
        public Guid Uuid { get; set; }
        public string Name { get; set; } = null!;
        public WorkStatus Status { get; set; }
        public ProjectTaskPriority Priority { get; set; }
        public DateOnly? DueDate { get; set; }
        public UserSummaryDTO CreatedBy { get; set; } = null!;
    }
}
