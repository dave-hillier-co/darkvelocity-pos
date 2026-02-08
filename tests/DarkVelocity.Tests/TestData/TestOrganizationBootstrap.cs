using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;

namespace DarkVelocity.Tests.TestData;

/// <summary>
/// Composable bootstrap helper that calls grain interfaces to populate the test cluster
/// with a complete "The Plough & Harrow" gastropub organisation.
/// Each per-domain method is independent and guarded against double-creation.
/// </summary>
public class TestOrganizationBootstrap
{
    private readonly IGrainFactory _grainFactory;

    private static readonly Guid OrgId = UkTestData.OrgId;
    private static readonly Guid[] AllSiteIds =
    [
        UkTestData.LondonSiteId,
        UkTestData.ManchesterSiteId,
        UkTestData.BirminghamSiteId,
        UkTestData.EdinburghSiteId
    ];

    public TestOrganizationBootstrap(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    /// <summary>
    /// Bootstraps all domains in dependency order.
    /// </summary>
    public async Task BootstrapAllAsync()
    {
        // L0
        await BootstrapOrganizationAsync();

        // L1
        await Task.WhenAll(
            BootstrapSitesAsync(),
            BootstrapUserGroupsAsync(),
            BootstrapTaxRatesAsync(),
            BootstrapRolesAsync(),
            BootstrapLoyaltyProgramAsync());

        // L2
        await Task.WhenAll(
            BootstrapUsersAsync(),
            BootstrapFloorPlansAsync(),
            BootstrapBookingSettingsAsync(),
            BootstrapKitchenStationsAsync(),
            BootstrapAccountingGroupsAsync());

        // L3
        await Task.WhenAll(
            BootstrapMenuAsync(),
            BootstrapTablesAsync(),
            BootstrapEmployeesAsync());

        // L4
        await BootstrapCustomersAsync();

        // L5
        await Task.WhenAll(
            BootstrapRecipesAsync(),
            BootstrapInventoryAsync(),
            BootstrapSuppliersAsync());

        // L6
        await Task.WhenAll(
            BootstrapGiftCardsAsync(),
            BootstrapChannelsAsync());
    }

    public async Task BootstrapOrganizationAsync()
    {
        var grain = _grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(OrgId));
        if (await grain.ExistsAsync()) return;

        await grain.CreateAsync(new CreateOrganizationCommand(
            UkTestData.Organization.Name,
            UkTestData.Organization.Slug,
            UkTestData.Organization.Settings));
    }

    public async Task BootstrapSitesAsync(params Guid[] siteIds)
    {
        var sites = (siteIds.Length > 0 ? siteIds : AllSiteIds)
            .Select(id => UkTestData.Sites.All.First(s => s.Id == id));

        foreach (var site in sites)
        {
            var grain = _grainFactory.GetGrain<ISiteGrain>(GrainKeys.Site(OrgId, site.Id));
            if (await grain.ExistsAsync()) continue;

            await grain.CreateAsync(new CreateSiteCommand(
                OrgId,
                site.Name,
                site.Code,
                site.Address,
                site.Timezone,
                site.Currency));

            // Register site with org
            var orgGrain = _grainFactory.GetGrain<IOrganizationGrain>(GrainKeys.Organization(OrgId));
            await orgGrain.AddSiteAsync(site.Id);
        }
    }

    public async Task BootstrapUserGroupsAsync()
    {
        foreach (var group in UkTestData.UserGroups.All)
        {
            var grain = _grainFactory.GetGrain<IUserGroupGrain>(GrainKeys.UserGroup(OrgId, group.Id));
            if (await grain.ExistsAsync()) continue;

            await grain.CreateAsync(new CreateUserGroupCommand(
                OrgId,
                group.Name,
                group.Description));
        }
    }

    public async Task BootstrapUsersAsync()
    {
        foreach (var user in UkTestData.Users.All)
        {
            var grain = _grainFactory.GetGrain<IUserGrain>(GrainKeys.User(OrgId, user.Id));
            if (await grain.ExistsAsync()) continue;

            await grain.CreateAsync(new CreateUserCommand(
                OrgId,
                user.Email,
                user.DisplayName,
                user.Type,
                user.FirstName,
                user.LastName));

            if (user.Pin is not null)
                await grain.SetPinAsync(user.Pin);

            foreach (var siteId in user.SiteAccess)
                await grain.GrantSiteAccessAsync(siteId);

            foreach (var groupId in user.GroupIds)
                await grain.AddToGroupAsync(groupId);
        }
    }

    public async Task BootstrapRolesAsync()
    {
        foreach (var role in UkTestData.Roles.All)
        {
            var grain = _grainFactory.GetGrain<IRoleGrain>(GrainKeys.Role(OrgId, role.Id));

            var department = role.Department switch
            {
                "Management" => Department.Management,
                "BackOfHouse" => Department.BackOfHouse,
                _ => Department.FrontOfHouse
            };

            try
            {
                await grain.CreateAsync(new CreateRoleCommand(
                    role.Name,
                    department,
                    role.DefaultHourlyRate,
                    role.Color,
                    role.SortOrder,
                    []));
            }
            catch (InvalidOperationException)
            {
                // Already exists
            }
        }
    }

    public async Task BootstrapTaxRatesAsync()
    {
        foreach (var taxRate in UkTestData.TaxRates.All)
        {
            var grain = _grainFactory.GetGrain<ITaxRateGrain>(
                GrainKeys.TaxRate(OrgId, taxRate.CountryCode, taxRate.FiscalCode));

            try
            {
                await grain.CreateAsync(new CreateTaxRateCommand(
                    taxRate.CountryCode,
                    taxRate.Rate,
                    taxRate.FiscalCode,
                    taxRate.Description,
                    new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    null));
            }
            catch (InvalidOperationException)
            {
                // Already exists
            }
        }
    }

    public async Task BootstrapAccountingGroupsAsync()
    {
        foreach (var group in UkTestData.AccountingGroups.All)
        {
            var grain = _grainFactory.GetGrain<IAccountingGroupGrain>(
                GrainKeys.AccountingGroup(OrgId, group.Id));

            try
            {
                await grain.CreateAsync(new CreateAccountingGroupCommand(
                    OrgId,
                    group.Name,
                    group.Code,
                    null,
                    group.RevenueAccountCode,
                    group.CogsAccountCode));
            }
            catch (InvalidOperationException)
            {
                // Already exists
            }
        }
    }

    public async Task BootstrapMenuAsync()
    {
        // Create menu definition
        var menuGrain = _grainFactory.GetGrain<IMenuDefinitionGrain>(
            GrainKeys.Menu(OrgId, UkTestData.DefaultMenuDefinitionId));
        try
        {
            await menuGrain.CreateAsync(new CreateMenuDefinitionCommand(
                OrgId,
                "Main Menu",
                "The Plough & Harrow standard menu",
                true));
        }
        catch (InvalidOperationException)
        {
            // Already exists
        }

        // Create categories
        foreach (var cat in UkTestData.MenuCategories.All)
        {
            var catGrain = _grainFactory.GetGrain<IMenuCategoryGrain>(
                GrainKeys.MenuCategory(OrgId, cat.Id));
            try
            {
                await catGrain.CreateAsync(new CreateMenuCategoryCommand(
                    OrgId,
                    cat.Name,
                    cat.Description,
                    cat.DisplayOrder,
                    cat.Color));
            }
            catch (InvalidOperationException)
            {
                // Already exists
            }
        }

        // Create menu items
        foreach (var item in UkTestData.MenuItems.All)
        {
            var itemGrain = _grainFactory.GetGrain<IMenuItemGrain>(
                GrainKeys.MenuItem(OrgId, item.Id));
            try
            {
                var accountingGroupId = UkTestData.AccountingGroups.GetGroupIdForCategory(item.CategoryId);

                await itemGrain.CreateAsync(new CreateMenuItemCommand(
                    OrgId,
                    item.CategoryId,
                    accountingGroupId,
                    null,
                    item.Name,
                    item.Description,
                    item.Price,
                    null,
                    item.Sku,
                    item.TrackInventory));
            }
            catch (InvalidOperationException)
            {
                // Already exists
            }
        }
    }

    public async Task BootstrapFloorPlansAsync(params Guid[] siteIds)
    {
        var targetSiteIds = siteIds.Length > 0 ? siteIds : AllSiteIds;

        foreach (var siteId in targetSiteIds)
        {
            var fp = UkTestData.FloorPlans.ForSite(siteId);
            var grain = _grainFactory.GetGrain<IFloorPlanGrain>(
                GrainKeys.FloorPlan(OrgId, siteId, fp.Id));
            if (await grain.ExistsAsync()) continue;

            await grain.CreateAsync(new CreateFloorPlanCommand(
                OrgId,
                siteId,
                fp.Name,
                fp.IsDefault,
                fp.Width,
                fp.Height));
        }
    }

    public async Task BootstrapTablesAsync(params Guid[] siteIds)
    {
        var targetSiteIds = siteIds.Length > 0 ? siteIds : AllSiteIds;

        foreach (var siteId in targetSiteIds)
        {
            var floorPlan = UkTestData.FloorPlans.ForSite(siteId);

            foreach (var table in UkTestData.Tables.ForSite(siteId))
            {
                var grain = _grainFactory.GetGrain<ITableGrain>(
                    GrainKeys.Table(OrgId, siteId, table.Id));
                if (await grain.ExistsAsync()) continue;

                await grain.CreateAsync(new CreateTableCommand(
                    OrgId,
                    siteId,
                    table.Number,
                    table.MinCapacity,
                    table.MaxCapacity,
                    table.Name,
                    table.Shape,
                    floorPlan.Id));

                foreach (var tag in table.Tags)
                    await grain.AddTagAsync(tag);

                // Register table with floor plan
                var fpGrain = _grainFactory.GetGrain<IFloorPlanGrain>(
                    GrainKeys.FloorPlan(OrgId, siteId, floorPlan.Id));
                await fpGrain.AddTableAsync(table.Id);
            }
        }
    }

    public async Task BootstrapKitchenStationsAsync(params Guid[] siteIds)
    {
        var targetSiteIds = siteIds.Length > 0 ? siteIds : AllSiteIds;

        foreach (var siteId in targetSiteIds)
        {
            foreach (var station in UkTestData.KitchenStations.ForSite(siteId))
            {
                var grain = _grainFactory.GetGrain<IKitchenStationGrain>(
                    GrainKeys.KitchenStation(OrgId, siteId, station.Id));
                if (await grain.ExistsAsync()) continue;

                var stationType = Enum.Parse<StationType>(station.Type);

                await grain.OpenAsync(new OpenStationCommand(
                    OrgId,
                    siteId,
                    station.Name,
                    stationType,
                    station.DisplayOrder));
            }
        }
    }

    public async Task BootstrapEmployeesAsync()
    {
        // Build a lookup from email to userId so we can link employees to users
        var usersByEmail = UkTestData.Users.All.ToDictionary(u => u.Email, u => u.Id);

        foreach (var emp in UkTestData.Employees.All)
        {
            var grain = _grainFactory.GetGrain<IEmployeeGrain>(
                GrainKeys.Employee(OrgId, emp.Id));
            if (await grain.ExistsAsync()) continue;

            // Match employee to user by email
            var userId = usersByEmail.GetValueOrDefault(emp.Email, Guid.Empty);
            if (userId == Guid.Empty)
            {
                // If no matching user, use a deterministic guid based on employee ID
                userId = emp.Id;
            }

            await grain.CreateAsync(new CreateEmployeeCommand(
                OrgId,
                userId,
                emp.DefaultSiteId,
                emp.EmployeeNumber,
                emp.FirstName,
                emp.LastName,
                emp.Email,
                emp.EmploymentType));

            // Set pay rate if specified
            if (emp.HourlyRate.HasValue || emp.SalaryAmount.HasValue)
            {
                await grain.UpdateAsync(new UpdateEmployeeCommand(
                    HourlyRate: emp.HourlyRate,
                    SalaryAmount: emp.SalaryAmount));
            }
        }
    }

    public async Task BootstrapCustomersAsync()
    {
        foreach (var customer in UkTestData.Customers.All)
        {
            var grain = _grainFactory.GetGrain<ICustomerGrain>(
                GrainKeys.Customer(OrgId, customer.Id));
            if (await grain.ExistsAsync()) continue;

            await grain.CreateAsync(new CreateCustomerCommand(
                OrgId,
                customer.FirstName,
                customer.LastName,
                customer.Email,
                customer.Phone,
                customer.Source));

            if (customer.DateOfBirth.HasValue)
            {
                await grain.UpdateAsync(new UpdateCustomerCommand(
                    DateOfBirth: customer.DateOfBirth));
            }

            foreach (var tag in customer.Tags)
                await grain.AddTagAsync(tag);

            if (customer.DietaryRestrictions.Count > 0 || customer.Allergens.Count > 0)
            {
                await grain.UpdatePreferencesAsync(new UpdatePreferencesCommand(
                    DietaryRestrictions: customer.DietaryRestrictions.Count > 0 ? customer.DietaryRestrictions : null,
                    Allergens: customer.Allergens.Count > 0 ? customer.Allergens : null));
            }
        }
    }

    public async Task BootstrapLoyaltyProgramAsync()
    {
        var grain = _grainFactory.GetGrain<ILoyaltyProgramGrain>(
            GrainKeys.LoyaltyProgram(OrgId, UkTestData.LoyaltyProgramId));
        if (await grain.ExistsAsync()) return;

        await grain.CreateAsync(new CreateLoyaltyProgramCommand(
            OrgId,
            "Plough Rewards",
            "Earn points on every visit"));

        // Add earning rule: 1 point per GBP
        await grain.AddEarningRuleAsync(new AddEarningRuleCommand(
            "Standard Earn",
            EarningType.PerDollar,
            PointsPerDollar: 1));

        // Add tiers
        await grain.AddTierAsync(new AddTierCommand(
            "Bronze", Level: 1, PointsRequired: 0, Color: "#CD7F32"));
        await grain.AddTierAsync(new AddTierCommand(
            "Silver", Level: 2, PointsRequired: 500, Color: "#C0C0C0"));
        await grain.AddTierAsync(new AddTierCommand(
            "Gold", Level: 3, PointsRequired: 2000, Color: "#FFD700"));

        await grain.ActivateAsync();
    }

    public async Task BootstrapSuppliersAsync()
    {
        foreach (var supplier in UkTestData.Suppliers.All)
        {
            var grain = _grainFactory.GetGrain<ISupplierGrain>(
                GrainKeys.Supplier(OrgId, supplier.Id));

            // Format address as single-line string (CreateSupplierCommand.Address is string)
            var addr = supplier.Address;
            var addressLine = string.Join(", ",
                new[] { addr.Street, addr.Street2, addr.City, addr.State, addr.PostalCode, addr.Country }
                    .Where(s => !string.IsNullOrEmpty(s)));

            try
            {
                await grain.CreateAsync(new CreateSupplierCommand(
                    $"SUP-{supplier.Name[..3].ToUpperInvariant()}",
                    supplier.Name,
                    supplier.ContactName,
                    supplier.Email,
                    supplier.Phone,
                    addressLine,
                    30,
                    supplier.LeadTimeDays,
                    null));
            }
            catch (InvalidOperationException)
            {
                // Already exists
            }
        }
    }

    public async Task BootstrapInventoryAsync(params Guid[] siteIds)
    {
        // Default to London only to keep activation count manageable
        var targetSiteIds = siteIds.Length > 0 ? siteIds : [UkTestData.LondonSiteId];

        foreach (var siteId in targetSiteIds)
        {
            foreach (var ingredient in UkTestData.Ingredients.All)
            {
                var grain = _grainFactory.GetGrain<IInventoryGrain>(
                    GrainKeys.Inventory(OrgId, siteId, ingredient.Id));
                if (await grain.ExistsAsync()) continue;

                await grain.InitializeAsync(new InitializeInventoryCommand(
                    OrgId,
                    siteId,
                    ingredient.Id,
                    ingredient.Name,
                    ingredient.Sku,
                    ingredient.Unit,
                    ingredient.Category,
                    ingredient.ReorderPoint,
                    ingredient.ParLevel));
            }
        }
    }

    public async Task BootstrapRecipesAsync()
    {
        foreach (var recipe in UkTestData.Recipes.All)
        {
            var documentId = recipe.Id.ToString();
            var grain = _grainFactory.GetGrain<IRecipeDocumentGrain>(
                GrainKeys.RecipeDocument(OrgId, documentId));
            if (await grain.ExistsAsync()) continue;

            var ingredients = recipe.Ingredients.Select(i => new CreateRecipeIngredientCommand(
                i.IngredientId,
                i.Name,
                i.Quantity,
                i.Unit)).ToList();

            await grain.CreateAsync(new CreateRecipeDocumentCommand(
                recipe.Name,
                recipe.Description,
                recipe.PortionYield,
                "portion",
                ingredients,
                PrepInstructions: null,
                PrepTimeMinutes: recipe.PrepTimeMinutes,
                CookTimeMinutes: recipe.CookTimeMinutes,
                PublishImmediately: true));
        }
    }

    public async Task BootstrapGiftCardsAsync()
    {
        foreach (var card in UkTestData.GiftCards.All)
        {
            var grain = _grainFactory.GetGrain<IGiftCardGrain>(
                GrainKeys.GiftCard(OrgId, card.Id));
            if (await grain.ExistsAsync()) continue;

            var cardType = card.Type switch
            {
                "Physical" => GiftCardType.Physical,
                "Digital" => GiftCardType.Digital,
                "Promotional" => GiftCardType.Promotional,
                _ => GiftCardType.Digital
            };

            await grain.CreateAsync(new CreateGiftCardCommand(
                OrgId,
                card.CardNumber,
                cardType,
                card.InitialValue,
                card.Currency));
        }
    }

    public async Task BootstrapChannelsAsync()
    {
        foreach (var channel in UkTestData.Channels.All)
        {
            var grain = _grainFactory.GetGrain<IChannelGrain>(
                GrainKeys.Channel(OrgId, channel.Id));

            var platformType = Enum.Parse<DeliveryPlatformType>(channel.PlatformType);
            var integrationType = Enum.Parse<IntegrationType>(channel.IntegrationType);

            try
            {
                await grain.ConnectAsync(new ConnectChannelCommand(
                    platformType,
                    integrationType,
                    channel.Name,
                    null,
                    null,
                    null,
                    null));
            }
            catch (InvalidOperationException)
            {
                // Already connected
            }
        }
    }

    public async Task BootstrapBookingSettingsAsync(params Guid[] siteIds)
    {
        var targetSiteIds = siteIds.Length > 0 ? siteIds : AllSiteIds;

        foreach (var siteId in targetSiteIds)
        {
            var grain = _grainFactory.GetGrain<IBookingSettingsGrain>(
                GrainKeys.BookingSettings(OrgId, siteId));
            if (await grain.ExistsAsync()) continue;

            await grain.InitializeAsync(OrgId, siteId);
        }
    }
}
