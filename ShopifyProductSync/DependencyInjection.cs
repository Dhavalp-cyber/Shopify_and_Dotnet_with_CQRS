using FluentValidation;
using MediatR;
using ShopifyProductSync.CQRS.Common.Behaviors;

namespace ShopifyProductSync
{
    /// <summary>
    /// Extension method to register all application-layer services:
    /// MediatR, FluentValidation validators, and pipeline behaviors.
    /// Call builder.Services.AddApplicationServices() in Program.cs.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Register MediatR — scans this assembly for all IRequestHandler implementations
            services.AddMediatR(cfg =>
                cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

            // Register all FluentValidation validators from this assembly
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

            // Register pipeline behaviors (order matters: Logging runs first, then Validation)
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            return services;
        }
    }
}
