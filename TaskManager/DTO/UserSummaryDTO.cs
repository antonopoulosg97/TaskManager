namespace TaskManager.DTO
{
    public record UserSummaryDTO
    {
        public Guid Uuid { get; set; }
        public string Username { get; set; } = null!;
        public string Firstname { get; set; } = null!;
        public string Lastname { get; set; } = null!;
        public string? PhoneNumber { get; set; }
    }
}
