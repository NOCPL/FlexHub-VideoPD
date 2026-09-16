using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Models;

namespace VisitMeet.Api.Data;

public static class SeedData
{
    public static async Task EnsureSeededAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var credit = new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "Priya Shah",
            Email = "credit@visit.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Credit@123"),
            Role = Roles.CreditOfficer,
            HostSlug = "h8k2m9q4w1"
        };
        var admin = new User
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Name = "VisitMeet Admin",
            Email = "admin@visit.local",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Role = Roles.Admin
        };

        db.Users.AddRange(credit, admin);

        var soon = DateTimeOffset.UtcNow.AddMinutes(5);
        var meeting = new Meeting
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Code = "VK7M2Q",
            Bank = "SBI",
            GroupId = "G-22",
            MemberId = "M-10482",
            MemberName = "Lakshmi Self-Help Group — Meena Devi",
            ScheduledAt = soon,
            Status = MeetingStatuses.Scheduled,
            CreditOfficerId = credit.Id,
            LiveKitRoomName = "visit-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
    }
}
