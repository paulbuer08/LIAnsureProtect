using LIAnsureProtect.Domain.Submissions;
using LIAnsureProtect.Infrastructure;
using LIAnsureProtect.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.IntegrationTests;

public sealed class PostgreSqlPersistenceTests
{
    private const string EnabledEnvironmentVariableName = "LIANSUREPROTECT_RUN_POSTGRES_TESTS";
    private const string ConnectionStringEnvironmentVariableName = "LIANSUREPROTECT_TEST_POSTGRES_CONNECTION_STRING";
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=liansureprotect;Username=postgres;Password=postgres";

    [Fact]
    public async Task PostgreSql_Database_Has_Pgvector_And_Persists_Submission()
    {
        Assert.SkipUnless(PostgreSqlTestsAreEnabled(), $"Set {EnabledEnvironmentVariableName}=true to run PostgreSQL-backed integration tests.");

        var services = new ServiceCollection();
        services.AddInfrastructure(GetConnectionString());

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SubmissionDbContext>();

        var hasVectorExtension = await HasVectorExtensionAsync(dbContext, TestContext.Current.CancellationToken);
        Assert.True(hasVectorExtension);

        var createdAtUtc = DateTime.UtcNow;
        var submission = Submission.CreateDraft(
            "PostgreSQL Applicant",
            "postgresql-applicant@example.com",
            $"PostgreSQL Company {Guid.NewGuid():N}",
            createdAtUtc);

        await dbContext.Submissions.AddAsync(submission, TestContext.Current.CancellationToken);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var savedSubmission = await dbContext.Submissions.SingleAsync(
            saved => saved.Id == submission.Id,
            TestContext.Current.CancellationToken);

        Assert.Equal(submission.Id, savedSubmission.Id);
        Assert.Equal("PostgreSQL Applicant", savedSubmission.ApplicantName);
        Assert.Equal("postgresql-applicant@example.com", savedSubmission.ApplicantEmail);
        Assert.Equal(SubmissionStatus.Draft, savedSubmission.Status);
    }

    private static bool PostgreSqlTestsAreEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(EnabledEnvironmentVariableName),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string GetConnectionString()
    {
        return Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariableName)
            ?? DefaultConnectionString;
    }

    private static async Task<bool> HasVectorExtensionAsync(
        SubmissionDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = "select exists (select 1 from pg_extension where extname = 'vector');";

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return result is true;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
