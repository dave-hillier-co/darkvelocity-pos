using DarkVelocity.Host.Auth;
using DarkVelocity.Host.Payments;
using DarkVelocity.Host.PaymentProcessors;
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
        IConfiguration configuration,
        bool skipOAuthProviders = false)
    {
        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        services.AddSingleton(jwtSettings);
        services.AddSingleton<JwtTokenService>();

        var oauthSettings = configuration.GetSection("OAuth").Get<OAuthSettings>() ?? new OAuthSettings();
        services.AddSingleton(oauthSettings);

        var authBuilder = services.AddAuthentication(options =>
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
        });

        // Only add OAuth providers if not in testing mode and credentials are configured
        if (!skipOAuthProviders &&
            !string.IsNullOrEmpty(oauthSettings.Google.ClientId) &&
            !string.IsNullOrEmpty(oauthSettings.Microsoft.ClientId))
        {
            authBuilder
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
        }

        authBuilder.AddApiKeyAuth();

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddPaymentGatewayServices(this IServiceCollection services)
    {
        services.AddSingleton<ICardValidationService, CardValidationService>();

        // Payment processor SDK clients (stub implementations for development)
        // TODO: Replace with actual SDK implementations in production
        services.AddSingleton<IStripeClient, StubStripeClient>();
        services.AddSingleton<IAdyenClient, StubAdyenClient>();

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

            // JWT Bearer Token (for direct API access)
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            // OAuth 2.0 Authorization Code Flow (Google/Microsoft)
            options.AddSecurityDefinition("OAuth2", new OpenApiSecurityScheme
            {
                Description = "OAuth 2.0 Authorization Code flow with Google or Microsoft",
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri("/api/oauth/authorize", UriKind.Relative),
                        TokenUrl = new Uri("/api/oauth/token", UriKind.Relative),
                        Scopes = new Dictionary<string, string>
                        {
                            ["openid"] = "OpenID Connect scope",
                            ["profile"] = "Access user profile information",
                            ["email"] = "Access user email",
                            ["offline_access"] = "Request refresh tokens"
                        }
                    }
                }
            });

            // PIN Authentication (for POS devices)
            options.AddSecurityDefinition("PinAuth", new OpenApiSecurityScheme
            {
                Description = "PIN-based authentication for POS devices. Use POST /api/auth/pin with organizationId, siteId, deviceId, and pin to obtain a JWT token, or use the OAuth-style flow at /api/oauth/pin/authorize.",
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = "Authorization",
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
                },
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "OAuth2"
                        }
                    },
                    new[] { "openid", "profile", "email" }
                }
            });
        });

        return services;
    }
}
