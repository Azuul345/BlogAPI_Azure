namespace BlogAPI.Dtos
{
    // DTO returned when reading comments from the API.
    // It contains basic information plus the author's user name.
    public class CommentResponse
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public int PostId { get; set; }
        public int UserId { get; set; }

        // UserName of the author (mapped from User.UserName)
        public string UserName { get; set; } = string.Empty;
    }
}
