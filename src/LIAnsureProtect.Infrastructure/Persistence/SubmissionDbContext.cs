using LIAnsureProtect.Domain.Submissions;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.Infrastructure.Persistence;

public sealed class SubmissionDbContext(DbContextOptions<SubmissionDbContext> options) : DbContext(options)
{
    public DbSet<Submission> Submissions => Set<Submission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SubmissionDbContext).Assembly);
    }
}
