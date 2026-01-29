using DarkVelocity.Menu.Api.Data;
using DarkVelocity.Menu.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Integration.Tests.Fixtures;

public class MenuServiceFixture : WebApplicationFactory<DarkVelocity.Menu.Api.Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Shared test IDs (set by IntegrationTestFixture)
    public Guid TestLocationId { get; set; }
    public Guid SecondLocationId { get; set; }

    // Pre-created test data IDs
    public Guid FoodAccountingGroupId { get; private set; }
    public Guid BeverageAccountingGroupId { get; private set; }
    public Guid MainCategoryId { get; private set; }
    public Guid DrinksCategoryId { get; private set; }
    public Guid BurgerItemId { get; private set; }
    public Guid PastaItemId { get; private set; }
    public Guid SodaItemId { get; private set; }
    public Guid DefaultMenuId { get; private set; }
    public Guid SecondaryMenuId { get; private set; }
    public Guid TestScreenId { get; private set; }
    public Guid TestButtonId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<MenuDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MenuDbContext>();

        await db.Database.EnsureCreatedAsync();

        // Create accounting groups
        var foodGroup = new AccountingGroup
        {
            Name = "Food",
            Description = "Food items with standard VAT",
            TaxRate = 0.20m
        };
        db.AccountingGroups.Add(foodGroup);
        FoodAccountingGroupId = foodGroup.Id;

        var beverageGroup = new AccountingGroup
        {
            Name = "Beverages",
            Description = "Drink items with standard VAT",
            TaxRate = 0.20m
        };
        db.AccountingGroups.Add(beverageGroup);
        BeverageAccountingGroupId = beverageGroup.Id;

        await db.SaveChangesAsync();

        // Create categories for primary location
        var mainCategory = new MenuCategory
        {
            LocationId = TestLocationId,
            Name = "Main Courses",
            Description = "Main course dishes",
            DisplayOrder = 1,
            Color = "#FF6600"
        };
        db.Categories.Add(mainCategory);
        MainCategoryId = mainCategory.Id;

        var drinksCategory = new MenuCategory
        {
            LocationId = TestLocationId,
            Name = "Drinks",
            Description = "Beverages and soft drinks",
            DisplayOrder = 2,
            Color = "#0066FF"
        };
        db.Categories.Add(drinksCategory);
        DrinksCategoryId = drinksCategory.Id;

        await db.SaveChangesAsync();

        // Create menu items
        var burgerItem = new MenuItem
        {
            LocationId = TestLocationId,
            CategoryId = MainCategoryId,
            AccountingGroupId = FoodAccountingGroupId,
            Name = "Classic Burger",
            Description = "Beef patty with all the fixings",
            Price = 12.50m,
            Sku = "BURGER-001",
            TrackInventory = true
        };
        db.Items.Add(burgerItem);
        BurgerItemId = burgerItem.Id;

        var pastaItem = new MenuItem
        {
            LocationId = TestLocationId,
            CategoryId = MainCategoryId,
            AccountingGroupId = FoodAccountingGroupId,
            Name = "Spaghetti Bolognese",
            Description = "Classic Italian pasta with meat sauce",
            Price = 11.00m,
            Sku = "PASTA-001",
            TrackInventory = true
        };
        db.Items.Add(pastaItem);
        PastaItemId = pastaItem.Id;

        var sodaItem = new MenuItem
        {
            LocationId = TestLocationId,
            CategoryId = DrinksCategoryId,
            AccountingGroupId = BeverageAccountingGroupId,
            Name = "Cola",
            Description = "Refreshing cola drink",
            Price = 2.50m,
            Sku = "DRINK-001",
            TrackInventory = false
        };
        db.Items.Add(sodaItem);
        SodaItemId = sodaItem.Id;

        await db.SaveChangesAsync();

        // Create default menu
        var defaultMenu = new MenuDefinition
        {
            LocationId = TestLocationId,
            Name = "Main Menu",
            Description = "The default POS menu",
            IsDefault = true
        };
        db.Menus.Add(defaultMenu);
        DefaultMenuId = defaultMenu.Id;

        // Create secondary menu (not default)
        var secondaryMenu = new MenuDefinition
        {
            LocationId = TestLocationId,
            Name = "Happy Hour Menu",
            Description = "Reduced prices 4-6pm",
            IsDefault = false
        };
        db.Menus.Add(secondaryMenu);
        SecondaryMenuId = secondaryMenu.Id;

        await db.SaveChangesAsync();

        // Create screen for default menu
        var screen = new MenuScreen
        {
            MenuId = DefaultMenuId,
            Name = "Food",
            Position = 1,
            Color = "#FFFFFF",
            Rows = 4,
            Columns = 5
        };
        db.MenuScreens.Add(screen);
        TestScreenId = screen.Id;

        await db.SaveChangesAsync();

        // Create button linked to burger item
        var burgerButton = new MenuButton
        {
            ScreenId = TestScreenId,
            ItemId = BurgerItemId,
            Row = 0,
            Column = 0,
            RowSpan = 1,
            ColumnSpan = 1,
            Label = "Burger",
            Color = "#FF6600",
            ButtonType = "item"
        };
        db.MenuButtons.Add(burgerButton);
        TestButtonId = burgerButton.Id;

        // Create button for pasta
        var pastaButton = new MenuButton
        {
            ScreenId = TestScreenId,
            ItemId = PastaItemId,
            Row = 0,
            Column = 1,
            RowSpan = 1,
            ColumnSpan = 1,
            Label = "Pasta",
            Color = "#CC6600",
            ButtonType = "item"
        };
        db.MenuButtons.Add(pastaButton);

        // Create category button
        var categoryButton = new MenuButton
        {
            ScreenId = TestScreenId,
            ItemId = null,
            Row = 3,
            Column = 4,
            RowSpan = 1,
            ColumnSpan = 1,
            Label = "Drinks",
            Color = "#0066FF",
            ButtonType = "category"
        };
        db.MenuButtons.Add(categoryButton);

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

    public MenuDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MenuDbContext>();
    }
}
