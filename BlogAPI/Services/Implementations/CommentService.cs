using AutoMapper;
using BlogAPI.Data;
using BlogAPI.Dtos;
using BlogAPI.Models;
using BlogAPI.Repositories.Interfaces;
using BlogAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogAPI.Services.Implementations
{
    // CommentService contains the business rules for working with comments.
    // It uses ICommentRepository for data access and AutoMapper to return DTOs.
    public class CommentService : ICommentService
    {
        private readonly AppDbContext _context;
        private readonly ICommentRepository _commentRepository;
        private readonly IMapper _mapper;

        // Dependencies are injected via the constructor.
        public CommentService(
            AppDbContext context,
            ICommentRepository commentRepository,
            IMapper mapper)
        {
            _context = context;
            _commentRepository = commentRepository;
            _mapper = mapper;
        }

        public async Task<List<CommentResponse>?> GetCommentsForPostAsync(int postId)
        {
            var postExists = await _context.Posts.AnyAsync(p => p.Id == postId);
            if (!postExists)
            {
                return null; // controller får returnera 404
            }

            var comments = await _commentRepository.GetForPostAsync(postId);
            return _mapper.Map<List<CommentResponse>>(comments);
        }

        public async Task<CommentResponse> CreateAsync(int postId, CreateCommentRequest request)
        {
            var post = await _context.Posts.FindAsync(postId)
                       ?? throw new ArgumentException("Post not found.");

            var user = await _context.Users.FindAsync(request.UserId)
                       ?? throw new ArgumentException("User not found.");

            if (post.UserId == request.UserId)
            {
                throw new UnauthorizedAccessException("You cannot comment on your own post.");
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("Comment text is required.");
            }

            var comment = new Comment
            {
                Text = request.Text,
                PostId = postId,
                UserId = request.UserId
            };

            await _commentRepository.AddAsync(comment);

            // ladda med User för att kunna mappa UserName om repo inte redan gjorde det
            comment = await _commentRepository.GetByIdAsync(comment.Id) ?? comment;

            return _mapper.Map<CommentResponse>(comment);
        }
    }
}
