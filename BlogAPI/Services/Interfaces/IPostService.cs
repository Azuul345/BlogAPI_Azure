using BlogAPI.Dtos;

namespace BlogAPI.Services.Interfaces
{
    // Service interface for business logic related to blog posts
    // Controllers depend on this abstraction instead of talking to repositories directly
    public interface IPostService
    {
        Task<List<PostResponse>> GetAllAsync();
        Task<PostResponse?> GetByIdAsync(int id);
        Task<List<PostResponse>> SearchAsync(string? title, int? categoryId);
        Task<PostResponse> CreateAsync(CreatePostRequest request);
        Task<bool> UpdateAsync(int id, UpdatePostRequest request);
        Task<bool> DeleteAsync(int id, int userId);
    }
}
