using AutoMapper;
using BlogAPI.Data;
using BlogAPI.Profiles;
using BlogAPI.Repositories.Implementations;
using BlogAPI.Repositories.Interfaces;
using BlogAPI.Services.Implementations;
using BlogAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;



namespace BlogAPI
{
    public class Program
    {
        // Program.cs is the entry point of the application.
        // Here it configure dependency injection, EF Core, AutoMapper, CORS, Swagger
        // and set up the HTTP request pipeline.

        public static void Main(string[] args)
        {
            // Create the builder which holds configuration, logging and DI container.
            var builder = WebApplication.CreateBuilder(args);


            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

            // Register AppDbContext so it can be injected into repositories and services.
            // EF Core will use SQL Server with the given connection string.
            builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

            // Register AutoMapper and scans the assembly for Profile classes (BlogProfile).
            builder.Services.AddAutoMapper(typeof(Program));


            // Register repositories (data access layer) for dependency injection.
            // Controllers and services only depend on interfaces, not concrete classes.
            builder.Services.AddScoped<IPostRepository, PostRepository>();
            builder.Services.AddScoped<ICommentRepository, CommentRepository>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();

            // Register services (business logic layer) for dependency injection.
            builder.Services.AddScoped<IPostService, PostService>();
            builder.Services.AddScoped<ICommentService, CommentService>();
            builder.Services.AddScoped<IUserService, UserService>();

            // Register controller support (API controllers).
            builder.Services.AddControllers();

            // Register Swagger/OpenAPI support so the API can be explored and tested.
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Register CORS so it can configure cross-origin access in the pipeline.
            builder.Services.AddCors();

            // Build the WebApplication (creates the DI container and middleware pipeline).
            var app = builder.Build();

            // In development it expose Swagger UI to document and test the API.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            // Redirect HTTP requests to HTTPS.
            app.UseHttpsRedirection();

            // Enable CORS so any frontend (during development) can call this API.
            // In a real production system this would usually be restricted to known origins.
            app.UseCors(policy => policy
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader());

            // Add authorization middleware (required if later adds [Authorize] attributes).
            app.UseAuthorization();

            // Map attribute-routed controllers (e.g. [Route("api/[controller]")]).
            app.MapControllers();

            // Start the application and begin listening for HTTP requests.
            app.Run();
        }
    }
}
