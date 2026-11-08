

namespace TaskManager.DTO
{
    public record UserReadOnlyDTO
    {
        public Guid Uuid { get; set; }
        public string Username { get; set; } = null!;
        public string Firstname { get; set; } = null!;
        public string Lastname { get; set; } = null!;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
