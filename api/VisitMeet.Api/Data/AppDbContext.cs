using Microsoft.EntityFrameworkCore;
using VisitMeet.Api.Models;

namespace VisitMeet.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<Recording> Recordings => Set<Recording>();
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<WaitingOfficer> WaitingOfficers => Set<WaitingOfficer>();
    public DbSet<LobbyMessage> LobbyMessages => Set<LobbyMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.HostSlug).IsUnique();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(40);
            e.Property(x => x.HostSlug).HasMaxLength(32);
        });

        modelBuilder.Entity<Meeting>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(12);
            e.Property(x => x.Bank).HasMaxLength(64);
            e.Property(x => x.GroupId).HasMaxLength(64);
            e.Property(x => x.MemberId).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(40);
            e.HasOne(x => x.CreditOfficer).WithMany().HasForeignKey(x => x.CreditOfficerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessage>(e =>
        {
            e.Property(x => x.Body).HasMaxLength(2000);
        });

        modelBuilder.Entity<WaitingOfficer>(e =>
        {
            e.HasIndex(x => new { x.HostSlug, x.Status });
            e.Property(x => x.HostSlug).HasMaxLength(32);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.Bank).HasMaxLength(64);
            e.Property(x => x.GroupId).HasMaxLength(64);
            e.Property(x => x.MemberId).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(40);
            e.HasOne(x => x.CreditOfficer).WithMany().HasForeignKey(x => x.CreditOfficerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Messages).WithOne(x => x.WaitingOfficer).HasForeignKey(x => x.WaitingOfficerId);
        });

        modelBuilder.Entity<LobbyMessage>(e =>
        {
            e.Property(x => x.Body).HasMaxLength(2000);
        });
    }
}
