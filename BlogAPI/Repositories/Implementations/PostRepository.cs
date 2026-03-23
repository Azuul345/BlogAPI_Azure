using BlogAPI.Data;
using BlogAPI.Models;
using BlogAPI.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogAPI.Repositories.Implementations
{
    // Concrete implementation of IPostRepository using EF Core and AppDbContext.
    public class PostRepository : IPostRepository
    {
        private readonly AppDbContext _context;

        // AppDbContext is injected via dependency injection.
        public PostRepository(AppDbContext context)
        {
            _context = context;
        }

        // Load all posts including author (User) and Category navigation properties.
        public async Task<List<BlogPost>> GetAllAsync()
        {
            return await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Category)
                .ToListAsync();
        }

        // Load a single post by id including User and Category.
        public async Task<BlogPost?> GetByIdAsync(int id)
        {
            return await _context.Posts
                .Include(p => p.User)
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        // Build a query dynamically based on optional search parameters.
        // If title is provided, filter by Title.Contains(title).
        // If categoryId is provided, filter by CategoryId.
        public async Task<List<BlogPost>> SearchAsync(string? title, int? categoryId)
        {
            var query = _context.Posts
                .Include(p => p.Category)
                .Include(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(title))
            {
                query = query.Where(p => p.Title.Contains(title));
            }

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            return await query.ToListAsync();
        }

        public async Task AddAsync(BlogPost post)
        {
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(BlogPost post)
        {
            _context.Posts.Update(post);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(BlogPost post)
        {
            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(int id)
        {
            return await _context.Posts.AnyAsync(p => p.Id == id);
        }
    }
}
