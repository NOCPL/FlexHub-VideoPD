using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
}
