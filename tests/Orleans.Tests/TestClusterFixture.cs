using Microsoft.Extensions.DependencyInjection;
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
        siloBuilder.Services.AddSingleton<IGrainFactory>(sp => sp.GetRequiredService<IGrainFactory>());
    }
}

[CollectionDefinition(Name)]
public class ClusterCollection : ICollectionFixture<TestClusterFixture>
{
    public const string Name = "ClusterCollection";
}
