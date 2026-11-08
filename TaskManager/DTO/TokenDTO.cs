namespace TaskManager.DTO
{
    public record TokenDTO
    {
        public string Token { get; set; } = null!;
        public DateTime ExpiresAt { get; set; }
    }
}
