using BlogAPI.Models;

namespace BlogAPI.Repositories.Interfaces
{
    // Repository interface for accessing BlogPost data.
    // This hides EF Core from higher layers (services/controllers)
    // Makes data access easier to test and to change later.
    public interface IPostRepository
    {
        Task<List<BlogPost>> GetAllAsync();
        Task<BlogPost?> GetByIdAsync(int id);
        Task<List<BlogPost>> SearchAsync(string? title, int? categoryId);
        Task AddAsync(BlogPost post);
        Task UpdateAsync(BlogPost post);
        Task DeleteAsync(BlogPost post);
        Task<bool> ExistsAsync(int id);
    }
}
