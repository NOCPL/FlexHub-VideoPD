using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Auth;
using VisitMeet.Api.Data;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;
using VisitMeet.Api.Services;

namespace VisitMeet.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens, MeetingMapper mapper) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }
        if (user.Role is not (Roles.CreditOfficer or Roles.Admin))
        {
            return Unauthorized(new { message = "Only credit officers and admins can sign in." });
        }

        return new AuthResponse(tokens.CreateToken(user), mapper.ToUser(user));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> Me()
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        if (user is null)
        {
            return Unauthorized();
        }
        return mapper.ToUser(user);
    }
}
