namespace DarkVelocity.Host.Authorization;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds SpiceDB authorization services to the service collection.
    /// </summary>
    public static IServiceCollection AddSpiceDbAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = new SpiceDbSettings();
        configuration.GetSection("SpiceDb").Bind(settings);

        services.AddSingleton(settings);
        services.AddSingleton<ISpiceDbClient, SpiceDbClient>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<SpiceDbAuthorizationFilter>();

        return services;
    }

    /// <summary>
    /// Adds SpiceDB authorization services with explicit settings.
    /// </summary>
    public static IServiceCollection AddSpiceDbAuthorization(
        this IServiceCollection services,
        Action<SpiceDbSettings> configureSettings)
    {
        var settings = new SpiceDbSettings();
        configureSettings(settings);

        services.AddSingleton(settings);
        services.AddSingleton<ISpiceDbClient, SpiceDbClient>();
        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<SpiceDbAuthorizationFilter>();

        return services;
    }
}
