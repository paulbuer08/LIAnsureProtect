using FluentValidation;
using LIAnsureProtect.Modules.Quoting.Application;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.Modules.Quoting.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Quoting module boundary. M39 introduces the Application boundary before moving
    /// quote tables, so this composition root scans handlers and validators but does not register a
    /// Quoting DbContext yet.
    /// </summary>
    public static IServiceCollection AddQuotingModule(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly));

        services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);

        return services;
    }
}
