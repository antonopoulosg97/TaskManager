namespace TaskManager.Models
{
    public class Role
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        public ICollection<Capability> Capabilities { get; set; } = new HashSet<Capability>();
        public ICollection<User> Users { get; set; } = new HashSet<User>();
    }
}
