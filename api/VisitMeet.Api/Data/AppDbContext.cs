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
    public DbSet<LoanCategory> LoanCategories => Set<LoanCategory>();
    public DbSet<EngagementGuide> EngagementGuides => Set<EngagementGuide>();
    public DbSet<GuideGroup> GuideGroups => Set<GuideGroup>();
    public DbSet<GuideTask> GuideTasks => Set<GuideTask>();
    public DbSet<DeviationRule> DeviationRules => Set<DeviationRule>();
    public DbSet<AssessmentApplication> AssessmentApplications => Set<AssessmentApplication>();
    public DbSet<Applicant> Applicants => Set<Applicant>();
    public DbSet<ApplicantInput> ApplicantInputs => Set<ApplicantInput>();
    public DbSet<RuleRun> RuleRuns => Set<RuleRun>();
    public DbSet<RuleRunResult> RuleRunResults => Set<RuleRunResult>();
    public DbSet<EngagementSession> EngagementSessions => Set<EngagementSession>();
    public DbSet<SessionTaskAnswer> SessionTaskAnswers => Set<SessionTaskAnswer>();

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
            e.Property(x => x.Branch).HasMaxLength(100);
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
            e.Property(x => x.Branch).HasMaxLength(100);
            e.Property(x => x.GroupId).HasMaxLength(64);
            e.Property(x => x.MemberId).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(40);
            e.Property(x => x.GeoError).HasMaxLength(120);
            e.HasOne(x => x.CreditOfficer).WithMany().HasForeignKey(x => x.CreditOfficerId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Messages).WithOne(x => x.RecipientWaitingOfficer)
                .HasForeignKey(x => x.RecipientWaitingOfficerId);
        });

        modelBuilder.Entity<Snapshot>(e =>
        {
            e.Property(x => x.Category).HasMaxLength(64);
            e.Property(x => x.FileName).HasMaxLength(200);
            e.Property(x => x.GeoError).HasMaxLength(120);
        });

        modelBuilder.Entity<LobbyMessage>(e =>
        {
            e.HasIndex(x => new { x.HostSlug, x.SentAt });
            e.Property(x => x.HostSlug).HasMaxLength(32);
            e.Property(x => x.Body).HasMaxLength(2000);
        });

        modelBuilder.Entity<LoanCategory>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(120);
        });

        modelBuilder.Entity<EngagementGuide>(e =>
        {
            e.HasIndex(x => new { x.Code, x.Version }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Status).HasMaxLength(24);
            e.HasMany(x => x.Groups).WithOne(x => x.Guide).HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Tasks).WithOne(x => x.Guide).HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GuideGroup>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
        });

        modelBuilder.Entity<GuideTask>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200);
            e.HasOne(x => x.Group).WithMany(x => x.Tasks).HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DeviationRule>(e =>
        {
            e.Property(x => x.CheckKey).HasMaxLength(120);
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Severity).HasMaxLength(24);
        });

        modelBuilder.Entity<AssessmentApplication>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(12);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.HasOne(x => x.LoanCategory).WithMany().HasForeignKey(x => x.LoanCategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Applicants).WithOne(x => x.Application).HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Applicant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Role).HasMaxLength(24);
            e.HasMany(x => x.Inputs).WithOne(x => x.Applicant).HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.RuleRuns).WithOne(x => x.Applicant).HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RuleRun>(e =>
        {
            e.Property(x => x.Kind).HasMaxLength(32);
            e.HasMany(x => x.Results).WithOne(x => x.RuleRun).HasForeignKey(x => x.RuleRunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EngagementSession>(e =>
        {
            e.Property(x => x.Mode).HasMaxLength(24);
            e.Property(x => x.Status).HasMaxLength(24);
            e.HasOne(x => x.Application).WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Guide).WithMany().HasForeignKey(x => x.GuideId).OnDelete(DeleteBehavior.Restrict);
            e.HasMany(x => x.Answers).WithOne(x => x.Session).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionTaskAnswer>(e =>
        {
            e.HasIndex(x => new { x.SessionId, x.TaskNumber }).IsUnique();
            e.Property(x => x.Status).HasMaxLength(24);
        });
    }
}
