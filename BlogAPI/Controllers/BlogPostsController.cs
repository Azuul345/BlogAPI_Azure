using AutoMapper;
using BlogAPI.Data;
using BlogAPI.Dtos;
using BlogAPI.Models;
using BlogAPI.Services.Implementations;
using BlogAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogAPI.Controllers
{
    // PostsController is the HTTP API layer for blog posts and their comments.
    // It delegates all business logic to IPostService and ICommentService.
    [ApiController]
    [Route("api/[controller]")]
    public class PostsController : ControllerBase
    {
        private readonly IPostService _postService;
        private readonly ICommentService _commentService;

        // Services are injected via constructor injection
        public PostsController(IPostService postService, ICommentService commentService)
        {
            _postService = postService;
            _commentService = commentService;
        }

        // GET: api/posts
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PostResponse>>> GetPosts()
        {
            var posts = await _postService.GetAllAsync();
            return Ok(posts);
        }



        // GET: api/posts/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PostResponse>> GetPost(int id)
        {
            var post = await _postService.GetByIdAsync(id);
            if (post == null) return NotFound();
            return Ok(post);
        }

        // POST: api/posts
        [HttpPost]
        public async Task<ActionResult<PostResponse>> CreatePost(CreatePostRequest request)
        {
            try
            {
                var post = await _postService.CreateAsync(request);
                return CreatedAtAction(nameof(GetPost), new { id = post.Id }, post);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }




        // PUT: api/posts/1
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePost(int id, UpdatePostRequest request)
        {
            try
            {
                var updated = await _postService.UpdateAsync(id, request);
                if (!updated) return NotFound();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }


        // DELETE: api/posts/1
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePost(int id, [FromQuery] int userId)
        {
            try
            {
                var deleted = await _postService.DeleteAsync(id, userId);
                if (!deleted) return NotFound();
                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }


        // GET: api/posts/search?title=...&categoryId=...
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<PostResponse>>> SearchPosts(
    [FromQuery] string? title,
    [FromQuery] int? categoryId)
        {
            var posts = await _postService.SearchAsync(title, categoryId);
            return Ok(posts);
        }

        // GET: api/posts/1/comments
        [HttpGet("{postId}/comments")]
        public async Task<ActionResult<IEnumerable<CommentResponse>>> GetCommentsForPost(int postId)
        {
            var comments = await _commentService.GetCommentsForPostAsync(postId);
            if (comments == null)
            {
                return NotFound("Post not found.");
            }

            return Ok(comments);
        }

        // POST: api/posts/1/comments
        [HttpPost("{postId}/comments")]
        public async Task<ActionResult<CommentResponse>> CreateComment(int postId, CreateCommentRequest request)
        {
            try
            {
                var comment = await _commentService.CreateAsync(postId, request);
                return CreatedAtAction(nameof(GetCommentsForPost),
                    new { postId = postId }, comment);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message); // Post or user not found, or invalid text
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message); // Trying to comment own post
            }
        }





    }
}
