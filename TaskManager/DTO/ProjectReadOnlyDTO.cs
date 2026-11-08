using TaskManager.Enums;

namespace TaskManager.DTO
{
    public record ProjectReadOnlyDTO
    {
        public Guid Uuid { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public WorkStatus Status { get; set; }
        public DateOnly? DueDate { get; set; }
        public List<UserSummaryDTO> Members { get; set; } = new();
        public UserSummaryDTO CreatedBy { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public int TaskCount { get; set; }
    }
}
