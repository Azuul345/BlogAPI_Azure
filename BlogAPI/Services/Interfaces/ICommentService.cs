using BlogAPI.Dtos;

namespace BlogAPI.Services.Interfaces
{
    // Service interface for business logic related to comments.
    public interface ICommentService
    {
        Task<List<CommentResponse>?> GetCommentsForPostAsync(int postId);
        Task<CommentResponse> CreateAsync(int postId, CreateCommentRequest request);
    }
}
