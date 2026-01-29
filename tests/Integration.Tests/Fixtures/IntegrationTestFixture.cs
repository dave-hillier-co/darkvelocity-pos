namespace DarkVelocity.Integration.Tests.Fixtures;

/// <summary>
/// Coordinates multiple service fixtures for comprehensive integration testing.
/// Ensures shared IDs are consistent across all service boundaries.
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    // Shared test data IDs - consistent across all services
    public Guid TestLocationId { get; } = Guid.NewGuid();
    public Guid TestUserId { get; } = Guid.NewGuid();
    public Guid ManagerUserId { get; } = Guid.NewGuid();
    public Guid TestMenuItemId { get; } = Guid.NewGuid();
    public Guid TestMenuItemId2 { get; } = Guid.NewGuid();

    // Service fixtures
    public OrdersServiceFixture Orders { get; private set; } = null!;
    public PaymentsServiceFixture Payments { get; private set; } = null!;
    public InventoryServiceFixture Inventory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Initialize Orders service fixture
        Orders = new OrdersServiceFixture
        {
            TestLocationId = TestLocationId,
            TestUserId = TestUserId,
            TestMenuItemId = TestMenuItemId,
            TestMenuItemId2 = TestMenuItemId2
        };
        await Orders.InitializeAsync();

        // Initialize Payments service fixture
        Payments = new PaymentsServiceFixture
        {
            TestLocationId = TestLocationId,
            TestUserId = TestUserId,
            TestOrderId = Orders.TestOrderId
        };
        await Payments.InitializeAsync();

        // Initialize Inventory service fixture
        Inventory = new InventoryServiceFixture
        {
            TestLocationId = TestLocationId,
            TestMenuItemId = TestMenuItemId,
            TestMenuItemId2 = TestMenuItemId2
        };
        await Inventory.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (Orders != null) await Orders.DisposeAsync();
        if (Payments != null) await Payments.DisposeAsync();
        if (Inventory != null) await Inventory.DisposeAsync();
    }
}
