using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using VisitMeet.Api.Data;
using VisitMeet.Api.Dtos;
using VisitMeet.Api.Models;
using VisitMeet.Api.Services;

namespace VisitMeet.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = Roles.Admin)]
public class UsersController(AppDbContext db, MeetingMapper mapper) : ControllerBase
{
    [HttpGet("credit-officers")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> CreditOfficers()
    {
        var users = await db.Users
            .Where(u => u.Role == Roles.CreditOfficer)
            .OrderBy(u => u.Name)
            .ToListAsync();
        return users.Select(mapper.ToUser).ToList();
    }

    [HttpPost("credit-officers")]
    public async Task<ActionResult<UserDto>> CreateCreditOfficer(CreateCreditOfficerRequest request)
    {
        var name = request.Name.Trim();
        var email = request.Email.Trim().ToLowerInvariant();
        if (name.Length < 2)
        {
            return BadRequest(new { message = "Name is required." });
        }
        if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
        {
            return BadRequest(new { message = "Enter a valid email address." });
        }
        if (request.TemporaryPassword.Length < 8)
        {
            return BadRequest(new { message = "Temporary password must be at least 8 characters." });
        }
        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        string slug;
        do
        {
            slug = Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant();
        } while (await db.Users.AnyAsync(u => u.HostSlug == slug));

        var officer = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.TemporaryPassword),
            Role = Roles.CreditOfficer,
            HostSlug = slug
        };
        db.Users.Add(officer);
        await db.SaveChangesAsync();
        return Created($"/api/users/credit-officers/{officer.Id}", mapper.ToUser(officer));
    }
}
