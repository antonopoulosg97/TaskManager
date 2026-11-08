using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TaskManager.Core;
using TaskManager.Data;
using TaskManager.DTO;
using TaskManager.Enums;
using TaskManager.Services;
using Xunit;

namespace TaskManager.Tests
{
    // Runs the real API (routes, auth policies, validation, services, repositories, exception handler)
    // inside the test process. The only replacement is the database: a throwaway in-memory SQLite one.
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        public const string AdminUsername = "testadmin";
        public const string AdminPassword = "Admin#12345";

        // Set by run-tests.ps1 -Postgres. Without it the tests use in-memory SQLite.
        private static readonly string? PostgresServer = Environment.GetEnvironmentVariable("TASKMANAGER_TEST_POSTGRES");

        private readonly SqliteConnection? _sqlite;
        private readonly string? _postgres;

        public ApiFactory()
        {
            if (PostgresServer is null)
            {
                _sqlite = new SqliteConnection("DataSource=:memory:");
                _sqlite.Open();
                using var context = CreateContext();
                context.Database.EnsureCreated();
            }
            else
            {
                // Every test gets its own database, built with the project's real migrations.
                _postgres = new NpgsqlConnectionStringBuilder(PostgresServer) { Database = $"tm_test_{Guid.NewGuid():N}" }.ConnectionString;
                using var context = CreateContext();
                context.Database.Migrate();
            }
        }

        private void UseDatabase(DbContextOptionsBuilder options)
        {
            if (_postgres is not null)
                options.UseNpgsql(_postgres);
            else
                options.UseSqlite(_sqlite!);
        }

        private TaskManagerContext CreateContext()
        {
            var builder = new DbContextOptionsBuilder<TaskManagerContext>();
            UseDatabase(builder);
            return new TaskManagerContext(builder.Options);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=unused");
            builder.UseSetting("Jwt:Key", "test-only-signing-key-test-only-signing-key-test-only-signing-key");
            builder.UseSetting("Jwt:Issuer", "test-issuer");
            builder.UseSetting("Jwt:Audience", "test-audience");
            builder.UseSetting("Jwt:ExpiresInMinutes", "60");
            builder.UseSetting("Admin:Username", AdminUsername);
            builder.UseSetting("Admin:Password", AdminPassword);

            builder.ConfigureTestServices(services =>
            {
                var postgres = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<TaskManagerContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(IDbContextOptionsConfiguration<TaskManagerContext>)).ToList();
                foreach (var descriptor in postgres)
                    services.Remove(descriptor);

                services.AddDbContext<TaskManagerContext>(UseDatabase);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_postgres is not null)
            {
                using var context = CreateContext();
                context.Database.EnsureDeleted();
            }
            _sqlite?.Dispose();
        }
    }

    public sealed record Actor(Guid Uuid, string Username, string Token);

    public abstract class ApiTestBase : IDisposable
    {
        protected const string Password = "Passw0rd!x";
        protected const int ManagerRoleId = 2;

        protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        protected readonly ApiFactory Factory = new();
        protected readonly HttpClient Client;
        private Actor? _admin;

        protected ApiTestBase()
        {
            Client = Factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        }

        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
            GC.SuppressFinalize(this);
        }

        protected async Task<HttpResponseMessage> Send(HttpMethod method, string url, Actor? actor = null, object? body = null, HttpClient? client = null)
        {
            using var request = new HttpRequestMessage(method, url);
            if (actor is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", actor.Token);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType(), options: Json);
            return await (client ?? Client).SendAsync(request);
        }

        protected static async Task Expect(HttpStatusCode expected, HttpResponseMessage response)
        {
            if (response.StatusCode != expected)
            {
                string content = await response.Content.ReadAsStringAsync();
                Assert.Fail($"Expected {(int)expected} {expected} but got {(int)response.StatusCode} {response.StatusCode}. Body: {content}");
            }
        }

        protected static async Task<T> Read<T>(HttpResponseMessage response)
            => (await response.Content.ReadFromJsonAsync<T>(Json))!;

        protected async Task<T> Get<T>(string url, Actor actor)
        {
            var response = await Send(HttpMethod.Get, url, actor);
            await Expect(HttpStatusCode.OK, response);
            return await Read<T>(response);
        }

        protected async Task<Actor> Login(string username, string password = Password)
        {
            var response = await Send(HttpMethod.Post, "api/v1/auth/login", body: new SignInDTO { Username = username, Password = password });
            await Expect(HttpStatusCode.OK, response);
            string token = (await Read<TokenDTO>(response)).Token;
            string payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using JsonDocument claims = JsonDocument.Parse(Convert.FromBase64String(payload));
            string uuid = claims.RootElement.EnumerateObject()
                .First(c => c.Name.EndsWith("nameidentifier") || c.Name == "nameid" || c.Name == "sub").Value.GetString()!;
            return new Actor(Guid.Parse(uuid), username, token);
        }

        protected async Task<Actor> Admin() => _admin ??= await Login(ApiFactory.AdminUsername, ApiFactory.AdminPassword);

        protected async Task<UserReadOnlyDTO> Register(string username)
        {
            var response = await Send(HttpMethod.Post, "api/v1/auth/register/user", body: new SignUpDTO
            {
                Username = username,
                Password = Password,
                Firstname = "First",
                Lastname = "Last"
            });
            await Expect(HttpStatusCode.Created, response);
            return await Read<UserReadOnlyDTO>(response);
        }

        protected async Task<Actor> Employee(string username)
        {
            await Register(username);
            return await Login(username);
        }

        protected async Task<Actor> Manager(string username)
        {
            UserReadOnlyDTO user = await Register(username);
            var response = await Send(HttpMethod.Put, $"api/v1/users/{user.Uuid}", await Admin(), new AdminUpdateUserDTO
            {
                Firstname = "First",
                Lastname = "Last",
                RoleId = ManagerRoleId
            });
            await Expect(HttpStatusCode.OK, response);
            return await Login(username);
        }

        protected async Task<ProjectReadOnlyDTO> CreateProject(Actor actor, string name, params Actor[] members)
        {
            var response = await Send(HttpMethod.Post, "api/v1/projects", actor, new CreateProjectDTO
            {
                Name = name,
                MembersUuids = members.Select(m => m.Uuid).ToHashSet()
            });
            await Expect(HttpStatusCode.Created, response);
            return await Read<ProjectReadOnlyDTO>(response);
        }

        protected async Task<TaskReadOnlyDTO> CreateTask(Actor actor, Guid projectUuid, string name, params Actor[] members)
        {
            var response = await Send(HttpMethod.Post, "api/v1/tasks", actor, new CreateTaskDTO
            {
                ProjectUuid = projectUuid,
                Name = name,
                MembersUuids = members.Select(m => m.Uuid).ToHashSet()
            });
            await Expect(HttpStatusCode.Created, response);
            return await Read<TaskReadOnlyDTO>(response);
        }

        protected static UpdateTaskDTO TaskUpdate(string name = "Updated", params Actor[] members) => new()
        {
            Name = name,
            Description = "New description",
            Priority = ProjectTaskPriority.High,
            Status = WorkStatus.InProgress,
            DueDate = new DateOnly(2030, 1, 15),
            MembersUuids = members.Select(m => m.Uuid).ToHashSet()
        };

        protected static UpdateProjectDTO ProjectUpdate(string name, WorkStatus status, params Actor[] members) => new()
        {
            Name = name,
            Description = "New description",
            DueDate = new DateOnly(2030, 6, 1),
            Status = status,
            MembersUuids = members.Select(m => m.Uuid).ToHashSet()
        };
    }

    public class AuthTests : ApiTestBase
    {
        [Fact]
        public async Task Register_creates_an_employee()
        {
            UserReadOnlyDTO user = await Register("nikos");
            Assert.Equal("nikos", user.Username);
            Assert.Equal("EMPLOYEE", user.Role);
        }

        [Fact]
        public async Task Register_with_taken_username_returns_409()
        {
            await Register("nikos");
            var response = await Send(HttpMethod.Post, "api/v1/auth/register/user", body: new SignUpDTO
            {
                Username = "nikos", Password = Password, Firstname = "First", Lastname = "Last"
            });
            await Expect(HttpStatusCode.Conflict, response);
        }

        [Fact]
        public async Task Register_with_weak_password_returns_400()
        {
            var response = await Send(HttpMethod.Post, "api/v1/auth/register/user", body: new SignUpDTO
            {
                Username = "nikos", Password = "weakpassword", Firstname = "First", Lastname = "Last"
            });
            await Expect(HttpStatusCode.BadRequest, response);
        }

        [Fact]
        public async Task Seeded_admin_can_log_in()
        {
            Actor admin = await Admin();
            Assert.False(string.IsNullOrEmpty(admin.Token));
        }

        [Fact]
        public async Task Login_with_wrong_password_returns_401()
        {
            await Register("nikos");
            var response = await Send(HttpMethod.Post, "api/v1/auth/login", body: new SignInDTO { Username = "nikos", Password = "Wrong#12345" });
            await Expect(HttpStatusCode.Unauthorized, response);
        }

        [Fact]
        public async Task Login_with_unknown_user_returns_401()
        {
            var response = await Send(HttpMethod.Post, "api/v1/auth/login", body: new SignInDTO { Username = "nobody", Password = Password });
            await Expect(HttpStatusCode.Unauthorized, response);
        }

        [Fact]
        public async Task Deleted_user_cannot_log_in()
        {
            UserReadOnlyDTO user = await Register("nikos");
            await Expect(HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"api/v1/users/{user.Uuid}", await Admin()));

            var response = await Send(HttpMethod.Post, "api/v1/auth/login", body: new SignInDTO { Username = "nikos", Password = Password });
            await Expect(HttpStatusCode.Unauthorized, response);
        }
    }

    public class UserTests : ApiTestBase
    {
        [Fact]
        public async Task Request_without_token_returns_401()
        {
            await Expect(HttpStatusCode.Unauthorized, await Send(HttpMethod.Get, $"api/v1/users/{Guid.NewGuid()}"));
        }

        [Fact]
        public async Task User_can_view_own_profile()
        {
            Actor nikos = await Employee("nikos");
            UserReadOnlyDTO user = await Get<UserReadOnlyDTO>($"api/v1/users/{nikos.Uuid}", nikos);
            Assert.Equal("nikos", user.Username);
        }

        [Fact]
        public async Task User_cannot_view_another_profile()
        {
            Actor nikos = await Employee("nikos");
            Actor anna = await Employee("anna");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Get, $"api/v1/users/{anna.Uuid}", nikos));
        }

        [Fact]
        public async Task Admin_can_view_any_profile()
        {
            Actor nikos = await Employee("nikos");
            UserReadOnlyDTO user = await Get<UserReadOnlyDTO>($"api/v1/users/{nikos.Uuid}", await Admin());
            Assert.Equal("nikos", user.Username);
        }

        [Fact]
        public async Task Only_admin_can_list_users()
        {
            Actor nikos = await Employee("nikos");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Get, "api/v1/users", nikos));

            var result = await Get<PaginatedResult<UserReadOnlyDTO>>("api/v1/users", await Admin());
            Assert.Equal(2, result.TotalRecords);
        }

        [Fact]
        public async Task User_can_update_own_profile()
        {
            Actor nikos = await Employee("nikos");
            var response = await Send(HttpMethod.Put, "api/v1/users/me", nikos, new UpdateUserDTO { Firstname = "Changed", Lastname = "Name" });
            await Expect(HttpStatusCode.OK, response);

            UserReadOnlyDTO user = await Get<UserReadOnlyDTO>($"api/v1/users/{nikos.Uuid}", nikos);
            Assert.Equal("Changed", user.Firstname);
        }

        [Fact]
        public async Task Password_change_takes_effect()
        {
            Actor nikos = await Employee("nikos");
            const string newPassword = "N3wPassw0rd!";
            var response = await Send(HttpMethod.Put, "api/v1/users/me/password", nikos, new ChangePasswordDTO { OldPassword = Password, NewPassword = newPassword });
            await Expect(HttpStatusCode.NoContent, response);

            await Login("nikos", newPassword);
            var oldLogin = await Send(HttpMethod.Post, "api/v1/auth/login", body: new SignInDTO { Username = "nikos", Password = Password });
            await Expect(HttpStatusCode.Unauthorized, oldLogin);
        }

        [Fact]
        public async Task Password_change_with_wrong_old_password_returns_400()
        {
            Actor nikos = await Employee("nikos");
            var response = await Send(HttpMethod.Put, "api/v1/users/me/password", nikos, new ChangePasswordDTO { OldPassword = "Wrong#12345", NewPassword = "N3wPassw0rd!" });
            await Expect(HttpStatusCode.BadRequest, response);
        }

        [Fact]
        public async Task Admin_can_change_a_role()
        {
            UserReadOnlyDTO user = await Register("nikos");
            var response = await Send(HttpMethod.Put, $"api/v1/users/{user.Uuid}", await Admin(), new AdminUpdateUserDTO { Firstname = "First", Lastname = "Last", RoleId = ManagerRoleId });
            await Expect(HttpStatusCode.OK, response);
            Assert.Equal("MANAGER", (await Read<UserReadOnlyDTO>(response)).Role);
        }

        [Fact]
        public async Task Last_admin_cannot_lose_the_admin_role()
        {
            Actor admin = await Admin();
            var response = await Send(HttpMethod.Put, $"api/v1/users/{admin.Uuid}", admin, new AdminUpdateUserDTO { Firstname = "System", Lastname = "Admin", RoleId = ManagerRoleId });
            await Expect(HttpStatusCode.BadRequest, response);
        }

        [Fact]
        public async Task Last_admin_cannot_be_deleted()
        {
            Actor admin = await Admin();
            await Expect(HttpStatusCode.BadRequest, await Send(HttpMethod.Delete, $"api/v1/users/{admin.Uuid}", admin));
        }

        [Fact]
        public async Task Non_admin_cannot_delete_a_user()
        {
            Actor manager = await Manager("maria");
            Actor nikos = await Employee("nikos");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Delete, $"api/v1/users/{nikos.Uuid}", manager));
        }

        [Fact]
        public async Task Deleting_a_user_passes_their_projects_and_tasks_to_the_admin()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design");
            Actor admin = await Admin();

            await Expect(HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"api/v1/users/{manager.Uuid}", admin));

            ProjectDetailsDTO details = await Get<ProjectDetailsDTO>($"api/v1/projects/{project.Uuid}", admin);
            Assert.Equal(admin.Uuid, details.CreatedBy.Uuid);
            Assert.Contains(details.Members, m => m.Uuid == admin.Uuid);
            Assert.DoesNotContain(details.Members, m => m.Uuid == manager.Uuid);

            TaskReadOnlyDTO transferred = await Get<TaskReadOnlyDTO>($"api/v1/tasks/{task.Uuid}", admin);
            Assert.Equal(admin.Uuid, transferred.CreatedBy.Uuid);
            Assert.Contains(transferred.Members, m => m.Uuid == admin.Uuid);
        }
    }

    public class ProjectTests : ApiTestBase
    {
        [Fact]
        public async Task List_without_token_returns_401()
        {
            await Expect(HttpStatusCode.Unauthorized, await Send(HttpMethod.Get, "api/v1/projects"));
        }

        [Fact]
        public async Task Employee_cannot_create_a_project()
        {
            Actor nikos = await Employee("nikos");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Post, "api/v1/projects", nikos, new CreateProjectDTO { Name = "Website" }));
        }

        [Fact]
        public async Task Manager_creates_a_project_and_becomes_a_member()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");

            Assert.Equal(manager.Uuid, project.CreatedBy.Uuid);
            Assert.Contains(project.Members, m => m.Uuid == manager.Uuid);
            Assert.Equal(WorkStatus.Todo, project.Status);
        }

        [Fact]
        public async Task Create_with_unknown_member_returns_400()
        {
            Actor manager = await Manager("maria");
            var response = await Send(HttpMethod.Post, "api/v1/projects", manager, new CreateProjectDTO { Name = "Website", MembersUuids = [Guid.NewGuid()] });
            await Expect(HttpStatusCode.BadRequest, response);
        }

        [Fact]
        public async Task Create_with_duplicate_name_returns_409()
        {
            Actor manager = await Manager("maria");
            await CreateProject(manager, "Website");
            var response = await Send(HttpMethod.Post, "api/v1/projects", manager, new CreateProjectDTO { Name = "Website" });
            await Expect(HttpStatusCode.Conflict, response);
        }

        [Fact]
        public async Task Everyone_sees_all_projects_with_task_counts()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            await CreateTask(manager, project.Uuid, "Design");
            await CreateProject(manager, "Mobile");
            Actor nikos = await Employee("nikos");

            var result = await Get<PaginatedResult<ProjectReadOnlyDTO>>("api/v1/projects", nikos);

            Assert.Equal(2, result.TotalRecords);
            Assert.Equal(1, result.Data.Single(p => p.Uuid == project.Uuid).TaskCount);
        }

        [Fact]
        public async Task Project_details_contain_members_and_tasks()
        {
            Actor manager = await Manager("maria");
            Actor nikos = await Employee("nikos");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website", nikos);
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design");

            ProjectDetailsDTO details = await Get<ProjectDetailsDTO>($"api/v1/projects/{project.Uuid}", nikos);

            Assert.Equal(2, details.Members.Count);
            Assert.Equal(task.Uuid, Assert.Single(details.Tasks).Uuid);
        }

        [Fact]
        public async Task Unknown_project_returns_404()
        {
            Actor nikos = await Employee("nikos");
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Get, $"api/v1/projects/{Guid.NewGuid()}", nikos));
        }

        [Fact]
        public async Task Update_saves_fields_and_replaces_members()
        {
            Actor manager = await Manager("maria");
            Actor nikos = await Employee("nikos");
            Actor anna = await Employee("anna");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website", nikos);

            var response = await Send(HttpMethod.Put, $"api/v1/projects/{project.Uuid}", manager, ProjectUpdate("Renamed", WorkStatus.Done, manager, anna));
            await Expect(HttpStatusCode.OK, response);

            ProjectDetailsDTO details = await Get<ProjectDetailsDTO>($"api/v1/projects/{project.Uuid}", manager);
            Assert.Equal("Renamed", details.Name);
            Assert.Equal(WorkStatus.Done, details.Status);
            Assert.Contains(details.Members, m => m.Uuid == anna.Uuid);
            Assert.DoesNotContain(details.Members, m => m.Uuid == nikos.Uuid);
        }

        [Fact]
        public async Task Removing_a_member_removes_them_from_tasks_and_deletes_tasks_they_created()
        {
            Actor owner = await Manager("maria");
            Actor other = await Manager("kostas");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website", other);
            TaskReadOnlyDTO ownersTask = await CreateTask(owner, project.Uuid, "Keep", other);
            TaskReadOnlyDTO othersTask = await CreateTask(other, project.Uuid, "Remove");

            await Expect(HttpStatusCode.OK, await Send(HttpMethod.Put, $"api/v1/projects/{project.Uuid}", owner, ProjectUpdate("Website", WorkStatus.Todo, owner)));

            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Get, $"api/v1/tasks/{othersTask.Uuid}", owner));
            TaskReadOnlyDTO kept = await Get<TaskReadOnlyDTO>($"api/v1/tasks/{ownersTask.Uuid}", owner);
            Assert.DoesNotContain(kept.Members, m => m.Uuid == other.Uuid);
        }

        [Fact]
        public async Task Employee_cannot_update_a_project()
        {
            Actor manager = await Manager("maria");
            Actor nikos = await Employee("nikos");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Put, $"api/v1/projects/{project.Uuid}", nikos, ProjectUpdate("X1", WorkStatus.Done, manager)));
        }

        [Fact]
        public async Task Manager_can_join_once()
        {
            Actor owner = await Manager("maria");
            Actor other = await Manager("kostas");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website");

            var joined = await Send(HttpMethod.Post, $"api/v1/projects/{project.Uuid}/join", other);
            await Expect(HttpStatusCode.OK, joined);
            Assert.Contains((await Read<ProjectReadOnlyDTO>(joined)).Members, m => m.Uuid == other.Uuid);

            await Expect(HttpStatusCode.Conflict, await Send(HttpMethod.Post, $"api/v1/projects/{project.Uuid}/join", other));
        }

        [Fact]
        public async Task Employee_cannot_join()
        {
            Actor owner = await Manager("maria");
            Actor nikos = await Employee("nikos");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Post, $"api/v1/projects/{project.Uuid}/join", nikos));
        }

        [Fact]
        public async Task Leaving_when_not_a_member_returns_400()
        {
            Actor owner = await Manager("maria");
            Actor other = await Manager("kostas");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website");
            await Expect(HttpStatusCode.BadRequest, await Send(HttpMethod.Post, $"api/v1/projects/{project.Uuid}/leave", other));
        }

        [Fact]
        public async Task Leaving_removes_user_from_tasks_and_deletes_tasks_they_created()
        {
            Actor owner = await Manager("maria");
            Actor other = await Manager("kostas");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website", other);
            TaskReadOnlyDTO ownersTask = await CreateTask(owner, project.Uuid, "Keep", other);
            TaskReadOnlyDTO othersTask = await CreateTask(other, project.Uuid, "Remove");

            await Expect(HttpStatusCode.OK, await Send(HttpMethod.Post, $"api/v1/projects/{project.Uuid}/leave", other));

            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Get, $"api/v1/tasks/{othersTask.Uuid}", owner));
            TaskReadOnlyDTO kept = await Get<TaskReadOnlyDTO>($"api/v1/tasks/{ownersTask.Uuid}", owner);
            Assert.DoesNotContain(kept.Members, m => m.Uuid == other.Uuid);
            ProjectDetailsDTO details = await Get<ProjectDetailsDTO>($"api/v1/projects/{project.Uuid}", owner);
            Assert.DoesNotContain(details.Members, m => m.Uuid == other.Uuid);
        }

        [Fact]
        public async Task Creator_may_leave_and_stays_creator()
        {
            Actor owner = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website");

            await Expect(HttpStatusCode.OK, await Send(HttpMethod.Post, $"api/v1/projects/{project.Uuid}/leave", owner));

            ProjectDetailsDTO details = await Get<ProjectDetailsDTO>($"api/v1/projects/{project.Uuid}", owner);
            Assert.Equal(owner.Uuid, details.CreatedBy.Uuid);
            Assert.Empty(details.Members);
        }

        [Fact]
        public async Task Deleting_a_project_deletes_its_tasks()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design");

            await Expect(HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"api/v1/projects/{project.Uuid}", manager));

            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Get, $"api/v1/projects/{project.Uuid}", manager));
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Get, $"api/v1/tasks/{task.Uuid}", manager));
        }

        [Fact]
        public async Task Deleted_project_name_can_be_reused()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            await Expect(HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"api/v1/projects/{project.Uuid}", manager));
            await CreateProject(manager, "Website");
        }

        [Fact]
        public async Task Employee_cannot_delete_a_project()
        {
            Actor manager = await Manager("maria");
            Actor nikos = await Employee("nikos");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Delete, $"api/v1/projects/{project.Uuid}", nikos));
        }
    }

    public class TaskTests : ApiTestBase
    {
        [Fact]
        public async Task Get_without_token_returns_401()
        {
            await Expect(HttpStatusCode.Unauthorized, await Send(HttpMethod.Get, $"api/v1/tasks/{Guid.NewGuid()}"));
        }

        [Fact]
        public async Task Non_member_employee_can_view_a_task()
        {
            Actor manager = await Manager("maria");
            Actor nikos = await Employee("nikos");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design");

            TaskReadOnlyDTO seen = await Get<TaskReadOnlyDTO>($"api/v1/tasks/{task.Uuid}", nikos);
            Assert.Equal("Design", seen.Name);
            Assert.Equal(project.Uuid, seen.ProjectUuid);
            Assert.Equal("Website", seen.ProjectName);
        }

        [Fact]
        public async Task Unknown_task_returns_404()
        {
            Actor nikos = await Employee("nikos");
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Get, $"api/v1/tasks/{Guid.NewGuid()}", nikos));
        }

        [Fact]
        public async Task Removed_get_all_route_is_gone()
        {
            Actor nikos = await Employee("nikos");
            await Expect(HttpStatusCode.MethodNotAllowed, await Send(HttpMethod.Get, "api/v1/tasks", nikos));
        }

        [Fact]
        public async Task Member_creates_a_task_with_defaults_and_location()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");

            var response = await Send(HttpMethod.Post, "api/v1/tasks", manager, new CreateTaskDTO { ProjectUuid = project.Uuid, Name = "Design" });
            await Expect(HttpStatusCode.Created, response);

            TaskReadOnlyDTO task = await Read<TaskReadOnlyDTO>(response);
            Assert.EndsWith($"/api/v1/tasks/{task.Uuid}", response.Headers.Location!.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal(ProjectTaskPriority.Medium, task.Priority);
            Assert.Equal(WorkStatus.Todo, task.Status);
            Assert.Equal(manager.Uuid, task.CreatedBy.Uuid);
            Assert.Equal(manager.Uuid, Assert.Single(task.Members).Uuid);
        }

        [Fact]
        public async Task Employee_cannot_create_a_task()
        {
            Actor manager = await Manager("maria");
            Actor nikos = await Employee("nikos");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website", nikos);
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Post, "api/v1/tasks", nikos, new CreateTaskDTO { ProjectUuid = project.Uuid, Name = "Design" }));
        }

        [Fact]
        public async Task Manager_outside_the_project_cannot_create_a_task()
        {
            Actor owner = await Manager("maria");
            Actor other = await Manager("kostas");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Post, "api/v1/tasks", other, new CreateTaskDTO { ProjectUuid = project.Uuid, Name = "Design" }));
        }

        [Fact]
        public async Task Task_member_must_be_a_project_member()
        {
            Actor manager = await Manager("maria");
            Actor nikos = await Employee("nikos");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Post, "api/v1/tasks", manager, new CreateTaskDTO { ProjectUuid = project.Uuid, Name = "Design", MembersUuids = [nikos.Uuid] }));
        }

        [Fact]
        public async Task Create_in_unknown_project_returns_404()
        {
            Actor manager = await Manager("maria");
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Post, "api/v1/tasks", manager, new CreateTaskDTO { ProjectUuid = Guid.NewGuid(), Name = "Design" }));
        }

        [Fact]
        public async Task Create_with_unknown_member_returns_404()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Post, "api/v1/tasks", manager, new CreateTaskDTO { ProjectUuid = project.Uuid, Name = "Design", MembersUuids = [Guid.NewGuid()] }));
        }

        [Fact]
        public async Task Create_without_name_returns_400()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            await Expect(HttpStatusCode.BadRequest, await Send(HttpMethod.Post, "api/v1/tasks", manager, new CreateTaskDTO { ProjectUuid = project.Uuid }));
        }

        [Fact]
        public async Task Create_with_duplicate_name_in_project_returns_409()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            await CreateTask(manager, project.Uuid, "Design");
            await Expect(HttpStatusCode.Conflict, await Send(HttpMethod.Post, "api/v1/tasks", manager, new CreateTaskDTO { ProjectUuid = project.Uuid, Name = "Design" }));
        }

        [Fact]
        public async Task Update_saves_every_field_including_status()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design");

            await Expect(HttpStatusCode.OK, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", manager, TaskUpdate("Updated", manager)));

            TaskReadOnlyDTO saved = await Get<TaskReadOnlyDTO>($"api/v1/tasks/{task.Uuid}", manager);
            Assert.Equal("Updated", saved.Name);
            Assert.Equal("New description", saved.Description);
            Assert.Equal(ProjectTaskPriority.High, saved.Priority);
            Assert.Equal(WorkStatus.InProgress, saved.Status);
            Assert.Equal(new DateOnly(2030, 1, 15), saved.DueDate);
        }

        [Fact]
        public async Task Update_replaces_members_and_creator_stays()
        {
            Actor manager = await Manager("maria");
            Actor nikos = await Employee("nikos");
            Actor anna = await Employee("anna");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website", nikos, anna);
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design", nikos);

            await Expect(HttpStatusCode.OK, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", manager, TaskUpdate("Design", anna)));

            TaskReadOnlyDTO saved = await Get<TaskReadOnlyDTO>($"api/v1/tasks/{task.Uuid}", manager);
            Assert.Equal(2, saved.Members.Count);
            Assert.Contains(saved.Members, m => m.Uuid == anna.Uuid);
            Assert.Contains(saved.Members, m => m.Uuid == manager.Uuid);
            Assert.DoesNotContain(saved.Members, m => m.Uuid == nikos.Uuid);
        }

        [Fact]
        public async Task Other_manager_cannot_update_a_task()
        {
            Actor owner = await Manager("maria");
            Actor other = await Manager("kostas");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website", other);
            TaskReadOnlyDTO task = await CreateTask(owner, project.Uuid, "Design");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", other, TaskUpdate()));
        }

        [Fact]
        public async Task Employee_cannot_update_a_task()
        {
            Actor owner = await Manager("maria");
            Actor nikos = await Employee("nikos");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website", nikos);
            TaskReadOnlyDTO task = await CreateTask(owner, project.Uuid, "Design", nikos);
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", nikos, TaskUpdate()));
        }

        [Fact]
        public async Task Admin_can_update_any_task()
        {
            Actor owner = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website");
            TaskReadOnlyDTO task = await CreateTask(owner, project.Uuid, "Design");
            await Expect(HttpStatusCode.OK, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", await Admin(), TaskUpdate()));
        }

        [Fact]
        public async Task Update_with_non_project_member_returns_403()
        {
            Actor manager = await Manager("maria");
            Actor nikos = await Employee("nikos");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", manager, TaskUpdate("Design", nikos)));
        }

        [Fact]
        public async Task Project_creator_who_left_cannot_be_added_to_a_task()
        {
            Actor creator = await Manager("anna");
            Actor member = await Manager("kostas");
            ProjectReadOnlyDTO project = await CreateProject(creator, "Website", member);
            TaskReadOnlyDTO task = await CreateTask(member, project.Uuid, "Design");
            await Expect(HttpStatusCode.OK, await Send(HttpMethod.Post, $"api/v1/projects/{project.Uuid}/leave", creator));

            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", member, TaskUpdate("Design", creator)));
        }

        [Fact]
        public async Task Update_unknown_task_returns_404()
        {
            Actor manager = await Manager("maria");
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Put, $"api/v1/tasks/{Guid.NewGuid()}", manager, TaskUpdate()));
        }

        [Fact]
        public async Task Update_without_status_returns_400()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design");
            await Expect(HttpStatusCode.BadRequest, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", manager, new UpdateTaskDTO { Name = "Design", Priority = ProjectTaskPriority.Low }));
        }

        [Fact]
        public async Task Creator_deletes_a_task_and_it_disappears()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design");

            await Expect(HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"api/v1/tasks/{task.Uuid}", manager));

            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Get, $"api/v1/tasks/{task.Uuid}", manager));
            ProjectDetailsDTO details = await Get<ProjectDetailsDTO>($"api/v1/projects/{project.Uuid}", manager);
            Assert.Empty(details.Tasks);

            using var scope = Factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<TaskManagerContext>();
            var row = await context.ProjectTasks.IgnoreQueryFilters().SingleAsync(t => t.Uuid == task.Uuid);
            Assert.True(row.IsDeleted);
            Assert.NotNull(row.DeletedAt);
        }

        [Fact]
        public async Task Other_manager_cannot_delete_a_task()
        {
            Actor owner = await Manager("maria");
            Actor other = await Manager("kostas");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website", other);
            TaskReadOnlyDTO task = await CreateTask(owner, project.Uuid, "Design");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Delete, $"api/v1/tasks/{task.Uuid}", other));
        }

        [Fact]
        public async Task Admin_can_delete_any_task()
        {
            Actor owner = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website");
            TaskReadOnlyDTO task = await CreateTask(owner, project.Uuid, "Design");
            await Expect(HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"api/v1/tasks/{task.Uuid}", await Admin()));
        }

        [Fact]
        public async Task Employee_cannot_delete_a_task()
        {
            Actor owner = await Manager("maria");
            Actor nikos = await Employee("nikos");
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website", nikos);
            TaskReadOnlyDTO task = await CreateTask(owner, project.Uuid, "Design");
            await Expect(HttpStatusCode.Forbidden, await Send(HttpMethod.Delete, $"api/v1/tasks/{task.Uuid}", nikos));
        }

        [Fact]
        public async Task Delete_unknown_task_returns_404()
        {
            Actor manager = await Manager("maria");
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Delete, $"api/v1/tasks/{Guid.NewGuid()}", manager));
        }

        [Fact]
        public async Task Update_with_unknown_member_returns_404()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design");
            var update = TaskUpdate() with { MembersUuids = [Guid.NewGuid()] };
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", manager, update));
        }

        [Fact]
        public async Task Update_without_description_keeps_the_old_one()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            TaskReadOnlyDTO task = await CreateTask(manager, project.Uuid, "Design");
            await Expect(HttpStatusCode.OK, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", manager, TaskUpdate("Design", manager)));

            var update = TaskUpdate("Design", manager) with { Description = null };
            await Expect(HttpStatusCode.OK, await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", manager, update));

            TaskReadOnlyDTO saved = await Get<TaskReadOnlyDTO>($"api/v1/tasks/{task.Uuid}", manager);
            Assert.Equal("New description", saved.Description);
        }
    }

    public class SortingTests : ApiTestBase
    {
        private async Task CreateProjectDue(Actor actor, string name, DateOnly dueDate)
        {
            var response = await Send(HttpMethod.Post, "api/v1/projects", actor, new CreateProjectDTO { Name = name, DueDate = dueDate });
            await Expect(HttpStatusCode.Created, response);
        }

        [Fact]
        public async Task Projects_can_be_sorted_by_due_date()
        {
            Actor manager = await Manager("maria");
            await CreateProjectDue(manager, "Later", new DateOnly(2031, 1, 1));
            await CreateProjectDue(manager, "Sooner", new DateOnly(2030, 1, 1));

            var result = await Get<PaginatedResult<ProjectReadOnlyDTO>>("api/v1/projects?sortBy=DueDate", manager);
            Assert.Equal(["Sooner", "Later"], result.Data.Select(p => p.Name));
        }

        [Fact]
        public async Task Projects_can_be_sorted_by_newest()
        {
            Actor manager = await Manager("maria");
            await CreateProjectDue(manager, "Older", new DateOnly(2030, 1, 1));
            await CreateProjectDue(manager, "Newer", new DateOnly(2031, 1, 1));

            var result = await Get<PaginatedResult<ProjectReadOnlyDTO>>("api/v1/projects?sortBy=CreatedAt", manager);
            Assert.Equal(["Newer", "Older"], result.Data.Select(p => p.Name));
        }

        [Fact]
        public async Task Users_can_be_sorted_by_role()
        {
            await Employee("nikos");
            await Manager("maria");

            var result = await Get<PaginatedResult<UserReadOnlyDTO>>("api/v1/users?sortBy=Role", await Admin());
            Assert.Equal(["ADMIN", "MANAGER", "EMPLOYEE"], result.Data.Select(u => u.Role));
        }

        [Fact]
        public async Task Users_can_be_sorted_by_newest()
        {
            await Register("nikos");
            await Register("anna");

            var result = await Get<PaginatedResult<UserReadOnlyDTO>>("api/v1/users?sortBy=CreatedAt", await Admin());
            Assert.Equal(["anna", "nikos", ApiFactory.AdminUsername], result.Data.Select(u => u.Username));
        }
    }

    // A token stays valid after its user is deleted, so the services must still answer with 404.
    public class DeletedUserWithValidTokenTests : ApiTestBase
    {
        private async Task<Actor> DeletedManager()
        {
            Actor manager = await Manager("maria");
            await Expect(HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"api/v1/users/{manager.Uuid}", await Admin()));
            return manager;
        }

        private static async Task ExpectUserNotFound(HttpResponseMessage response)
        {
            await Expect(HttpStatusCode.NotFound, response);
            Assert.Contains("User with uuid", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task Project_actions_return_404()
        {
            Actor admin = await Admin();
            ProjectReadOnlyDTO project = await CreateProject(admin, "Website");
            Actor deleted = await DeletedManager();

            await ExpectUserNotFound(await Send(HttpMethod.Post, "api/v1/projects", deleted, new CreateProjectDTO { Name = "Other" }));
            await ExpectUserNotFound(await Send(HttpMethod.Put, $"api/v1/projects/{project.Uuid}", deleted, ProjectUpdate("Website", WorkStatus.Done, admin)));
            await ExpectUserNotFound(await Send(HttpMethod.Post, $"api/v1/projects/{project.Uuid}/join", deleted));
            await ExpectUserNotFound(await Send(HttpMethod.Post, $"api/v1/projects/{project.Uuid}/leave", deleted));
            await ExpectUserNotFound(await Send(HttpMethod.Delete, $"api/v1/projects/{project.Uuid}", deleted));
        }

        [Fact]
        public async Task Task_actions_return_404()
        {
            Actor admin = await Admin();
            ProjectReadOnlyDTO project = await CreateProject(admin, "Website");
            TaskReadOnlyDTO task = await CreateTask(admin, project.Uuid, "Design");
            Actor deleted = await DeletedManager();

            await ExpectUserNotFound(await Send(HttpMethod.Post, "api/v1/tasks", deleted, new CreateTaskDTO { ProjectUuid = project.Uuid, Name = "Other" }));
            await ExpectUserNotFound(await Send(HttpMethod.Put, $"api/v1/tasks/{task.Uuid}", deleted, TaskUpdate()));
            await ExpectUserNotFound(await Send(HttpMethod.Delete, $"api/v1/tasks/{task.Uuid}", deleted));
        }

        [Fact]
        public async Task Profile_actions_return_404()
        {
            Actor deleted = await DeletedManager();

            await ExpectUserNotFound(await Send(HttpMethod.Get, $"api/v1/users/{deleted.Uuid}", deleted));
            await ExpectUserNotFound(await Send(HttpMethod.Put, "api/v1/users/me", deleted, new UpdateUserDTO { Firstname = "First", Lastname = "Last" }));
            await ExpectUserNotFound(await Send(HttpMethod.Put, "api/v1/users/me/password", deleted, new ChangePasswordDTO { OldPassword = Password, NewPassword = "N3wPassw0rd!" }));
        }
    }

    public class EdgeCaseTests : ApiTestBase
    {
        private const int AdminRoleId = 1;

        private async Task<Actor> SecondAdmin()
        {
            UserReadOnlyDTO user = await Register("boss2");
            var response = await Send(HttpMethod.Put, $"api/v1/users/{user.Uuid}", await Admin(), new AdminUpdateUserDTO { Firstname = "First", Lastname = "Last", RoleId = AdminRoleId });
            await Expect(HttpStatusCode.OK, response);
            return await Login("boss2");
        }

        [Fact]
        public async Task Project_actions_on_unknown_project_return_404()
        {
            Actor admin = await Admin();
            Guid unknown = Guid.NewGuid();

            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Put, $"api/v1/projects/{unknown}", admin, ProjectUpdate("Website", WorkStatus.Done, admin)));
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Post, $"api/v1/projects/{unknown}/join", admin));
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Post, $"api/v1/projects/{unknown}/leave", admin));
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Delete, $"api/v1/projects/{unknown}", admin));
        }

        [Fact]
        public async Task Project_update_with_unknown_member_returns_400()
        {
            Actor manager = await Manager("maria");
            ProjectReadOnlyDTO project = await CreateProject(manager, "Website");
            var update = ProjectUpdate("Website", WorkStatus.Done) with { MembersUuids = [Guid.NewGuid()] };
            await Expect(HttpStatusCode.BadRequest, await Send(HttpMethod.Put, $"api/v1/projects/{project.Uuid}", manager, update));
        }

        [Fact]
        public async Task Admin_update_of_unknown_user_returns_404()
        {
            var response = await Send(HttpMethod.Put, $"api/v1/users/{Guid.NewGuid()}", await Admin(), new AdminUpdateUserDTO { Firstname = "First", Lastname = "Last", RoleId = ManagerRoleId });
            await Expect(HttpStatusCode.NotFound, response);
        }

        [Fact]
        public async Task Admin_update_with_unknown_role_returns_400()
        {
            UserReadOnlyDTO user = await Register("nikos");
            var response = await Send(HttpMethod.Put, $"api/v1/users/{user.Uuid}", await Admin(), new AdminUpdateUserDTO { Firstname = "First", Lastname = "Last", RoleId = 99 });
            await Expect(HttpStatusCode.BadRequest, response);
        }

        [Fact]
        public async Task Deleting_unknown_user_returns_404()
        {
            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Delete, $"api/v1/users/{Guid.NewGuid()}", await Admin()));
        }

        [Fact]
        public async Task Password_change_to_the_same_password_returns_400()
        {
            Actor nikos = await Employee("nikos");
            var response = await Send(HttpMethod.Put, "api/v1/users/me/password", nikos, new ChangePasswordDTO { OldPassword = Password, NewPassword = Password });
            await Expect(HttpStatusCode.BadRequest, response);
        }

        [Fact]
        public async Task Admin_can_be_demoted_when_another_admin_exists()
        {
            Actor admin = await Admin();
            await SecondAdmin();
            var response = await Send(HttpMethod.Put, $"api/v1/users/{admin.Uuid}", admin, new AdminUpdateUserDTO { Firstname = "System", Lastname = "Admin", RoleId = ManagerRoleId });
            await Expect(HttpStatusCode.OK, response);
            Assert.Equal("MANAGER", (await Read<UserReadOnlyDTO>(response)).Role);
        }

        [Fact]
        public async Task Admin_deleting_themselves_passes_projects_to_another_admin()
        {
            Actor admin = await Admin();
            Actor other = await SecondAdmin();
            ProjectReadOnlyDTO project = await CreateProject(admin, "Website");

            await Expect(HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"api/v1/users/{admin.Uuid}", admin));

            ProjectDetailsDTO details = await Get<ProjectDetailsDTO>($"api/v1/projects/{project.Uuid}", other);
            Assert.Equal(other.Uuid, details.CreatedBy.Uuid);
            Assert.Contains(details.Members, m => m.Uuid == other.Uuid);
        }

        [Fact]
        public async Task Deleted_admin_with_valid_token_cannot_delete_users()
        {
            Actor admin = await Admin();
            Actor other = await SecondAdmin();
            UserReadOnlyDTO target = await Register("nikos");
            await Expect(HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"api/v1/users/{other.Uuid}", admin));

            await Expect(HttpStatusCode.NotFound, await Send(HttpMethod.Delete, $"api/v1/users/{target.Uuid}", other));
        }

        [Fact]
        public async Task Deleting_a_task_creator_adds_the_admin_to_the_project()
        {
            Actor owner = await Manager("maria");
            Actor creator = await Manager("kostas");
            Actor admin = await Admin();
            ProjectReadOnlyDTO project = await CreateProject(owner, "Website", creator);
            TaskReadOnlyDTO task = await CreateTask(creator, project.Uuid, "Design");

            await Expect(HttpStatusCode.NoContent, await Send(HttpMethod.Delete, $"api/v1/users/{creator.Uuid}", admin));

            TaskReadOnlyDTO transferred = await Get<TaskReadOnlyDTO>($"api/v1/tasks/{task.Uuid}", admin);
            Assert.Equal(admin.Uuid, transferred.CreatedBy.Uuid);
            ProjectDetailsDTO details = await Get<ProjectDetailsDTO>($"api/v1/projects/{project.Uuid}", admin);
            Assert.Contains(details.Members, m => m.Uuid == admin.Uuid);
        }
    }

    // Replaces a service with one that crashes, to prove the exception handler hides internal details.
    public class CrashingProxy : DispatchProxy
    {
        public const string Secret = "secret internal detail";

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException(Secret);
    }

    public class UnexpectedErrorTests : ApiTestBase
    {
        private HttpClient ClientWithCrashing<TService>() where TService : class
        {
            var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddScoped(_ => DispatchProxy.Create<TService, CrashingProxy>())));
            return factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });
        }

        private static async Task ExpectHiddenError(HttpResponseMessage response)
        {
            await Expect(HttpStatusCode.InternalServerError, response);
            string body = await response.Content.ReadAsStringAsync();
            Assert.Contains("An unexpected error occurred.", body);
            Assert.DoesNotContain(CrashingProxy.Secret, body);
        }

        [Fact]
        public async Task Crash_for_a_logged_in_user_returns_500_without_details()
        {
            Actor admin = await Admin();
            using HttpClient client = ClientWithCrashing<ITaskService>();
            await ExpectHiddenError(await Send(HttpMethod.Get, $"api/v1/tasks/{Guid.NewGuid()}", admin, client: client));
        }

        [Fact]
        public async Task Crash_for_an_anonymous_user_returns_500_without_details()
        {
            using HttpClient client = ClientWithCrashing<IUserService>();
            await ExpectHiddenError(await Send(HttpMethod.Post, "api/v1/auth/login", body: new SignInDTO { Username = "nikos", Password = Password }, client: client));
        }
    }
}
