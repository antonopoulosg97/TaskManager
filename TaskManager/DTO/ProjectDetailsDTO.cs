namespace TaskManager.DTO
{
    public record ProjectDetailsDTO : ProjectReadOnlyDTO
    {
        public List<TaskSummaryDTO> Tasks { get; set; } = new();
    }
}
