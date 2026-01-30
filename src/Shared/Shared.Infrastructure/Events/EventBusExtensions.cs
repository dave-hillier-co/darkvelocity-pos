using System.Reflection;
using DarkVelocity.Shared.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Shared.Infrastructure.Events;

/// <summary>
/// Extension methods for registering event bus services.
/// </summary>
public static class EventBusExtensions
{
    /// <summary>
    /// Adds the in-memory event bus to the service collection.
    /// </summary>
    public static IServiceCollection AddInMemoryEventBus(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryEventBus>();
        services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<InMemoryEventBus>());
        return services;
    }

    /// <summary>
    /// Registers a single event handler.
    /// </summary>
    public static IServiceCollection AddEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : IIntegrationEvent
        where THandler : class, IEventHandler<TEvent>
    {
        services.AddScoped<IEventHandler<TEvent>, THandler>();
        return services;
    }

    /// <summary>
    /// Automatically discovers and registers all event handlers from the specified assembly.
    /// </summary>
    public static IServiceCollection AddEventHandlersFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                .Select(i => new { HandlerType = t, InterfaceType = i }))
            .ToList();

        foreach (var handler in handlerTypes)
        {
            services.AddScoped(handler.InterfaceType, handler.HandlerType);
        }

        return services;
    }

    /// <summary>
    /// Automatically discovers and registers all event handlers from the calling assembly.
    /// </summary>
    public static IServiceCollection AddEventHandlersFromAssembly(this IServiceCollection services)
    {
        return services.AddEventHandlersFromAssembly(Assembly.GetCallingAssembly());
    }
}
