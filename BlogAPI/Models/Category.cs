using System.Text.Json.Serialization;

namespace BlogAPI.Models
{
    // Category represents a type of post (e.g. Training, Fashion, Health).
    // Valid categories are stored in their own table.
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // Ignored in JSON to avoid circular references.
        [JsonIgnore]
        public List<BlogPost> Posts {  get; set; } = new List<BlogPost>();
    }
}
