using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RiskGuard.Domain.Entities;
using RiskGuard.Persistence.Identity;

namespace RiskGuard.Persistence;

public sealed class RiskGuardDbContext(DbContextOptions<RiskGuardDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RiskCategory> RiskCategories => Set<RiskCategory>();
    public DbSet<AssessmentQuestion> AssessmentQuestions => Set<AssessmentQuestion>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentResponse> AssessmentResponses => Set<AssessmentResponse>();
    public DbSet<RiskItem> Risks => Set<RiskItem>();
    public DbSet<RiskScore> RiskScores => Set<RiskScore>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<ComplianceFramework> ComplianceFrameworks => Set<ComplianceFramework>();
    public DbSet<ComplianceControl> ComplianceControls => Set<ComplianceControl>();
    public DbSet<ComplianceGap> ComplianceGaps => Set<ComplianceGap>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentComment> IncidentComments => Set<IncidentComment>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorAssessment> VendorAssessments => Set<VendorAssessment>();
    public DbSet<BusinessContinuityPlan> BusinessContinuityPlans => Set<BusinessContinuityPlan>();
    public DbSet<CriticalSystem> CriticalSystems => Set<CriticalSystem>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AiInteraction> AiInteractions => Set<AiInteraction>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        foreach (var property in builder.Model.GetEntityTypes()
                     .SelectMany(type => type.GetProperties())
                     .Where(property => property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?)))
        {
            property.SetPrecision(10);
            property.SetScale(2);
        }

        builder.Entity<Organization>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<Department>().HasIndex(x => new { x.OrganizationId, x.Name }).IsUnique();
        builder.Entity<RiskCategory>().HasIndex(x => x.Type).IsUnique();
        builder.Entity<AssessmentQuestion>().HasIndex(x => new { x.RiskCategoryId, x.Text }).IsUnique();
        builder.Entity<AssessmentResponse>().HasIndex(x => new { x.AssessmentId, x.QuestionId }).IsUnique();
        builder.Entity<RiskScore>().HasIndex(x => new { x.AssessmentId, x.CalculatedAtUtc });
        builder.Entity<ComplianceFramework>().HasIndex(x => x.Name).IsUnique();
        builder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        builder.Entity<RefreshToken>().HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
        builder.Entity<Notification>().HasIndex(x => new { x.UserId, x.IsRead });
        builder.Entity<AuditLog>().HasIndex(x => x.CreatedAtUtc);

        builder.Entity<Organization>().Property(x => x.Name).HasMaxLength(160);
        builder.Entity<Department>().Property(x => x.Name).HasMaxLength(120);
        builder.Entity<RiskCategory>().Property(x => x.Name).HasMaxLength(120);
        builder.Entity<Assessment>().Property(x => x.Title).HasMaxLength(180);
        builder.Entity<Assessment>().Property(x => x.AssignedToUserId).HasMaxLength(64);
        builder.Entity<Assessment>().Property(x => x.AssignedToName).HasMaxLength(160);
        builder.Entity<AssessmentQuestion>().Property(x => x.Text).HasMaxLength(500);
        builder.Entity<RiskItem>().Property(x => x.Title).HasMaxLength(240);
        builder.Entity<Incident>().Property(x => x.Title).HasMaxLength(180);
        builder.Entity<Vendor>().Property(x => x.Name).HasMaxLength(180);
        builder.Entity<Notification>().Property(x => x.UserId).HasMaxLength(64);
        builder.Entity<RefreshToken>().Property(x => x.UserId).HasMaxLength(64);
        builder.Entity<RefreshToken>().Property(x => x.TokenHash).HasMaxLength(128);

        builder.Entity<Assessment>()
            .HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<RiskItem>()
            .HasOne(x => x.Department)
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Incident>()
            .HasOne(x => x.RelatedRisk)
            .WithMany()
            .HasForeignKey(x => x.RiskItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ComplianceGap>()
            .HasOne(x => x.RelatedRisk)
            .WithMany()
            .HasForeignKey(x => x.RiskItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<RiskGuard.Domain.Common.BaseEntity>()
                     .Where(x => x.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAtUtc = now;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
