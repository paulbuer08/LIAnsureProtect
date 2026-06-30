using LIAnsureProtect.Modules.Underwriting.Domain;
using LIAnsureProtect.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Modules.Underwriting.Infrastructure.Persistence;

/// <summary>
/// The Underwriting module's own DbContext, owning the dedicated <c>underwriting</c> schema.
/// In this first slice it holds only the advisory AI review audit; later slices add referral
/// operations, evidence, and the decision audit.
/// </summary>
public sealed class UnderwritingDbContext(DbContextOptions<UnderwritingDbContext> options)
    : ModuleDbContext(options)
{
    public const string SchemaName = "underwriting";

    public DbSet<AiUnderwritingReview> AiUnderwritingReviews => Set<AiUnderwritingReview>();

    protected override string? Schema => SchemaName;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UnderwritingDbContext).Assembly);
    }
}
