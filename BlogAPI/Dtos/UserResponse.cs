namespace BlogAPI.Dtos
{
    // DTO returned when reading user information from the API.
    // It exposes only safe fields (no password)
    public class UserResponse
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
