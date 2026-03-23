using BlogAPI.Models;

namespace BlogAPI.Repositories.Interfaces
{
    // Repository interface for accessing User data.
    // Controllers and services depend on this abstraction instead of EF Core directly
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByUserNameAsync(string userName);
        Task<List<User>> GetAllAsync();
        Task AddAsync(User user);
        Task UpdateAsync(User user);
        Task DeleteAsync(User user);
    }
}
