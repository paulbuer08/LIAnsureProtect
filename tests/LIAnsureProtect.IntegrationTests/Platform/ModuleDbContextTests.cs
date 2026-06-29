using LIAnsureProtect.Platform.Abstractions.DomainEvents;
using LIAnsureProtect.Platform.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LIAnsureProtect.IntegrationTests.Platform;

/// <summary>
/// Proves the reusable <see cref="ModuleDbContext"/> base behaves like the existing
/// <c>SubmissionDbContext</c>: it applies the module's default schema, captures domain events on
/// save (in the same transaction), and clears them afterwards. This is the template the first real
/// module DbContext inherits in Milestone 33.
/// </summary>
public sealed class ModuleDbContextTests
{
    [Fact]
    public void AppliesTheModuleDefaultSchema()
    {
        using var context = CreateContext(out var connection);
        using (connection)
        {
            Assert.Equal("test_module", context.Model.GetDefaultSchema());
        }
    }

    [Fact]
    public async Task CapturesAndClearsDomainEventsOnSave()
    {
        using var context = CreateContext(out var connection);
        using (connection)
        {
            context.Database.EnsureCreated();

            var entity = new TestEntity { Name = "first" };
            entity.Raise(new TestDomainEvent(DateTime.UtcNow));
            context.Items.Add(entity);

            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            // The base handed the event to the capture hook (the outbox seam) ...
            Assert.Single(context.CapturedEvents);
            // ... and cleared the aggregate's events after the successful save.
            Assert.Empty(entity.DomainEvents);
        }
    }

    private static TestModuleDbContext CreateContext(out SqliteConnection connection)
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TestModuleDbContext>()
            .UseSqlite(connection)
            .Options;
        return new TestModuleDbContext(options);
    }

    private sealed record TestDomainEvent(DateTime OccurredAtUtc) : IDomainEvent;

    private sealed class TestEntity : IHasDomainEvents
    {
        private readonly List<IDomainEvent> _domainEvents = [];

        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

        public void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

        public void ClearDomainEvents() => _domainEvents.Clear();
    }

    private sealed class TestModuleDbContext(DbContextOptions<TestModuleDbContext> options)
        : ModuleDbContext(options)
    {
        public DbSet<TestEntity> Items => Set<TestEntity>();

        public List<IDomainEvent> CapturedEvents { get; } = [];

        protected override string? Schema => "test_module";

        protected override Task CaptureDomainEventsAsync(
            IReadOnlyCollection<IDomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            CapturedEvents.AddRange(domainEvents);
            return Task.CompletedTask;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestEntity>().ToTable("items");
        }
    }
}
