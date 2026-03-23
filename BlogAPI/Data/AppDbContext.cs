using BlogAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace BlogAPI.Data
{
    // AppDbContext is the EF Core context for this application.
    // It defines the tables (DbSet properties) and configures the
    // relationships between the entities.
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<BlogPost> Posts   { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Comment> Comments { get; set; }

        // Configure relationships and deletion behavior using Fluent API.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Call the base implementation first.
            base.OnModelCreating(modelBuilder);

            // When a user is deleted, all their posts are deleted (cascade).
            modelBuilder.Entity<BlogPost>()
                .HasOne(p => p.User)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // When a post is deleted, all comments on that post are deleted (cascade).
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            // A user can have many comments, but deleting a user does not automatically
            // delete the comments to avoid multiple cascade paths. 
            // Handle removal of user comments manually in UserService.
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }

    }
}
