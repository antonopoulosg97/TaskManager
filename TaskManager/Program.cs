using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using TaskManager.Configuration;
using TaskManager.Data;
using TaskManager.Handlers;
using TaskManager.Repositories;
using TaskManager.Security;
using TaskManager.Services;
using Microsoft.OpenApi;

namespace TaskManager
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
            builder.Services.AddScoped<IProjectTaskRepository, ProjectTaskRepository>();

            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IProjectService, ProjectService>();
            builder.Services.AddScoped<ITaskService, TaskService>();

            builder.Services.AddDbContext<TaskManagerContext>(options =>
                options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            builder.Services.AddAutoMapper(cfg => { }, typeof(MapperConfig));
            builder.Services.AddSingleton<IEncryptionUtil, EncryptionUtil>();
            builder.Services.AddSingleton<ITokenService, TokenService>();


            string jwtKey = builder.Configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Jwt:Key is missing. Set it with: dotnet user-secrets set \"Jwt:Key\" \"<key>\"");

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidateAudience = true,
                        ValidAudience = builder.Configuration["Jwt:Audience"],
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.Zero,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                    };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("MANAGE_USERS", p => p.RequireClaim("capability", "MANAGE_USERS"));
                options.AddPolicy("CREATE_PROJECT", p => p.RequireClaim("capability", "CREATE_PROJECT"));
                options.AddPolicy("EDIT_PROJECT", p => p.RequireClaim("capability", "EDIT_PROJECT"));
                options.AddPolicy("DELETE_PROJECT", p => p.RequireClaim("capability", "DELETE_PROJECT"));
                options.AddPolicy("MANAGE_TASKS", p => p.RequireClaim("capability", "MANAGE_TASKS"));
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("Frontend", policy => policy.WithOrigins("http://localhost:5173")
                .AllowAnyMethod()
                .AllowAnyHeader());
            });

            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddProblemDetails();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi(options =>
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    document.Components ??= new OpenApiComponents();
                    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT"
                    };

                    document.Security ??= new List<OpenApiSecurityRequirement>();
                    document.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
                    });

                    return Task.CompletedTask;
                });
            });



            var app = builder.Build();
            await DbSeeder.SeedAdminAsync(app.Services);

            // Configure the HTTP request pipeline.
            app.UseExceptionHandler();
            app.UseHttpsRedirection();
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "TaskManager v1"));
            }

            app.UseCors("Frontend");
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            await app.RunAsync();
        }
    }
}
