using DarkVelocity.Host.Payments;
using DarkVelocity.Host.PaymentProcessors;
using DarkVelocity.Host.Services;
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
        siloBuilder.AddMemoryGrainStorage("purchases");

        // Log consistency provider for JournaledGrain event sourcing
        siloBuilder.AddLogStorageBasedLogConsistencyProvider("LogStorage");
        siloBuilder.AddMemoryGrainStorage("LogStorage");

        // Add memory stream provider for pub/sub testing with implicit subscription support
        siloBuilder.AddMemoryStreams(StreamConstants.DefaultStreamProvider);

        // Add payment gateway services
        siloBuilder.Services.AddSingleton<ICardValidationService, CardValidationService>();

        // Add payment processor SDK clients (stub implementations for testing)
        siloBuilder.Services.AddSingleton<IStripeClient, StubStripeClient>();
        siloBuilder.Services.AddSingleton<IAdyenClient, StubAdyenClient>();

        // Add other required services
        siloBuilder.Services.AddSingleton<IFuzzyMatchingService, FuzzyMatchingService>();
        siloBuilder.Services.AddSingleton<IDocumentIntelligenceService, StubDocumentIntelligenceService>();
        siloBuilder.Services.AddSingleton<IEmailIngestionService, StubEmailIngestionService>();

        // Add notification services
        siloBuilder.Services.AddSingleton<IEmailService, StubEmailService>();
        siloBuilder.Services.AddSingleton<ISmsService, StubSmsService>();
        siloBuilder.Services.AddSingleton<IPushService, StubPushService>();
        siloBuilder.Services.AddSingleton<ISlackService, StubSlackService>();

        // Add webhook delivery service
        siloBuilder.Services.AddSingleton<IWebhookDeliveryService, StubWebhookDeliveryService>();

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
