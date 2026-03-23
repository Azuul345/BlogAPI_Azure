using AutoMapper;
using BlogAPI.Data;
using BlogAPI.Dtos;
using BlogAPI.Models;
using BlogAPI.Repositories.Interfaces;
using BlogAPI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BlogAPI.Services.Implementations
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly AppDbContext _context;
        private readonly IMapper _mapper;

        // All dependencies are injected via the constructor.
        public UserService(
            IUserRepository userRepository,
            AppDbContext context,
            IMapper mapper)
        {
            _userRepository = userRepository;
            _context = context;
            _mapper = mapper;
        }

        public async Task<UserResponse> RegisterAsync(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserName) ||
                string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                throw new ArgumentException("UserName, Email and Password are required.");
            }

            var existing = await _userRepository.GetByUserNameAsync(request.UserName);
            if (existing != null)
            {
                throw new ArgumentException("Username is already taken.");
            }

            var user = new User
            {
                UserName = request.UserName,
                Email = request.Email,
                Password = request.Password // save as hash in real application
            };

            await _userRepository.AddAsync(user);

            return _mapper.Map<UserResponse>(user);
        }

        public async Task<UserResponse?> LoginAsync(LoginRequest request)
        {
            var user = await _userRepository.GetByUserNameAsync(request.UserName);
            if (user == null || user.Password != request.Password)
            {
                return null;
            }

            return _mapper.Map<UserResponse>(user);
        }

        public async Task<bool> UpdateAsync(int id, UpdateUserRequest request)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return false;

            if (user.Id != request.UserId)
            {
                throw new UnauthorizedAccessException("You are not allowed to edit this user.");
            }

            user.UserName = request.UserName;
            user.Email = request.Email;
            user.Password = request.Password;

            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, int userId)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null) return false;

            if (user.Id != userId)
            {
                throw new UnauthorizedAccessException("You are not allowed to delete this user.");
            }

         
            // Remove all comments written by this user manually before deleting the user.
            // Using DeleteBehavior.Restrict on User to Comments in AppDbContext to avoid
            // multiple cascade paths in the database, so this has to be done in code.
            var userComments = await _context.Comments
                .Where(c => c.UserId == id)
                .ToListAsync();
            _context.Comments.RemoveRange(userComments);

            await _userRepository.DeleteAsync(user);
            return true;
        }

        public async Task<List<UserResponse>> GetAllAsync()
        {
            var users = await _userRepository.GetAllAsync();
            return _mapper.Map<List<UserResponse>>(users);
        }
    }
}
