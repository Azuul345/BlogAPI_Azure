namespace BlogAPI.Dtos
{
    // DTO returned when reading posts from the API.
    // It flattens the BlogPost entity and includes the author's user name
    // and the category name instead of nested objects.
    public class PostResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

        // UserName of the author (mapped from User.UserName)
        public string UserName { get; set; } = string.Empty;
        // Name of the category (mapped from Category.Name)
        public string CategoryName { get; set; } = string.Empty;
    }
}
