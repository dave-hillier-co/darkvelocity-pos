using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.OrdersGateway.Api.Adapters;

/// <summary>
/// Factory for creating delivery platform adapters.
/// </summary>
public interface IDeliveryPlatformAdapterFactory
{
    /// <summary>
    /// Gets the adapter for the specified platform type.
    /// </summary>
    IDeliveryPlatformAdapter? GetAdapter(string platformType);

    /// <summary>
    /// Gets all supported platform types.
    /// </summary>
    IEnumerable<string> GetSupportedPlatforms();
}

/// <summary>
/// Implementation of the delivery platform adapter factory.
/// </summary>
public class DeliveryPlatformAdapterFactory : IDeliveryPlatformAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _adapterTypes;

    public DeliveryPlatformAdapterFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _adapterTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            { "UberEats", typeof(UberEatsAdapter) },
            { "DoorDash", typeof(DoorDashAdapter) },
            { "Deliveroo", typeof(DeliverooAdapter) },
            { "JustEat", typeof(JustEatAdapter) }
        };
    }

    public IDeliveryPlatformAdapter? GetAdapter(string platformType)
    {
        if (_adapterTypes.TryGetValue(platformType, out var adapterType))
        {
            return (IDeliveryPlatformAdapter?)_serviceProvider.GetService(adapterType);
        }

        return null;
    }

    public IEnumerable<string> GetSupportedPlatforms()
    {
        return _adapterTypes.Keys;
    }
}

/// <summary>
/// Extension methods for registering delivery platform adapters.
/// </summary>
public static class DeliveryPlatformAdapterExtensions
{
    public static IServiceCollection AddDeliveryPlatformAdapters(this IServiceCollection services)
    {
        // Register HTTP clients for each adapter
        services.AddHttpClient<UberEatsAdapter>();
        services.AddHttpClient<DoorDashAdapter>();
        services.AddHttpClient<DeliverooAdapter>();
        services.AddHttpClient<JustEatAdapter>();

        // Register the factory
        services.AddSingleton<IDeliveryPlatformAdapterFactory, DeliveryPlatformAdapterFactory>();

        return services;
    }
}
