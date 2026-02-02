using DarkVelocity.Host.Payments;
using DarkVelocity.Host.Streams;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.TestingHost;

namespace DarkVelocity.Tests;

public class TestClusterFixture : IDisposable
{
    public TestCluster Cluster { get; }

    public TestClusterFixture()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
        builder.AddClientBuilderConfigurator<TestClientConfigurator>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster.StopAllSilos();
    }
}

public class TestSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorageAsDefault();
        siloBuilder.AddMemoryGrainStorage("OrleansStorage");
        siloBuilder.AddMemoryGrainStorage("PubSubStore");

        // Add memory stream provider for pub/sub testing with implicit subscription support
        siloBuilder.AddMemoryStreams(StreamConstants.DefaultStreamProvider);

        // Add payment gateway services
        siloBuilder.Services.AddSingleton<ICardValidationService, CardValidationService>();

        siloBuilder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
    }
}

public class TestClientConfigurator : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        clientBuilder.AddMemoryStreams(StreamConstants.DefaultStreamProvider);
    }
}

[CollectionDefinition(Name)]
public class ClusterCollection : ICollectionFixture<TestClusterFixture>
{
    public const string Name = "ClusterCollection";
}
