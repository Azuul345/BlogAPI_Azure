using BlogAPI.Dtos;
using BlogAPI.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;


// UsersController exposes HTTP endpoints for managing user accounts:
// register, login, update, delete and list users.
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        try
        {
            var user = await _userService.RegisterAsync(request);
            return Ok(user); // innehåller Id, UserName, Email
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _userService.LoginAsync(request);
        if (user == null)
        {
            return Unauthorized("Invalid username or password.");
        }

        // enligt uppgiften ska du ge tillbaka userId; här kan du skicka både DTO och id
        return Ok(new { userId = user.Id, user.UserName, user.Email });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, UpdateUserRequest request)
    {
        try
        {
            var updated = await _userService.UpdateAsync(id, request);
            if (!updated) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id, [FromQuery] int userId)
    {
        try
        {
            var deleted = await _userService.DeleteAsync(id, userId);
            if (!deleted) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetUsers()
    {
        var users = await _userService.GetAllAsync();
        return Ok(users);
    }
}
