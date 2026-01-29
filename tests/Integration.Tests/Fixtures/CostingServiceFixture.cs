using DarkVelocity.Costing.Api.Data;
using DarkVelocity.Costing.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Integration.Tests.Fixtures;

public class CostingServiceFixture : WebApplicationFactory<DarkVelocity.Costing.Api.Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Shared test IDs (set by IntegrationTestFixture)
    public Guid TestLocationId { get; set; }

    // Ingredient IDs (shared across procurement/costing)
    public Guid BeefIngredientId { get; set; }
    public Guid CheeseIngredientId { get; set; }
    public Guid BunsIngredientId { get; set; }
    public Guid TomatoesIngredientId { get; set; }

    // Menu item IDs (external references)
    public Guid BurgerMenuItemId { get; set; }
    public Guid PastaMenuItemId { get; set; }

    // Pre-created test data IDs
    public Guid BurgerRecipeId { get; private set; }
    public Guid PastaRecipeId { get; private set; }
    public Guid BeefPriceId { get; private set; }
    public Guid CheesePriceId { get; private set; }
    public Guid BunsPriceId { get; private set; }
    public Guid TestSnapshotId { get; private set; }
    public Guid OldSnapshotId { get; private set; }
    public Guid UnacknowledgedAlertId { get; private set; }
    public Guid AcknowledgedAlertId { get; private set; }
    public Guid CostingSettingsId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<CostingDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CostingDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create ingredient prices
        var beefPrice = new IngredientPrice
        {
            IngredientId = BeefIngredientId,
            IngredientName = "Ground Beef",
            CurrentPrice = 50.00m, // $50 per 5kg pack
            UnitOfMeasure = "kg",
            PackSize = 5m,
            PricePerUnit = 10.00m, // $10/kg
            PreferredSupplierId = Guid.NewGuid(),
            PreferredSupplierName = "Fresh Foods Ltd"
        };
        db.IngredientPrices.Add(beefPrice);
        BeefPriceId = beefPrice.Id;

        var cheesePrice = new IngredientPrice
        {
            IngredientId = CheeseIngredientId,
            IngredientName = "Cheddar Cheese",
            CurrentPrice = 20.00m,
            UnitOfMeasure = "kg",
            PackSize = 2m,
            PricePerUnit = 10.00m,
            PreviousPrice = 18.00m,
            PriceChangedAt = DateTime.UtcNow.AddDays(-7),
            PriceChangePercent = 11.11m
        };
        db.IngredientPrices.Add(cheesePrice);
        CheesePriceId = cheesePrice.Id;

        var bunsPrice = new IngredientPrice
        {
            IngredientId = BunsIngredientId,
            IngredientName = "Burger Buns",
            CurrentPrice = 12.00m,
            UnitOfMeasure = "unit",
            PackSize = 24m,
            PricePerUnit = 0.50m
        };
        db.IngredientPrices.Add(bunsPrice);
        BunsPriceId = bunsPrice.Id;

        await db.SaveChangesAsync();

        // Create burger recipe
        var burgerRecipe = new Recipe
        {
            MenuItemId = BurgerMenuItemId,
            MenuItemName = "Classic Cheeseburger",
            Code = "RCP-BURGER-001",
            CategoryId = Guid.NewGuid(),
            CategoryName = "Mains",
            Description = "Classic beef cheeseburger with all the fixings",
            PortionYield = 1,
            PrepInstructions = "Grill patty to desired doneness",
            CurrentCostPerPortion = 3.75m,
            CostCalculatedAt = DateTime.UtcNow
        };
        db.Recipes.Add(burgerRecipe);
        BurgerRecipeId = burgerRecipe.Id;

        await db.SaveChangesAsync();

        // Add ingredients to burger recipe
        var beefIngredient = new RecipeIngredient
        {
            RecipeId = BurgerRecipeId,
            IngredientId = BeefIngredientId,
            IngredientName = "Ground Beef",
            Quantity = 0.15m, // 150g
            UnitOfMeasure = "kg",
            WastePercentage = 5m,
            CurrentUnitCost = 10.00m,
            CurrentLineCost = 1.575m // 0.15 * 1.05 * 10
        };
        db.RecipeIngredients.Add(beefIngredient);

        var bunIngredient = new RecipeIngredient
        {
            RecipeId = BurgerRecipeId,
            IngredientId = BunsIngredientId,
            IngredientName = "Burger Bun",
            Quantity = 1m,
            UnitOfMeasure = "unit",
            WastePercentage = 0m,
            CurrentUnitCost = 0.50m,
            CurrentLineCost = 0.50m
        };
        db.RecipeIngredients.Add(bunIngredient);

        var cheeseIngredient = new RecipeIngredient
        {
            RecipeId = BurgerRecipeId,
            IngredientId = CheeseIngredientId,
            IngredientName = "Cheddar Cheese",
            Quantity = 0.03m, // 30g
            UnitOfMeasure = "kg",
            WastePercentage = 10m,
            CurrentUnitCost = 10.00m,
            CurrentLineCost = 0.33m // 0.03 * 1.1 * 10
        };
        db.RecipeIngredients.Add(cheeseIngredient);

        await db.SaveChangesAsync();

        // Create pasta recipe (multi-portion)
        var pastaRecipe = new Recipe
        {
            MenuItemId = PastaMenuItemId,
            MenuItemName = "Spaghetti Bolognese",
            Code = "RCP-PASTA-001",
            CategoryId = Guid.NewGuid(),
            CategoryName = "Mains",
            Description = "Classic Italian pasta with meat sauce",
            PortionYield = 4, // Recipe makes 4 portions
            CurrentCostPerPortion = 2.50m,
            CostCalculatedAt = DateTime.UtcNow
        };
        db.Recipes.Add(pastaRecipe);
        PastaRecipeId = pastaRecipe.Id;

        await db.SaveChangesAsync();

        // Create historical cost snapshots for burger (for trend analysis)
        var oldSnapshot = new RecipeCostSnapshot
        {
            RecipeId = BurgerRecipeId,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            TotalIngredientCost = 3.25m,
            CostPerPortion = 3.25m,
            PortionYield = 1,
            MenuPrice = 12.00m,
            CostPercentage = 27.08m,
            GrossMarginPercent = 72.92m,
            SnapshotReason = "weekly"
        };
        db.RecipeCostSnapshots.Add(oldSnapshot);
        OldSnapshotId = oldSnapshot.Id;

        var midSnapshot = new RecipeCostSnapshot
        {
            RecipeId = BurgerRecipeId,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)),
            TotalIngredientCost = 3.50m,
            CostPerPortion = 3.50m,
            PortionYield = 1,
            MenuPrice = 12.00m,
            CostPercentage = 29.17m,
            GrossMarginPercent = 70.83m,
            SnapshotReason = "weekly"
        };
        db.RecipeCostSnapshots.Add(midSnapshot);

        var recentSnapshot = new RecipeCostSnapshot
        {
            RecipeId = BurgerRecipeId,
            SnapshotDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
            TotalIngredientCost = 3.75m,
            CostPerPortion = 3.75m,
            PortionYield = 1,
            MenuPrice = 12.00m,
            CostPercentage = 31.25m,
            GrossMarginPercent = 68.75m,
            SnapshotReason = "price_change"
        };
        db.RecipeCostSnapshots.Add(recentSnapshot);
        TestSnapshotId = recentSnapshot.Id;

        await db.SaveChangesAsync();

        // Create cost alerts
        var unacknowledgedAlert = new CostAlert
        {
            AlertType = "cost_increase",
            RecipeId = BurgerRecipeId,
            RecipeName = "Classic Cheeseburger",
            IngredientId = CheeseIngredientId,
            IngredientName = "Cheddar Cheese",
            PreviousValue = 18.00m,
            CurrentValue = 20.00m,
            ChangePercent = 11.11m,
            ThresholdValue = 5.00m,
            ImpactDescription = "Cheese price increased by 11.11%, affecting recipe cost",
            AffectedRecipeCount = 3
        };
        db.CostAlerts.Add(unacknowledgedAlert);
        UnacknowledgedAlertId = unacknowledgedAlert.Id;

        var acknowledgedAlert = new CostAlert
        {
            AlertType = "margin_warning",
            RecipeId = PastaRecipeId,
            RecipeName = "Spaghetti Bolognese",
            PreviousValue = 72.00m,
            CurrentValue = 58.00m,
            ChangePercent = -19.44m,
            ThresholdValue = 60.00m,
            ImpactDescription = "Margin dropped below warning threshold",
            IsAcknowledged = true,
            AcknowledgedAt = DateTime.UtcNow.AddDays(-1),
            Notes = "Price increase scheduled for next menu update",
            ActionTaken = "will_adjust_price"
        };
        db.CostAlerts.Add(acknowledgedAlert);
        AcknowledgedAlertId = acknowledgedAlert.Id;

        await db.SaveChangesAsync();

        // Create costing settings
        var settings = new CostingSettings
        {
            LocationId = TestLocationId,
            TargetFoodCostPercent = 30m,
            TargetBeverageCostPercent = 25m,
            MinimumMarginPercent = 50m,
            WarningMarginPercent = 60m,
            PriceChangeAlertThreshold = 10m,
            CostIncreaseAlertThreshold = 5m,
            AutoRecalculateCosts = true,
            AutoCreateSnapshots = true,
            SnapshotFrequencyDays = 7
        };
        db.CostingSettings.Add(settings);
        CostingSettingsId = settings.Id;

        await db.SaveChangesAsync();
    }

    public new async Task DisposeAsync()
    {
        Client?.Dispose();
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }

    public CostingDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<CostingDbContext>();
    }
}
