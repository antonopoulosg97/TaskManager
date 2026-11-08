using TaskManager.Enums;

namespace TaskManager.Models
{
    public class Project : BaseEntity
    {
        public int Id { get; set; }
        
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public WorkStatus Status { get; set; } = WorkStatus.Todo;
        public DateOnly? DueDate { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;


        public ICollection<User> Users { get; set; } = new HashSet<User>();
        public ICollection<ProjectTask> ProjectTasks { get; set; } = new HashSet<ProjectTask>();

    }
}
