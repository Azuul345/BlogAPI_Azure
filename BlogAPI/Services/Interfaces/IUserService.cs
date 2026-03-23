using BlogAPI.Dtos;

namespace BlogAPI.Services.Interfaces
{
    public interface IUserService
    {
        Task<UserResponse> RegisterAsync(RegisterRequest request);
        Task<UserResponse?> LoginAsync(LoginRequest request);
        Task<bool> UpdateAsync(int id, UpdateUserRequest request);
        Task<bool> DeleteAsync(int id, int userId);
        Task<List<UserResponse>> GetAllAsync();
    }
}
