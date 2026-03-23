using BlogAPI.Models;

namespace BlogAPI.Repositories.Interfaces
{
    // Repository interface for accessing Comment data.
    public interface ICommentRepository
    {
        Task<List<Comment>> GetForPostAsync(int postId);
        Task<Comment?> GetByIdAsync(int id);
        Task AddAsync(Comment comment);
        Task DeleteAsync(Comment comment);
    }
}
