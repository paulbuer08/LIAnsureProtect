using FluentValidation;
using LIAnsureProtect.Application.Common.Behaviors;
using LIAnsureProtect.Application.Quotes.Rating;
using LIAnsureProtect.Application.Quotes.Commands.CreateQuote;
using Microsoft.Extensions.DependencyInjection;

namespace LIAnsureProtect.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            // Cache-aside for requests that opt in via ICacheableRequest; inert for everything else.
            configuration.AddOpenBehavior(typeof(CachingBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddTransient<ICyberRatingStrategy, HighRiskCyberRatingStrategy>();
        services.AddTransient<ICyberRatingStrategy, BaselineCyberRatingStrategy>();
        services.AddTransient<ICyberRatingStrategySelector, CyberRatingStrategySelector>();
        services.AddScoped<IQuoteCreationService, QuoteCreationService>();

        return services;
    }
}
