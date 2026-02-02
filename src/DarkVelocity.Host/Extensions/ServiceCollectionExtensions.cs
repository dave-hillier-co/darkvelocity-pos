using DarkVelocity.Host.Auth;
using DarkVelocity.Host.Search;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

namespace DarkVelocity.Host.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOrleansConfiguration(this IHostBuilder hostBuilder)
    {
        // Orleans is configured on the HostBuilder, not IServiceCollection
        // This is handled separately in Program.cs
        return null!;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        services.AddSingleton(jwtSettings);
        services.AddSingleton<JwtTokenService>();

        var oauthSettings = configuration.GetSection("OAuth").Get<OAuthSettings>() ?? new OAuthSettings();
        services.AddSingleton(oauthSettings);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            var tokenService = new JwtTokenService(jwtSettings);
            options.TokenValidationParameters = tokenService.GetValidationParameters();
        })
        .AddCookie(options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        })
        .AddGoogle(options =>
        {
            options.ClientId = oauthSettings.Google.ClientId;
            options.ClientSecret = oauthSettings.Google.ClientSecret;
            options.CallbackPath = "/signin-google";
            options.SaveTokens = true;
        })
        .AddMicrosoftAccount(options =>
        {
            options.ClientId = oauthSettings.Microsoft.ClientId;
            options.ClientSecret = oauthSettings.Microsoft.ClientSecret;
            options.CallbackPath = "/signin-microsoft";
            options.SaveTokens = true;
        });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(
                        "http://localhost:5173",  // POS dev
                        "http://localhost:5174",  // Back Office dev
                        "http://localhost:5175"   // KDS dev
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    public static IServiceCollection AddSearchServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var searchConnectionString = configuration.GetConnectionString("Search")
            ?? "Host=localhost;Database=darkvelocity_search;Username=darkvelocity;Password=darkvelocity_dev";

        services.AddDbContextFactory<SearchDbContext>(options =>
            options.UseNpgsql(searchConnectionString));
        services.AddScoped<ISearchService, PostgresSearchService>();
        services.AddScoped<ISearchIndexer, PostgresSearchService>();

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "DarkVelocity POS API",
                Version = "v1",
                Description = "Point of Sale system API with Orleans backend",
                Contact = new OpenApiContact
                {
                    Name = "DarkVelocity",
                    Url = new Uri("https://github.com/dave-hillier-co/darkvelocity-pos")
                }
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}
