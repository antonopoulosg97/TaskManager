

namespace TaskManager.Models
{
    public class User : BaseEntity
    {
        public int Id { get; set; }
        
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Firstname { get; set; } = null!;
        public string Lastname { get; set; } = null!;

        public string? PhoneNumber { get; set; }

        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
        public ICollection<ProjectTask> Tasks { get; set; } = new HashSet<ProjectTask>();
        public ICollection<Project> Projects { get; set; } = new HashSet<Project>();
        public ICollection<ProjectTask> CreatedTasks { get; set; } = new HashSet<ProjectTask>();
        public ICollection<Project> CreatedProjects { get; set; } = new HashSet<Project>();

    }
}
