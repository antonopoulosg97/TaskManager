using TaskManager.Models;
using Microsoft.EntityFrameworkCore;

namespace TaskManager.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAdminAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TaskManagerContext>();
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var adminRole = await db.Roles.SingleAsync(r => r.Name == "ADMIN");
            if (await db.Users.AnyAsync(u => u.RoleId == adminRole.Id)) return;

            db.Users.Add(new User
            {
                Username = config["Admin:Username"]!,
                Password = BCrypt.Net.BCrypt.HashPassword(config["Admin:Password"]!),
                Firstname = "System",
                Lastname = "Admin",
                RoleId = adminRole.Id
            });
            await db.SaveChangesAsync();
        }
    }
}
