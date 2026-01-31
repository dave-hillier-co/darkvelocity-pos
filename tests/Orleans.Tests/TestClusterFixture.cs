using DarkVelocity.Orleans.Abstractions.Streams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.TestingHost;

namespace DarkVelocity.Orleans.Tests;

public class TestClusterFixture : IDisposable
{
    public TestCluster Cluster { get; }

    public TestClusterFixture()
    {
        var builder = new TestClusterBuilder();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurator>();
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
        siloBuilder.AddMemoryGrainStorage("OrleansStorage");

        // Add memory stream provider for pub/sub testing
        siloBuilder.AddMemoryStreams(StreamConstants.DefaultStreamProvider);

        siloBuilder.Services.AddSingleton<IGrainFactory>(sp => sp.GetRequiredService<IGrainFactory>());
        siloBuilder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
    }
}

[CollectionDefinition(Name)]
public class ClusterCollection : ICollectionFixture<TestClusterFixture>
{
    public const string Name = "ClusterCollection";
}
