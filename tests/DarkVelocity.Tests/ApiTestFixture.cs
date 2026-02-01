using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DarkVelocity.Tests;

public class ApiTestFixture : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Reduce logging noise during tests
            services.AddLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
            });
        });
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiTestFixture>
{
    public const string Name = "ApiCollection";
}

public static class HttpClientExtensions
{
    public static async Task<T?> GetFromJsonAsync<T>(this HttpClient client, string requestUri)
    {
        var response = await client.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
