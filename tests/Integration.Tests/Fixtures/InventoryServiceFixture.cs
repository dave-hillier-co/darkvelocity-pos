using DarkVelocity.Inventory.Api.Data;
using DarkVelocity.Inventory.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Integration.Tests.Fixtures;

/// <summary>
/// Fixture for the Inventory service with comprehensive test data for integration testing.
/// </summary>
public class InventoryServiceFixture : WebApplicationFactory<DarkVelocity.Inventory.Api.Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Shared test data IDs (coordinated across services)
    public Guid TestLocationId { get; set; }
    public Guid TestMenuItemId { get; set; }
    public Guid TestMenuItemId2 { get; set; }

    // Service-specific test data - Ingredients
    public Guid BeefIngredientId { get; private set; }
    public Guid BunsIngredientId { get; private set; }
    public Guid CheeseIngredientId { get; private set; }
    public Guid PotatoesIngredientId { get; private set; }
    public Guid OilIngredientId { get; private set; }

    // Recipes
    public Guid BurgerRecipeId { get; private set; }
    public Guid FriesRecipeId { get; private set; }

    // Stock batches
    public Guid BeefBatch1Id { get; private set; }
    public Guid BeefBatch2Id { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<InventoryDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Initialize shared IDs if not set
        if (TestLocationId == Guid.Empty) TestLocationId = Guid.NewGuid();
        if (TestMenuItemId == Guid.Empty) TestMenuItemId = Guid.NewGuid();
        if (TestMenuItemId2 == Guid.Empty) TestMenuItemId2 = Guid.NewGuid();

        // Create ingredients for burger
        var beef = new Ingredient
        {
            Code = "BEEF-PATTY",
            Name = "Beef Patty",
            UnitOfMeasure = "unit",
            Category = "proteins",
            StorageType = "chilled",
            ReorderLevel = 20m,
            ReorderQuantity = 50m,
            CurrentStock = 100m,
            IsTracked = true
        };
        db.Ingredients.Add(beef);
        BeefIngredientId = beef.Id;

        var buns = new Ingredient
        {
            Code = "BURGER-BUN",
            Name = "Burger Bun",
            UnitOfMeasure = "unit",
            Category = "bakery",
            StorageType = "ambient",
            ReorderLevel = 30m,
            ReorderQuantity = 100m,
            CurrentStock = 200m,
            IsTracked = true
        };
        db.Ingredients.Add(buns);
        BunsIngredientId = buns.Id;

        var cheese = new Ingredient
        {
            Code = "CHEESE-SLICE",
            Name = "Cheese Slice",
            UnitOfMeasure = "unit",
            Category = "dairy",
            StorageType = "chilled",
            ReorderLevel = 50m,
            ReorderQuantity = 200m,
            CurrentStock = 300m,
            IsTracked = true
        };
        db.Ingredients.Add(cheese);
        CheeseIngredientId = cheese.Id;

        // Create ingredients for fries
        var potatoes = new Ingredient
        {
            Code = "POTATO-FRIES",
            Name = "Pre-cut Fries",
            UnitOfMeasure = "kg",
            Category = "vegetables",
            StorageType = "frozen",
            ReorderLevel = 10m,
            ReorderQuantity = 30m,
            CurrentStock = 50m,
            IsTracked = true
        };
        db.Ingredients.Add(potatoes);
        PotatoesIngredientId = potatoes.Id;

        var oil = new Ingredient
        {
            Code = "FRYING-OIL",
            Name = "Frying Oil",
            UnitOfMeasure = "litre",
            Category = "oils",
            StorageType = "ambient",
            ReorderLevel = 5m,
            ReorderQuantity = 20m,
            CurrentStock = 25m,
            IsTracked = true
        };
        db.Ingredients.Add(oil);
        OilIngredientId = oil.Id;

        await db.SaveChangesAsync();

        // Create stock batches for beef (FIFO testing)
        var beefBatch1 = new StockBatch
        {
            IngredientId = BeefIngredientId,
            LocationId = TestLocationId,
            InitialQuantity = 50m,
            RemainingQuantity = 50m,
            UnitCost = 2.00m,
            ReceivedAt = DateTime.UtcNow.AddDays(-14),
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            BatchNumber = "BEEF-2026-001",
            Status = "active"
        };
        db.StockBatches.Add(beefBatch1);
        BeefBatch1Id = beefBatch1.Id;

        var beefBatch2 = new StockBatch
        {
            IngredientId = BeefIngredientId,
            LocationId = TestLocationId,
            InitialQuantity = 50m,
            RemainingQuantity = 50m,
            UnitCost = 2.50m,
            ReceivedAt = DateTime.UtcNow.AddDays(-7),
            ExpiryDate = DateTime.UtcNow.AddDays(14),
            BatchNumber = "BEEF-2026-002",
            Status = "active"
        };
        db.StockBatches.Add(beefBatch2);
        BeefBatch2Id = beefBatch2.Id;

        // Create stock batches for other ingredients
        var bunsBatch = new StockBatch
        {
            IngredientId = BunsIngredientId,
            LocationId = TestLocationId,
            InitialQuantity = 200m,
            RemainingQuantity = 200m,
            UnitCost = 0.30m,
            ReceivedAt = DateTime.UtcNow.AddDays(-3),
            BatchNumber = "BUNS-2026-001",
            Status = "active"
        };
        db.StockBatches.Add(bunsBatch);

        var cheeseBatch = new StockBatch
        {
            IngredientId = CheeseIngredientId,
            LocationId = TestLocationId,
            InitialQuantity = 300m,
            RemainingQuantity = 300m,
            UnitCost = 0.15m,
            ReceivedAt = DateTime.UtcNow.AddDays(-5),
            BatchNumber = "CHEESE-2026-001",
            Status = "active"
        };
        db.StockBatches.Add(cheeseBatch);

        var potatoBatch = new StockBatch
        {
            IngredientId = PotatoesIngredientId,
            LocationId = TestLocationId,
            InitialQuantity = 50m,
            RemainingQuantity = 50m,
            UnitCost = 1.50m,
            ReceivedAt = DateTime.UtcNow.AddDays(-2),
            BatchNumber = "FRIES-2026-001",
            Status = "active"
        };
        db.StockBatches.Add(potatoBatch);

        await db.SaveChangesAsync();

        // Create recipes
        var burgerRecipe = new Recipe
        {
            Code = "RCP-BURGER-001",
            Name = "Classic Cheeseburger",
            MenuItemId = TestMenuItemId,
            PortionYield = 1,
            IsActive = true
        };
        db.Recipes.Add(burgerRecipe);
        BurgerRecipeId = burgerRecipe.Id;

        var friesRecipe = new Recipe
        {
            Code = "RCP-FRIES-001",
            Name = "French Fries",
            MenuItemId = TestMenuItemId2,
            PortionYield = 1,
            IsActive = true
        };
        db.Recipes.Add(friesRecipe);
        FriesRecipeId = friesRecipe.Id;

        await db.SaveChangesAsync();

        // Add ingredients to burger recipe
        db.RecipeIngredients.Add(new RecipeIngredient
        {
            RecipeId = BurgerRecipeId,
            IngredientId = BeefIngredientId,
            Quantity = 1m,
            WastePercentage = 0m
        });
        db.RecipeIngredients.Add(new RecipeIngredient
        {
            RecipeId = BurgerRecipeId,
            IngredientId = BunsIngredientId,
            Quantity = 1m,
            WastePercentage = 0m
        });
        db.RecipeIngredients.Add(new RecipeIngredient
        {
            RecipeId = BurgerRecipeId,
            IngredientId = CheeseIngredientId,
            Quantity = 1m,
            WastePercentage = 0m
        });

        // Add ingredients to fries recipe
        db.RecipeIngredients.Add(new RecipeIngredient
        {
            RecipeId = FriesRecipeId,
            IngredientId = PotatoesIngredientId,
            Quantity = 0.15m,
            WastePercentage = 5m
        });
        db.RecipeIngredients.Add(new RecipeIngredient
        {
            RecipeId = FriesRecipeId,
            IngredientId = OilIngredientId,
            Quantity = 0.05m,
            WastePercentage = 0m
        });

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

    public InventoryDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
    }
}
