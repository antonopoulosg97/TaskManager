using TaskManager.Enums;
namespace TaskManager.Models
   
{
    public class ProjectTask: BaseEntity
    {
        public int Id { get; set; }
        
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public WorkStatus Status { get; set; } = WorkStatus.Todo;
        public ProjectTaskPriority Priority { get; set; } = ProjectTaskPriority.Medium;
        public DateOnly? DueDate { get; set; }




        public int ProjectId { get; set; }
        public Project Project { get; set; } = null!;

        public int UserId { get; set; } //CreatedById
        public User User { get; set; } = null!; //CreatedBy

        public ICollection<User> Users { get; set; } = new HashSet<User>();


    }
}
