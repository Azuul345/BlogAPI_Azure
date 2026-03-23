using System.Text.Json.Serialization;

namespace BlogAPI.Models
{
    // User is the domain model / EF Core entity representing a user account in the database.
    // It contains login information and navigation properties to posts and comments.
    public class User
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;


        
        // Marked with JsonIgnore to avoid circular references when serializing to JSON.
        [JsonIgnore]
        public List<BlogPost> Posts { get; set; } = new List<BlogPost>();

        [JsonIgnore]
        public List<Comment> Comments { get; set; } = new List<Comment>();
    }
}
