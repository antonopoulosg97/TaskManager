using Microsoft.EntityFrameworkCore;
using TaskManager.Models;
namespace TaskManager.Data
{
    public class TaskManagerContext : DbContext
    {
        public TaskManagerContext(DbContextOptions<TaskManagerContext> options) : base(options)
        { }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<ProjectTask> ProjectTasks { get; set; }
        public DbSet<Project> Projects { get; set; }
        public DbSet<Capability> Capabilities { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(e => e.Firstname).HasMaxLength(30);
                entity.Property(e => e.Lastname).HasMaxLength(40);
                entity.Property(e => e.Password).HasMaxLength(60);
                entity.Property(e => e.Username).HasMaxLength(50);
                entity.Property(e => e.PhoneNumber).HasMaxLength(15);

                entity.HasOne(u => u.Role)
                    .WithMany(u => u.Users)
                    .HasForeignKey(u => u.RoleId)
                    .OnDelete(DeleteBehavior.NoAction)
                    .HasConstraintName("FK_Users_Roles_RoleId");

                entity.HasIndex(e => e.Lastname, "IX_Users_Lastname");
                entity.HasIndex(e => e.Uuid, "UQ_Users_Uuid").IsUnique();
                entity.HasIndex(e => e.Username, "UQ_Users_Username")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                entity.HasQueryFilter(e => !e.IsDeleted);

            });

            modelBuilder.Entity<Role>(entity =>
            {
                entity.Property(e => e.Name).HasMaxLength(40);

                entity.HasMany(r => r.Capabilities)
                    .WithMany(r => r.Roles)
                    .UsingEntity("RolesCapabilities", j =>
                    {
                        j.HasIndex("CapabilitiesId").HasDatabaseName("IX_RolesCapabilities_CapabilityId");

                        j.HasData(
                       // ADMIN: everything (1-7)
                       new { RolesId = 1, CapabilitiesId = 1 }, 
                       new { RolesId = 1, CapabilitiesId = 2 },
                       new { RolesId = 1, CapabilitiesId = 3 }, 
                       new { RolesId = 1, CapabilitiesId = 4 },
                       new { RolesId = 1, CapabilitiesId = 5 }, 
                       new { RolesId = 1, CapabilitiesId = 6 },
                       new { RolesId = 1, CapabilitiesId = 7 },
                       // MANAGER: projects (no delete) + tasks
                       new { RolesId = 2, CapabilitiesId = 1 }, 
                       new { RolesId = 2, CapabilitiesId = 2 },
                       new { RolesId = 2, CapabilitiesId = 3 },
                       new { RolesId = 2, CapabilitiesId = 4 },
                       new { RolesId = 2, CapabilitiesId = 5 },
                       new { RolesId = 2, CapabilitiesId = 6 },
                       // EMPLOYEE: view projects + view tasks
                       new { RolesId = 3, CapabilitiesId = 1 }, 
                       new { RolesId = 3, CapabilitiesId = 6 }
                       );
                    });

                entity.HasIndex(e => e.Name, "UQ_Roles_Name").IsUnique();

                entity.HasData(
                   new Role { Id = 1, Name = "ADMIN", Description = "Full access" },
                   new Role { Id = 2, Name = "MANAGER", Description = "Manages projects and tasks" },
                   new Role { Id = 3, Name = "EMPLOYEE", Description = "Works on assigned tasks" }
                );
            });

            modelBuilder.Entity<Capability>(entity =>
            {
                entity.Property(e => e.Name).HasMaxLength(30);
                entity.Property(e => e.Description).HasMaxLength(255);

                entity.HasIndex(e => e.Name, "UQ_Capabilities_Name").IsUnique();

                entity.HasData(
                   new Capability { Id = 1, Name = "VIEW_PROJECTS", Description = "View projects" },
                   new Capability { Id = 2, Name = "CREATE_PROJECT", Description = "Create projects" },
                   new Capability { Id = 3, Name = "EDIT_PROJECT", Description = "Edit projects" },
                   new Capability { Id = 4, Name = "DELETE_PROJECT", Description = "Delete projects" },
                   new Capability { Id = 5, Name = "MANAGE_TASKS", Description = "Create, edit and delete tasks" },
                   new Capability { Id = 6, Name = "VIEW_TASKS", Description = "View tasks" },
                   new Capability { Id = 7, Name = "MANAGE_USERS", Description = "Manage users and roles" }
                );
            });

            modelBuilder.Entity<Project>(entity =>
            {
                entity.Property(e => e.Name).HasMaxLength(60);
                entity.Property(e => e.Description).HasMaxLength(400);

                entity.HasMany(p => p.Users)
                .WithMany(p => p.Projects)
                .UsingEntity("ProjectsUsers", j =>
                {
                    j.HasIndex("UsersId").HasDatabaseName("IX_ProjectsUsers_UserId");
                });
                entity.HasOne(p => p.User)
                .WithMany(p => p.CreatedProjects)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Projects_Users_UserId");
                entity.HasMany(p => p.ProjectTasks)
                .WithOne(p => p.Project)
                .HasForeignKey(p => p.ProjectId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProjectTasks_Projects_ProjectId");

                entity.HasIndex(e => e.Uuid, "UQ_Project_Uuid").IsUnique();
                entity.HasIndex(e => e.Name, "UQ_Project_Name")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<ProjectTask>(entity =>
            {
                entity.Property(e => e.Name).HasMaxLength(60);
                entity.Property(e => e.Description).HasMaxLength(400);

                entity.HasMany(t => t.Users)
                .WithMany(u => u.Tasks)
                .UsingEntity("ProjectTasksUsers", j =>
                {
                    j.HasIndex("UsersId").HasDatabaseName("IX_ProjectTasksUsers_UserId");
                });
                entity.HasOne(t => t.User)
                .WithMany(u => u.CreatedTasks)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ProjectTasks_Users_UserId");

                entity.HasIndex(e => e.Uuid, "UQ_ProjectTask_Uuid").IsUnique();
                entity.HasIndex(e => new { e.ProjectId, e.Name }, "UQ_ProjectTask_ProjectId_Name")
                    .IsUnique()
                    .HasFilter("\"IsDeleted\" = false");

                entity.HasQueryFilter(e => !e.IsDeleted);
            });


        }

        public override int SaveChanges()
        {
            ApplyAuditInfo();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyAuditInfo();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            ApplyAuditInfo();
            return base.SaveChangesAsync(ct);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
        {
            ApplyAuditInfo();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
        }

        private void ApplyAuditInfo()
        {
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = now;
                        entry.Entity.UpdatedAt = now;
                        break;

                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = now;
                        break;

                    case EntityState.Deleted:
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.DeletedAt = now;
                        entry.Entity.UpdatedAt = now;
                        break;
                }
            }
        }
    }
}
