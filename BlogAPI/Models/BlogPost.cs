using System.Text.Json.Serialization;

namespace BlogAPI.Models
{
    // BlogPost is the domain model / EF Core entity representing a blog post.
    // It links to a User (author), a Category, and has a collection of Comments.
    public class BlogPost
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int UserId { get; set; }

        // Ignored in JSON to prevent circular references when returning entities.
        [JsonIgnore]
        public User User { get; set; }
        public int CategoryId { get; set; }
        public Category Category { get; set; }
        public List<Comment> Comments { get; set; } = new List<Comment>();
    }
}
