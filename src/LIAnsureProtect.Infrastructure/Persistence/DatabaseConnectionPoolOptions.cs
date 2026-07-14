namespace LIAnsureProtect.Infrastructure.Persistence;

public sealed class DatabaseConnectionPoolOptions
{
    public const string SectionName = "Database:ConnectionPool";

    public int MinimumPoolSize { get; set; }

    public int MaximumPoolSize { get; set; } = 40;

    public int ConnectionTimeoutSeconds { get; set; } = 15;

    public int CommandTimeoutSeconds { get; set; } = 30;

    public int IdleLifetimeSeconds { get; set; } = 300;

    public int PruningIntervalSeconds { get; set; } = 10;

    public int ConnectionLifetimeSeconds { get; set; } = 3600;
}
