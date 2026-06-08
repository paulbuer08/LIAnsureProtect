using LIAnsureProtect.Application.Submissions;
using LIAnsureProtect.Infrastructure.Submissions;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.Infrastructure;


public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISubmissionRepository, InMemorySubmissionRepository>();

        return services;
    }
}
