namespace BlogAPI.Dtos
{
    // DTO used as request body when registering a new user account.
    // Don't exposes the User entity directly from the API.
    public class RegisterRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;  // simple variant for the assignment
    }

    // DTO used as request body when logging in.
    // The API returns the userId if the credentials are correct.
    public class LoginRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    // DTO used when updating a user account.
    // UserId is the id of the logged-in user who is trying to update the account.
    public class UpdateUserRequest
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty; 
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

    }

    // DTO used as request body when creating a new blog post.
    // UserId is the id returned from login, CategoryId must refer to a valid category.
    public class CreatePostRequest
    {
        public int UserId { get; set; }      // id of the logged-in user
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int CategoryId { get; set; }  // id of an existing Category
    }


    // DTO used when updating an existing blog post.
    // UserId is used to check that only the owner can update the post.
    public class UpdatePostRequest
    {
        public int UserId { get; set; }      // id of the user trying to update the post
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int CategoryId { get; set; }
    }


    // DTO used as request body when creating a new comment on a post.
    // UserId is the id of the logged-in user who writes the comment.
    public class CreateCommentRequest
    {
        public int UserId { get; set; }   // id of the logged-in user
        public string Text { get; set; } = string.Empty;
    }

    // DTO used as request body when creating a new category.
    public class CreateCategoryRequest
    {
        public string Name { get; set; } = string.Empty; 
    }

  
    

}
