using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VisitMeet.Api.Models;

namespace VisitMeet.Api.Auth;

public class TokenService(IConfiguration config)
{
    public string CreateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role)
        };
        if (!string.IsNullOrEmpty(user.HostSlug))
        {
            claims.Add(new Claim("hostSlug", user.HostSlug));
        }
        return Write(claims, TimeSpan.FromHours(12));
    }

    public string CreateGuestToken(Guid guestId, string displayName, Guid waitingOfficerId, Guid? meetingId = null)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, guestId.ToString()),
            new(ClaimTypes.NameIdentifier, guestId.ToString()),
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Role, Roles.FieldGuest),
            new("waitingId", waitingOfficerId.ToString())
        };
        if (meetingId is Guid mid)
        {
            claims.Add(new Claim("meetingId", mid.ToString()));
        }
        return Write(claims, TimeSpan.FromHours(4));
    }

    private string Write(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SigningKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class ClaimsExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    public static Guid? GetMeetingId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("meetingId");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    public static Guid? GetWaitingId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue("waitingId");
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
