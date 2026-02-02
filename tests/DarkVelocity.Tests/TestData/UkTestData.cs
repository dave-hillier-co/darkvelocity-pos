using DarkVelocity.Host.State;

namespace DarkVelocity.Tests.TestData;

/// <summary>
/// UK-centric test data for demos featuring "The Plough & Harrow" gastropub chain.
/// All data is realistic with proper UK formatting (addresses, phone numbers, postcodes).
/// </summary>
public static class UkTestData
{
    // Fixed GUIDs for consistent test data
    public static readonly Guid OrgId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid LondonSiteId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid ManchesterSiteId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid BirminghamSiteId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly Guid EdinburghSiteId = Guid.Parse("20000000-0000-0000-0000-000000000004");

    // Loyalty Program
    public static readonly Guid LoyaltyProgramId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid BronzeTierId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    public static readonly Guid SilverTierId = Guid.Parse("30000000-0000-0000-0000-000000000003");
    public static readonly Guid GoldTierId = Guid.Parse("30000000-0000-0000-0000-000000000004");

    // User Groups
    public static readonly Guid AdminsGroupId = Guid.Parse("31000000-0000-0000-0000-000000000001");
    public static readonly Guid ManagersGroupId = Guid.Parse("31000000-0000-0000-0000-000000000002");
    public static readonly Guid KitchenGroupId = Guid.Parse("31000000-0000-0000-0000-000000000003");
    public static readonly Guid FrontOfHouseGroupId = Guid.Parse("31000000-0000-0000-0000-000000000004");
    public static readonly Guid BarGroupId = Guid.Parse("31000000-0000-0000-0000-000000000005");

    #region Organization

    public static class Organization
    {
        public const string Name = "The Plough & Harrow";
        public const string Slug = "plough-harrow";

        public static OrganizationSettings Settings => new()
        {
            DefaultCurrency = "GBP",
            DefaultTimezone = "Europe/London",
            DefaultLocale = "en-GB",
            RequirePinForVoids = true,
            RequireManagerApprovalForDiscounts = true,
            DataRetentionDays = 365 * 7
        };
    }

    #endregion

    #region Users

    public static class Users
    {
        // Super Admin / Owner - full access to everything
        public static readonly Guid SuperAdminId = Guid.Parse("32000000-0000-0000-0000-000000000001");

        public static IEnumerable<UserData> All =>
        [
            // Owner - Company founder with full access
            new()
            {
                Id = SuperAdminId,
                Email = "richard.harrow@ploughandharrow.co.uk",
                DisplayName = "Richard Harrow",
                FirstName = "Richard",
                LastName = "Harrow",
                Type = UserType.Owner,
                Pin = "1234",
                SiteAccess = [LondonSiteId, ManchesterSiteId, BirminghamSiteId, EdinburghSiteId],
                GroupIds = [AdminsGroupId]
            },
            // Admin - Operations Director
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000002"),
                Email = "victoria.chen@ploughandharrow.co.uk",
                DisplayName = "Victoria Chen",
                FirstName = "Victoria",
                LastName = "Chen",
                Type = UserType.Admin,
                Pin = "2345",
                SiteAccess = [LondonSiteId, ManchesterSiteId, BirminghamSiteId, EdinburghSiteId],
                GroupIds = [AdminsGroupId]
            },
            // Admin - Finance Director
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000003"),
                Email = "marcus.thompson@ploughandharrow.co.uk",
                DisplayName = "Marcus Thompson",
                FirstName = "Marcus",
                LastName = "Thompson",
                Type = UserType.Admin,
                Pin = "3456",
                SiteAccess = [LondonSiteId, ManchesterSiteId, BirminghamSiteId, EdinburghSiteId],
                GroupIds = [AdminsGroupId]
            },

            // London Site Users
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0001-000000000001"),
                Email = "james.wilson@ploughandharrow.co.uk",
                DisplayName = "James Wilson",
                FirstName = "James",
                LastName = "Wilson",
                Type = UserType.Manager,
                Pin = "1111",
                SiteAccess = [LondonSiteId],
                GroupIds = [ManagersGroupId]
            },
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0001-000000000002"),
                Email = "sophie.taylor@ploughandharrow.co.uk",
                DisplayName = "Sophie Taylor",
                FirstName = "Sophie",
                LastName = "Taylor",
                Type = UserType.Manager,
                Pin = "1112",
                SiteAccess = [LondonSiteId],
                GroupIds = [ManagersGroupId]
            },
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0001-000000000003"),
                Email = "oliver.brown@ploughandharrow.co.uk",
                DisplayName = "Oliver Brown",
                FirstName = "Oliver",
                LastName = "Brown",
                Type = UserType.Employee,
                Pin = "1113",
                SiteAccess = [LondonSiteId],
                GroupIds = [KitchenGroupId]
            },
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0001-000000000004"),
                Email = "charlotte.evans@ploughandharrow.co.uk",
                DisplayName = "Charlotte Evans",
                FirstName = "Charlotte",
                LastName = "Evans",
                Type = UserType.Employee,
                Pin = "1114",
                SiteAccess = [LondonSiteId],
                GroupIds = [FrontOfHouseGroupId]
            },
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0001-000000000005"),
                Email = "amelia.walker@ploughandharrow.co.uk",
                DisplayName = "Amelia Walker",
                FirstName = "Amelia",
                LastName = "Walker",
                Type = UserType.Employee,
                Pin = "1115",
                SiteAccess = [LondonSiteId],
                GroupIds = [BarGroupId]
            },

            // Manchester Site Users
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0002-000000000001"),
                Email = "jack.hughes@ploughandharrow.co.uk",
                DisplayName = "Jack Hughes",
                FirstName = "Jack",
                LastName = "Hughes",
                Type = UserType.Manager,
                Pin = "2111",
                SiteAccess = [ManchesterSiteId],
                GroupIds = [ManagersGroupId]
            },
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0002-000000000002"),
                Email = "mia.clarke@ploughandharrow.co.uk",
                DisplayName = "Mia Clarke",
                FirstName = "Mia",
                LastName = "Clarke",
                Type = UserType.Employee,
                Pin = "2112",
                SiteAccess = [ManchesterSiteId],
                GroupIds = [KitchenGroupId]
            },
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0002-000000000003"),
                Email = "noah.scott@ploughandharrow.co.uk",
                DisplayName = "Noah Scott",
                FirstName = "Noah",
                LastName = "Scott",
                Type = UserType.Employee,
                Pin = "2113",
                SiteAccess = [ManchesterSiteId],
                GroupIds = [FrontOfHouseGroupId]
            },

            // Birmingham Site Users
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0003-000000000001"),
                Email = "william.king@ploughandharrow.co.uk",
                DisplayName = "William King",
                FirstName = "William",
                LastName = "King",
                Type = UserType.Manager,
                Pin = "3111",
                SiteAccess = [BirminghamSiteId],
                GroupIds = [ManagersGroupId]
            },
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0003-000000000002"),
                Email = "lily.wright@ploughandharrow.co.uk",
                DisplayName = "Lily Wright",
                FirstName = "Lily",
                LastName = "Wright",
                Type = UserType.Employee,
                Pin = "3112",
                SiteAccess = [BirminghamSiteId],
                GroupIds = [KitchenGroupId]
            },

            // Edinburgh Site Users
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0004-000000000001"),
                Email = "isla.campbell@ploughandharrow.co.uk",
                DisplayName = "Isla Campbell",
                FirstName = "Isla",
                LastName = "Campbell",
                Type = UserType.Manager,
                Pin = "4111",
                SiteAccess = [EdinburghSiteId],
                GroupIds = [ManagersGroupId]
            },
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0004-000000000002"),
                Email = "finlay.stewart@ploughandharrow.co.uk",
                DisplayName = "Finlay Stewart",
                FirstName = "Finlay",
                LastName = "Stewart",
                Type = UserType.Employee,
                Pin = "4112",
                SiteAccess = [EdinburghSiteId],
                GroupIds = [KitchenGroupId]
            },
            new()
            {
                Id = Guid.Parse("32000000-0000-0000-0004-000000000003"),
                Email = "ava.macdonald@ploughandharrow.co.uk",
                DisplayName = "Ava MacDonald",
                FirstName = "Ava",
                LastName = "MacDonald",
                Type = UserType.Employee,
                Pin = "4113",
                SiteAccess = [EdinburghSiteId],
                GroupIds = [FrontOfHouseGroupId]
            }
        ];

        // Convenience accessors
        public static UserData SuperAdmin => All.First(u => u.Id == SuperAdminId);
        public static IEnumerable<UserData> Owners => All.Where(u => u.Type == UserType.Owner);
        public static IEnumerable<UserData> Admins => All.Where(u => u.Type == UserType.Admin);
        public static IEnumerable<UserData> Managers => All.Where(u => u.Type == UserType.Manager);
        public static IEnumerable<UserData> ForSite(Guid siteId) => All.Where(u => u.SiteAccess.Contains(siteId));
    }

    public record UserData
    {
        public required Guid Id { get; init; }
        public required string Email { get; init; }
        public required string DisplayName { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public required UserType Type { get; init; }
        public string? Pin { get; init; }
        public List<Guid> SiteAccess { get; init; } = [];
        public List<Guid> GroupIds { get; init; } = [];
    }

    #endregion

    #region User Groups

    public static class UserGroups
    {
        public static IEnumerable<UserGroupData> All =>
        [
            new()
            {
                Id = AdminsGroupId,
                Name = "Administrators",
                Description = "Full system access - owners and operations team",
                IsSystemGroup = true
            },
            new()
            {
                Id = ManagersGroupId,
                Name = "Site Managers",
                Description = "Site-level management access - can manage staff, view reports, approve discounts",
                IsSystemGroup = true
            },
            new()
            {
                Id = KitchenGroupId,
                Name = "Kitchen Staff",
                Description = "Kitchen display access, inventory management, recipe viewing",
                IsSystemGroup = false
            },
            new()
            {
                Id = FrontOfHouseGroupId,
                Name = "Front of House",
                Description = "POS access, table management, order taking",
                IsSystemGroup = false
            },
            new()
            {
                Id = BarGroupId,
                Name = "Bar Staff",
                Description = "Bar POS access, drinks orders, tab management",
                IsSystemGroup = false
            }
        ];
    }

    public record UserGroupData
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public string? Description { get; init; }
        public bool IsSystemGroup { get; init; }
    }

    #endregion

    #region Sites

    public static class Sites
    {
        public static SiteData London => new()
        {
            Id = LondonSiteId,
            Name = "The Plough & Harrow - Shoreditch",
            Code = "LON01",
            Address = new Address
            {
                Street = "42 Brick Lane",
                Street2 = null,
                City = "London",
                State = "Greater London",
                PostalCode = "E1 6RF",
                Country = "United Kingdom",
                Latitude = 51.5218,
                Longitude = -0.0713
            },
            Timezone = "Europe/London",
            Currency = "GBP",
            TaxJurisdiction = new TaxJurisdiction
            {
                Country = "United Kingdom",
                State = "England",
                DefaultTaxRate = 0.20m // 20% VAT
            }
        };

        public static SiteData Manchester => new()
        {
            Id = ManchesterSiteId,
            Name = "The Plough & Harrow - Northern Quarter",
            Code = "MAN01",
            Address = new Address
            {
                Street = "15 Stevenson Square",
                Street2 = null,
                City = "Manchester",
                State = "Greater Manchester",
                PostalCode = "M1 1FB",
                Country = "United Kingdom",
                Latitude = 53.4841,
                Longitude = -2.2354
            },
            Timezone = "Europe/London",
            Currency = "GBP",
            TaxJurisdiction = new TaxJurisdiction
            {
                Country = "United Kingdom",
                State = "England",
                DefaultTaxRate = 0.20m
            }
        };

        public static SiteData Birmingham => new()
        {
            Id = BirminghamSiteId,
            Name = "The Plough & Harrow - Jewellery Quarter",
            Code = "BHM01",
            Address = new Address
            {
                Street = "78 Vyse Street",
                Street2 = null,
                City = "Birmingham",
                State = "West Midlands",
                PostalCode = "B18 6HA",
                Country = "United Kingdom",
                Latitude = 52.4891,
                Longitude = -1.9109
            },
            Timezone = "Europe/London",
            Currency = "GBP",
            TaxJurisdiction = new TaxJurisdiction
            {
                Country = "United Kingdom",
                State = "England",
                DefaultTaxRate = 0.20m
            }
        };

        public static SiteData Edinburgh => new()
        {
            Id = EdinburghSiteId,
            Name = "The Plough & Harrow - Grassmarket",
            Code = "EDI01",
            Address = new Address
            {
                Street = "23 Grassmarket",
                Street2 = null,
                City = "Edinburgh",
                State = "City of Edinburgh",
                PostalCode = "EH1 2HS",
                Country = "United Kingdom",
                Latitude = 55.9478,
                Longitude = -3.1945
            },
            Timezone = "Europe/London",
            Currency = "GBP",
            TaxJurisdiction = new TaxJurisdiction
            {
                Country = "United Kingdom",
                State = "Scotland",
                DefaultTaxRate = 0.20m
            }
        };

        public static IEnumerable<SiteData> All => [London, Manchester, Birmingham, Edinburgh];

        public static OperatingHours StandardOperatingHours => new()
        {
            Schedule =
            [
                new DaySchedule { Day = DayOfWeek.Monday, OpenTime = new TimeOnly(11, 0), CloseTime = new TimeOnly(23, 0) },
                new DaySchedule { Day = DayOfWeek.Tuesday, OpenTime = new TimeOnly(11, 0), CloseTime = new TimeOnly(23, 0) },
                new DaySchedule { Day = DayOfWeek.Wednesday, OpenTime = new TimeOnly(11, 0), CloseTime = new TimeOnly(23, 0) },
                new DaySchedule { Day = DayOfWeek.Thursday, OpenTime = new TimeOnly(11, 0), CloseTime = new TimeOnly(23, 30) },
                new DaySchedule { Day = DayOfWeek.Friday, OpenTime = new TimeOnly(11, 0), CloseTime = new TimeOnly(0, 0) },
                new DaySchedule { Day = DayOfWeek.Saturday, OpenTime = new TimeOnly(10, 0), CloseTime = new TimeOnly(0, 0) },
                new DaySchedule { Day = DayOfWeek.Sunday, OpenTime = new TimeOnly(10, 0), CloseTime = new TimeOnly(22, 0) }
            ]
        };
    }

    public record SiteData
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Code { get; init; }
        public required Address Address { get; init; }
        public required string Timezone { get; init; }
        public required string Currency { get; init; }
        public required TaxJurisdiction TaxJurisdiction { get; init; }
    }

    #endregion

    #region Menu Categories

    public static class MenuCategories
    {
        public static readonly Guid StartersId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        public static readonly Guid MainsId = Guid.Parse("40000000-0000-0000-0000-000000000002");
        public static readonly Guid SundayRoastId = Guid.Parse("40000000-0000-0000-0000-000000000003");
        public static readonly Guid PuddingsId = Guid.Parse("40000000-0000-0000-0000-000000000004");
        public static readonly Guid SidesId = Guid.Parse("40000000-0000-0000-0000-000000000005");
        public static readonly Guid DrinksBeerId = Guid.Parse("40000000-0000-0000-0000-000000000006");
        public static readonly Guid DrinksWineId = Guid.Parse("40000000-0000-0000-0000-000000000007");
        public static readonly Guid DrinksSoftId = Guid.Parse("40000000-0000-0000-0000-000000000008");
        public static readonly Guid DrinksSpiritsId = Guid.Parse("40000000-0000-0000-0000-000000000009");

        public static IEnumerable<MenuCategoryData> All =>
        [
            new() { Id = StartersId, Name = "Starters", DisplayOrder = 1, Color = "#E8B4B8" },
            new() { Id = MainsId, Name = "Mains", DisplayOrder = 2, Color = "#A67B5B" },
            new() { Id = SundayRoastId, Name = "Sunday Roast", Description = "Available Sunday 12-6pm", DisplayOrder = 3, Color = "#8B4513" },
            new() { Id = PuddingsId, Name = "Puddings", DisplayOrder = 4, Color = "#DEB887" },
            new() { Id = SidesId, Name = "Sides", DisplayOrder = 5, Color = "#90EE90" },
            new() { Id = DrinksBeerId, Name = "Beer & Cider", DisplayOrder = 6, Color = "#DAA520" },
            new() { Id = DrinksWineId, Name = "Wine", DisplayOrder = 7, Color = "#722F37" },
            new() { Id = DrinksSoftId, Name = "Soft Drinks", DisplayOrder = 8, Color = "#87CEEB" },
            new() { Id = DrinksSpiritsId, Name = "Spirits & Cocktails", DisplayOrder = 9, Color = "#4169E1" }
        ];
    }

    public record MenuCategoryData
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public string? Description { get; init; }
        public required int DisplayOrder { get; init; }
        public string? Color { get; init; }
    }

    #endregion

    #region Menu Items

    public static class MenuItems
    {
        // Starters
        public static IEnumerable<MenuItemData> Starters =>
        [
            new() { Id = Guid.Parse("50000000-0000-0000-0001-000000000001"), CategoryId = MenuCategories.StartersId, Name = "Scotch Egg", Description = "Free-range egg wrapped in Cumberland sausage meat, panko crumb, piccalilli", Price = 8.50m, Sku = "START-001" },
            new() { Id = Guid.Parse("50000000-0000-0000-0001-000000000002"), CategoryId = MenuCategories.StartersId, Name = "Soup of the Day", Description = "Served with crusty sourdough bread", Price = 6.95m, Sku = "START-002" },
            new() { Id = Guid.Parse("50000000-0000-0000-0001-000000000003"), CategoryId = MenuCategories.StartersId, Name = "Prawn Cocktail", Description = "North Atlantic prawns, Marie Rose sauce, baby gem, brown bread", Price = 9.95m, Sku = "START-003" },
            new() { Id = Guid.Parse("50000000-0000-0000-0001-000000000004"), CategoryId = MenuCategories.StartersId, Name = "Pork Scratchings", Description = "With apple sauce dip", Price = 4.50m, Sku = "START-004" },
            new() { Id = Guid.Parse("50000000-0000-0000-0001-000000000005"), CategoryId = MenuCategories.StartersId, Name = "Welsh Rarebit", Description = "Mature cheddar, ale, mustard on toasted sourdough", Price = 7.95m, Sku = "START-005" },
            new() { Id = Guid.Parse("50000000-0000-0000-0001-000000000006"), CategoryId = MenuCategories.StartersId, Name = "Potted Crab", Description = "Brown and white Cromer crab, clarified butter, toast", Price = 12.95m, Sku = "START-006" }
        ];

        // Mains
        public static IEnumerable<MenuItemData> Mains =>
        [
            new() { Id = Guid.Parse("50000000-0000-0000-0002-000000000001"), CategoryId = MenuCategories.MainsId, Name = "Fish & Chips", Description = "Beer-battered North Sea haddock, triple-cooked chips, mushy peas, tartare sauce", Price = 16.95m, Sku = "MAIN-001" },
            new() { Id = Guid.Parse("50000000-0000-0000-0002-000000000002"), CategoryId = MenuCategories.MainsId, Name = "Steak & Ale Pie", Description = "28-day aged beef, Timothy Taylor Landlord ale, shortcrust pastry, mash, greens", Price = 17.50m, Sku = "MAIN-002" },
            new() { Id = Guid.Parse("50000000-0000-0000-0002-000000000003"), CategoryId = MenuCategories.MainsId, Name = "Bangers & Mash", Description = "Cumberland sausages, creamy mash, onion gravy, crispy onions", Price = 14.95m, Sku = "MAIN-003" },
            new() { Id = Guid.Parse("50000000-0000-0000-0002-000000000004"), CategoryId = MenuCategories.MainsId, Name = "Shepherd's Pie", Description = "Slow-braised lamb shoulder, root vegetables, cheesy mash crust", Price = 15.95m, Sku = "MAIN-004" },
            new() { Id = Guid.Parse("50000000-0000-0000-0002-000000000005"), CategoryId = MenuCategories.MainsId, Name = "Ploughman's Lunch", Description = "Mature cheddar, ham hock, piccalilli, pickled onion, crusty bread, apple", Price = 13.95m, Sku = "MAIN-005" },
            new() { Id = Guid.Parse("50000000-0000-0000-0002-000000000006"), CategoryId = MenuCategories.MainsId, Name = "Beef Burger", Description = "8oz chuck & brisket patty, smoked bacon, cheddar, brioche, chips", Price = 15.95m, Sku = "MAIN-006" },
            new() { Id = Guid.Parse("50000000-0000-0000-0002-000000000007"), CategoryId = MenuCategories.MainsId, Name = "Chicken & Leek Pie", Description = "Free-range chicken, leeks, tarragon cream, puff pastry, greens", Price = 16.50m, Sku = "MAIN-007" },
            new() { Id = Guid.Parse("50000000-0000-0000-0002-000000000008"), CategoryId = MenuCategories.MainsId, Name = "Beer-Battered Halloumi", Description = "Triple-cooked chips, mushy peas, tartare sauce (V)", Price = 14.95m, Sku = "MAIN-008" },
            new() { Id = Guid.Parse("50000000-0000-0000-0002-000000000009"), CategoryId = MenuCategories.MainsId, Name = "Gammon, Egg & Chips", Description = "Thick-cut gammon, fried eggs, triple-cooked chips, pineapple", Price = 15.50m, Sku = "MAIN-009" },
            new() { Id = Guid.Parse("50000000-0000-0000-0002-000000000010"), CategoryId = MenuCategories.MainsId, Name = "8oz Ribeye Steak", Description = "28-day aged, triple-cooked chips, grilled tomato, watercress", Price = 28.95m, Sku = "MAIN-010" }
        ];

        // Sunday Roast
        public static IEnumerable<MenuItemData> SundayRoast =>
        [
            new() { Id = Guid.Parse("50000000-0000-0000-0003-000000000001"), CategoryId = MenuCategories.SundayRoastId, Name = "Roast Beef", Description = "Topside of British beef, Yorkshire pudding, roast potatoes, seasonal veg, gravy", Price = 18.95m, Sku = "SUND-001" },
            new() { Id = Guid.Parse("50000000-0000-0000-0003-000000000002"), CategoryId = MenuCategories.SundayRoastId, Name = "Roast Chicken", Description = "Free-range chicken breast, Yorkshire pudding, roast potatoes, seasonal veg, gravy", Price = 17.95m, Sku = "SUND-002" },
            new() { Id = Guid.Parse("50000000-0000-0000-0003-000000000003"), CategoryId = MenuCategories.SundayRoastId, Name = "Roast Lamb", Description = "Slow-roasted leg of lamb, Yorkshire pudding, roast potatoes, seasonal veg, mint gravy", Price = 19.95m, Sku = "SUND-003" },
            new() { Id = Guid.Parse("50000000-0000-0000-0003-000000000004"), CategoryId = MenuCategories.SundayRoastId, Name = "Roast Pork Belly", Description = "Crackling, Yorkshire pudding, roast potatoes, seasonal veg, apple sauce, gravy", Price = 18.50m, Sku = "SUND-004" },
            new() { Id = Guid.Parse("50000000-0000-0000-0003-000000000005"), CategoryId = MenuCategories.SundayRoastId, Name = "Nut Roast", Description = "Chestnut & mushroom roast, Yorkshire pudding, roast potatoes, seasonal veg, gravy (V)", Price = 15.95m, Sku = "SUND-005" }
        ];

        // Puddings
        public static IEnumerable<MenuItemData> Puddings =>
        [
            new() { Id = Guid.Parse("50000000-0000-0000-0004-000000000001"), CategoryId = MenuCategories.PuddingsId, Name = "Sticky Toffee Pudding", Description = "Dates, toffee sauce, clotted cream", Price = 7.95m, Sku = "PUD-001" },
            new() { Id = Guid.Parse("50000000-0000-0000-0004-000000000002"), CategoryId = MenuCategories.PuddingsId, Name = "Apple Crumble", Description = "Bramley apples, oat crumble, vanilla custard", Price = 7.50m, Sku = "PUD-002" },
            new() { Id = Guid.Parse("50000000-0000-0000-0004-000000000003"), CategoryId = MenuCategories.PuddingsId, Name = "Eton Mess", Description = "Meringue, strawberries, Chantilly cream", Price = 7.95m, Sku = "PUD-003" },
            new() { Id = Guid.Parse("50000000-0000-0000-0004-000000000004"), CategoryId = MenuCategories.PuddingsId, Name = "Bread & Butter Pudding", Description = "Brioche, sultanas, vanilla custard", Price = 7.50m, Sku = "PUD-004" },
            new() { Id = Guid.Parse("50000000-0000-0000-0004-000000000005"), CategoryId = MenuCategories.PuddingsId, Name = "Selection of British Cheeses", Description = "Stilton, mature cheddar, Cornish brie, oatcakes, chutney", Price = 11.95m, Sku = "PUD-005" }
        ];

        // Sides
        public static IEnumerable<MenuItemData> Sides =>
        [
            new() { Id = Guid.Parse("50000000-0000-0000-0005-000000000001"), CategoryId = MenuCategories.SidesId, Name = "Triple-Cooked Chips", Price = 4.50m, Sku = "SIDE-001" },
            new() { Id = Guid.Parse("50000000-0000-0000-0005-000000000002"), CategoryId = MenuCategories.SidesId, Name = "Buttered Greens", Price = 4.50m, Sku = "SIDE-002" },
            new() { Id = Guid.Parse("50000000-0000-0000-0005-000000000003"), CategoryId = MenuCategories.SidesId, Name = "Creamy Mash", Price = 4.00m, Sku = "SIDE-003" },
            new() { Id = Guid.Parse("50000000-0000-0000-0005-000000000004"), CategoryId = MenuCategories.SidesId, Name = "House Salad", Price = 4.50m, Sku = "SIDE-004" },
            new() { Id = Guid.Parse("50000000-0000-0000-0005-000000000005"), CategoryId = MenuCategories.SidesId, Name = "Onion Rings", Price = 4.50m, Sku = "SIDE-005" },
            new() { Id = Guid.Parse("50000000-0000-0000-0005-000000000006"), CategoryId = MenuCategories.SidesId, Name = "Extra Yorkshire Pudding", Price = 2.00m, Sku = "SIDE-006" }
        ];

        // Drinks - Beer & Cider
        public static IEnumerable<MenuItemData> BeersAndCiders =>
        [
            new() { Id = Guid.Parse("50000000-0000-0000-0006-000000000001"), CategoryId = MenuCategories.DrinksBeerId, Name = "Timothy Taylor Landlord", Description = "Pint, 4.3%", Price = 6.20m, Sku = "BEER-001" },
            new() { Id = Guid.Parse("50000000-0000-0000-0006-000000000002"), CategoryId = MenuCategories.DrinksBeerId, Name = "London Pride", Description = "Pint, 4.7%", Price = 5.95m, Sku = "BEER-002" },
            new() { Id = Guid.Parse("50000000-0000-0000-0006-000000000003"), CategoryId = MenuCategories.DrinksBeerId, Name = "Doom Bar", Description = "Pint, 4.3%", Price = 5.80m, Sku = "BEER-003" },
            new() { Id = Guid.Parse("50000000-0000-0000-0006-000000000004"), CategoryId = MenuCategories.DrinksBeerId, Name = "Camden Hells Lager", Description = "Pint, 4.6%", Price = 6.50m, Sku = "BEER-004" },
            new() { Id = Guid.Parse("50000000-0000-0000-0006-000000000005"), CategoryId = MenuCategories.DrinksBeerId, Name = "BrewDog Punk IPA", Description = "Pint, 5.6%", Price = 6.80m, Sku = "BEER-005" },
            new() { Id = Guid.Parse("50000000-0000-0000-0006-000000000006"), CategoryId = MenuCategories.DrinksBeerId, Name = "Guinness", Description = "Pint, 4.2%", Price = 6.40m, Sku = "BEER-006" },
            new() { Id = Guid.Parse("50000000-0000-0000-0006-000000000007"), CategoryId = MenuCategories.DrinksBeerId, Name = "Aspall Cyder", Description = "Pint, 5.5%", Price = 5.95m, Sku = "BEER-007" },
            new() { Id = Guid.Parse("50000000-0000-0000-0006-000000000008"), CategoryId = MenuCategories.DrinksBeerId, Name = "Cornish Orchards Gold", Description = "330ml bottle, 5.0%", Price = 5.50m, Sku = "BEER-008" }
        ];

        // Drinks - Wine
        public static IEnumerable<MenuItemData> Wines =>
        [
            new() { Id = Guid.Parse("50000000-0000-0000-0007-000000000001"), CategoryId = MenuCategories.DrinksWineId, Name = "House White - Pinot Grigio", Description = "175ml glass", Price = 6.50m, Sku = "WINE-001" },
            new() { Id = Guid.Parse("50000000-0000-0000-0007-000000000002"), CategoryId = MenuCategories.DrinksWineId, Name = "House Red - Merlot", Description = "175ml glass", Price = 6.50m, Sku = "WINE-002" },
            new() { Id = Guid.Parse("50000000-0000-0000-0007-000000000003"), CategoryId = MenuCategories.DrinksWineId, Name = "Prosecco", Description = "125ml glass", Price = 7.50m, Sku = "WINE-003" },
            new() { Id = Guid.Parse("50000000-0000-0000-0007-000000000004"), CategoryId = MenuCategories.DrinksWineId, Name = "Chapel Down Bacchus", Description = "English white, 175ml", Price = 9.95m, Sku = "WINE-004" },
            new() { Id = Guid.Parse("50000000-0000-0000-0007-000000000005"), CategoryId = MenuCategories.DrinksWineId, Name = "Bottle - House White", Description = "Pinot Grigio, 750ml", Price = 22.00m, Sku = "WINE-005" },
            new() { Id = Guid.Parse("50000000-0000-0000-0007-000000000006"), CategoryId = MenuCategories.DrinksWineId, Name = "Bottle - House Red", Description = "Merlot, 750ml", Price = 22.00m, Sku = "WINE-006" }
        ];

        // Drinks - Soft
        public static IEnumerable<MenuItemData> SoftDrinks =>
        [
            new() { Id = Guid.Parse("50000000-0000-0000-0008-000000000001"), CategoryId = MenuCategories.DrinksSoftId, Name = "Coca-Cola", Description = "330ml bottle", Price = 3.50m, Sku = "SOFT-001" },
            new() { Id = Guid.Parse("50000000-0000-0000-0008-000000000002"), CategoryId = MenuCategories.DrinksSoftId, Name = "Diet Coke", Description = "330ml bottle", Price = 3.50m, Sku = "SOFT-002" },
            new() { Id = Guid.Parse("50000000-0000-0000-0008-000000000003"), CategoryId = MenuCategories.DrinksSoftId, Name = "Fentimans Ginger Beer", Description = "275ml bottle", Price = 4.50m, Sku = "SOFT-003" },
            new() { Id = Guid.Parse("50000000-0000-0000-0008-000000000004"), CategoryId = MenuCategories.DrinksSoftId, Name = "Fentimans Victorian Lemonade", Description = "275ml bottle", Price = 4.50m, Sku = "SOFT-004" },
            new() { Id = Guid.Parse("50000000-0000-0000-0008-000000000005"), CategoryId = MenuCategories.DrinksSoftId, Name = "Elderflower Presse", Description = "275ml bottle", Price = 4.50m, Sku = "SOFT-005" },
            new() { Id = Guid.Parse("50000000-0000-0000-0008-000000000006"), CategoryId = MenuCategories.DrinksSoftId, Name = "Fresh Orange Juice", Price = 3.95m, Sku = "SOFT-006" },
            new() { Id = Guid.Parse("50000000-0000-0000-0008-000000000007"), CategoryId = MenuCategories.DrinksSoftId, Name = "Sparkling Water", Description = "750ml bottle", Price = 3.95m, Sku = "SOFT-007" },
            new() { Id = Guid.Parse("50000000-0000-0000-0008-000000000008"), CategoryId = MenuCategories.DrinksSoftId, Name = "Still Water", Description = "750ml bottle", Price = 3.95m, Sku = "SOFT-008" }
        ];

        // Drinks - Spirits & Cocktails
        public static IEnumerable<MenuItemData> Spirits =>
        [
            new() { Id = Guid.Parse("50000000-0000-0000-0009-000000000001"), CategoryId = MenuCategories.DrinksSpiritsId, Name = "Sipsmith London Dry Gin", Description = "25ml, served with Fever-Tree tonic", Price = 8.50m, Sku = "SPRT-001" },
            new() { Id = Guid.Parse("50000000-0000-0000-0009-000000000002"), CategoryId = MenuCategories.DrinksSpiritsId, Name = "Tanqueray 10", Description = "25ml, served with Fever-Tree tonic", Price = 9.50m, Sku = "SPRT-002" },
            new() { Id = Guid.Parse("50000000-0000-0000-0009-000000000003"), CategoryId = MenuCategories.DrinksSpiritsId, Name = "Aperol Spritz", Price = 10.50m, Sku = "SPRT-003" },
            new() { Id = Guid.Parse("50000000-0000-0000-0009-000000000004"), CategoryId = MenuCategories.DrinksSpiritsId, Name = "Espresso Martini", Price = 11.50m, Sku = "SPRT-004" },
            new() { Id = Guid.Parse("50000000-0000-0000-0009-000000000005"), CategoryId = MenuCategories.DrinksSpiritsId, Name = "Old Fashioned", Description = "Woodford Reserve bourbon", Price = 12.00m, Sku = "SPRT-005" },
            new() { Id = Guid.Parse("50000000-0000-0000-0009-000000000006"), CategoryId = MenuCategories.DrinksSpiritsId, Name = "Negroni", Price = 11.00m, Sku = "SPRT-006" },
            new() { Id = Guid.Parse("50000000-0000-0000-0009-000000000007"), CategoryId = MenuCategories.DrinksSpiritsId, Name = "House Whisky", Description = "Famous Grouse, 25ml", Price = 5.50m, Sku = "SPRT-007" },
            new() { Id = Guid.Parse("50000000-0000-0000-0009-000000000008"), CategoryId = MenuCategories.DrinksSpiritsId, Name = "Jack Daniel's", Description = "25ml", Price = 6.50m, Sku = "SPRT-008" }
        ];

        public static IEnumerable<MenuItemData> All =>
            Starters.Concat(Mains).Concat(SundayRoast).Concat(Puddings).Concat(Sides)
            .Concat(BeersAndCiders).Concat(Wines).Concat(SoftDrinks).Concat(Spirits);
    }

    public record MenuItemData
    {
        public required Guid Id { get; init; }
        public required Guid CategoryId { get; init; }
        public required string Name { get; init; }
        public string? Description { get; init; }
        public required decimal Price { get; init; }
        public required string Sku { get; init; }
        public bool TrackInventory { get; init; }
    }

    #endregion

    #region Employees

    public static class Employees
    {
        public static IEnumerable<EmployeeData> All =>
        [
            // London Site
            new() { Id = Guid.Parse("60000000-0000-0000-0001-000000000001"), DefaultSiteId = LondonSiteId, EmployeeNumber = "EMP001", FirstName = "James", LastName = "Wilson", Email = "james.wilson@ploughandharrow.co.uk", Role = "General Manager", Department = "Management", EmploymentType = EmploymentType.FullTime, HourlyRate = null, SalaryAmount = 48000m },
            new() { Id = Guid.Parse("60000000-0000-0000-0001-000000000002"), DefaultSiteId = LondonSiteId, EmployeeNumber = "EMP002", FirstName = "Sophie", LastName = "Taylor", Email = "sophie.taylor@ploughandharrow.co.uk", Role = "Assistant Manager", Department = "Management", EmploymentType = EmploymentType.FullTime, HourlyRate = null, SalaryAmount = 35000m },
            new() { Id = Guid.Parse("60000000-0000-0000-0001-000000000003"), DefaultSiteId = LondonSiteId, EmployeeNumber = "EMP003", FirstName = "Oliver", LastName = "Brown", Email = "oliver.brown@ploughandharrow.co.uk", Role = "Head Chef", Department = "Kitchen", EmploymentType = EmploymentType.FullTime, HourlyRate = null, SalaryAmount = 42000m },
            new() { Id = Guid.Parse("60000000-0000-0000-0001-000000000004"), DefaultSiteId = LondonSiteId, EmployeeNumber = "EMP004", FirstName = "Emily", LastName = "Davies", Email = "emily.davies@ploughandharrow.co.uk", Role = "Sous Chef", Department = "Kitchen", EmploymentType = EmploymentType.FullTime, HourlyRate = 15.50m },
            new() { Id = Guid.Parse("60000000-0000-0000-0001-000000000005"), DefaultSiteId = LondonSiteId, EmployeeNumber = "EMP005", FirstName = "George", LastName = "Thompson", Email = "george.thompson@ploughandharrow.co.uk", Role = "Line Cook", Department = "Kitchen", EmploymentType = EmploymentType.FullTime, HourlyRate = 13.50m },
            new() { Id = Guid.Parse("60000000-0000-0000-0001-000000000006"), DefaultSiteId = LondonSiteId, EmployeeNumber = "EMP006", FirstName = "Charlotte", LastName = "Evans", Email = "charlotte.evans@ploughandharrow.co.uk", Role = "Server", Department = "Front of House", EmploymentType = EmploymentType.FullTime, HourlyRate = 12.00m },
            new() { Id = Guid.Parse("60000000-0000-0000-0001-000000000007"), DefaultSiteId = LondonSiteId, EmployeeNumber = "EMP007", FirstName = "Harry", LastName = "Roberts", Email = "harry.roberts@ploughandharrow.co.uk", Role = "Server", Department = "Front of House", EmploymentType = EmploymentType.PartTime, HourlyRate = 12.00m },
            new() { Id = Guid.Parse("60000000-0000-0000-0001-000000000008"), DefaultSiteId = LondonSiteId, EmployeeNumber = "EMP008", FirstName = "Amelia", LastName = "Walker", Email = "amelia.walker@ploughandharrow.co.uk", Role = "Bartender", Department = "Bar", EmploymentType = EmploymentType.FullTime, HourlyRate = 13.00m },

            // Manchester Site
            new() { Id = Guid.Parse("60000000-0000-0000-0002-000000000001"), DefaultSiteId = ManchesterSiteId, EmployeeNumber = "EMP101", FirstName = "Jack", LastName = "Hughes", Email = "jack.hughes@ploughandharrow.co.uk", Role = "General Manager", Department = "Management", EmploymentType = EmploymentType.FullTime, HourlyRate = null, SalaryAmount = 45000m },
            new() { Id = Guid.Parse("60000000-0000-0000-0002-000000000002"), DefaultSiteId = ManchesterSiteId, EmployeeNumber = "EMP102", FirstName = "Mia", LastName = "Clarke", Email = "mia.clarke@ploughandharrow.co.uk", Role = "Head Chef", Department = "Kitchen", EmploymentType = EmploymentType.FullTime, HourlyRate = null, SalaryAmount = 40000m },
            new() { Id = Guid.Parse("60000000-0000-0000-0002-000000000003"), DefaultSiteId = ManchesterSiteId, EmployeeNumber = "EMP103", FirstName = "Noah", LastName = "Scott", Email = "noah.scott@ploughandharrow.co.uk", Role = "Server", Department = "Front of House", EmploymentType = EmploymentType.FullTime, HourlyRate = 11.50m },
            new() { Id = Guid.Parse("60000000-0000-0000-0002-000000000004"), DefaultSiteId = ManchesterSiteId, EmployeeNumber = "EMP104", FirstName = "Isabella", LastName = "Green", Email = "isabella.green@ploughandharrow.co.uk", Role = "Bartender", Department = "Bar", EmploymentType = EmploymentType.PartTime, HourlyRate = 12.50m },

            // Birmingham Site
            new() { Id = Guid.Parse("60000000-0000-0000-0003-000000000001"), DefaultSiteId = BirminghamSiteId, EmployeeNumber = "EMP201", FirstName = "William", LastName = "King", Email = "william.king@ploughandharrow.co.uk", Role = "General Manager", Department = "Management", EmploymentType = EmploymentType.FullTime, HourlyRate = null, SalaryAmount = 44000m },
            new() { Id = Guid.Parse("60000000-0000-0000-0003-000000000002"), DefaultSiteId = BirminghamSiteId, EmployeeNumber = "EMP202", FirstName = "Lily", LastName = "Wright", Email = "lily.wright@ploughandharrow.co.uk", Role = "Head Chef", Department = "Kitchen", EmploymentType = EmploymentType.FullTime, HourlyRate = null, SalaryAmount = 38000m },
            new() { Id = Guid.Parse("60000000-0000-0000-0003-000000000003"), DefaultSiteId = BirminghamSiteId, EmployeeNumber = "EMP203", FirstName = "Thomas", LastName = "Hall", Email = "thomas.hall@ploughandharrow.co.uk", Role = "Server", Department = "Front of House", EmploymentType = EmploymentType.FullTime, HourlyRate = 11.50m },

            // Edinburgh Site
            new() { Id = Guid.Parse("60000000-0000-0000-0004-000000000001"), DefaultSiteId = EdinburghSiteId, EmployeeNumber = "EMP301", FirstName = "Isla", LastName = "Campbell", Email = "isla.campbell@ploughandharrow.co.uk", Role = "General Manager", Department = "Management", EmploymentType = EmploymentType.FullTime, HourlyRate = null, SalaryAmount = 46000m },
            new() { Id = Guid.Parse("60000000-0000-0000-0004-000000000002"), DefaultSiteId = EdinburghSiteId, EmployeeNumber = "EMP302", FirstName = "Finlay", LastName = "Stewart", Email = "finlay.stewart@ploughandharrow.co.uk", Role = "Head Chef", Department = "Kitchen", EmploymentType = EmploymentType.FullTime, HourlyRate = null, SalaryAmount = 40000m },
            new() { Id = Guid.Parse("60000000-0000-0000-0004-000000000003"), DefaultSiteId = EdinburghSiteId, EmployeeNumber = "EMP303", FirstName = "Ava", LastName = "MacDonald", Email = "ava.macdonald@ploughandharrow.co.uk", Role = "Server", Department = "Front of House", EmploymentType = EmploymentType.FullTime, HourlyRate = 12.00m },
            new() { Id = Guid.Parse("60000000-0000-0000-0004-000000000004"), DefaultSiteId = EdinburghSiteId, EmployeeNumber = "EMP304", FirstName = "Liam", LastName = "Murray", Email = "liam.murray@ploughandharrow.co.uk", Role = "Bartender", Department = "Bar", EmploymentType = EmploymentType.PartTime, HourlyRate = 12.50m }
        ];
    }

    public record EmployeeData
    {
        public required Guid Id { get; init; }
        public required Guid DefaultSiteId { get; init; }
        public required string EmployeeNumber { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public required string Role { get; init; }
        public required string Department { get; init; }
        public required EmploymentType EmploymentType { get; init; }
        public decimal? HourlyRate { get; init; }
        public decimal? SalaryAmount { get; init; }
        public string FullName => $"{FirstName} {LastName}";
    }

    #endregion

    #region Customers

    public static class Customers
    {
        public static IEnumerable<CustomerData> All =>
        [
            new()
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000001"),
                FirstName = "Emma",
                LastName = "Johnson",
                Email = "emma.johnson@gmail.com",
                Phone = "+44 7700 900123",
                Source = CustomerSource.Website,
                Address = new Address { Street = "14 Primrose Hill Road", City = "London", State = "Greater London", PostalCode = "NW3 3NA", Country = "United Kingdom" },
                Segment = CustomerSegment.Loyal,
                TotalVisits = 28,
                TotalSpend = 1456.80m,
                Tags = ["Regular", "Wine Lover"]
            },
            new()
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000002"),
                FirstName = "David",
                LastName = "Smith",
                Email = "david.smith@outlook.com",
                Phone = "+44 7700 900456",
                Source = CustomerSource.Direct,
                Address = new Address { Street = "27 Park Lane", City = "Manchester", State = "Greater Manchester", PostalCode = "M14 5AQ", Country = "United Kingdom" },
                Segment = CustomerSegment.Champion,
                TotalVisits = 52,
                TotalSpend = 3240.50m,
                Tags = ["VIP", "Regular", "Craft Beer"],
                DateOfBirth = new DateOnly(1985, 3, 15)
            },
            new()
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000003"),
                FirstName = "Sarah",
                LastName = "Williams",
                Email = "sarah.w@icloud.com",
                Phone = "+44 7700 900789",
                Source = CustomerSource.Mobile,
                Address = new Address { Street = "8 Queen Street", City = "Edinburgh", State = "City of Edinburgh", PostalCode = "EH2 1JE", Country = "United Kingdom" },
                Segment = CustomerSegment.Regular,
                TotalVisits = 12,
                TotalSpend = 524.30m,
                Tags = ["Sunday Roast Fan"]
            },
            new()
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000004"),
                FirstName = "Michael",
                LastName = "Brown",
                Email = "michael.brown@yahoo.co.uk",
                Phone = "+44 7700 900321",
                Source = CustomerSource.Referral,
                Address = new Address { Street = "45 Corporation Street", City = "Birmingham", State = "West Midlands", PostalCode = "B2 4TE", Country = "United Kingdom" },
                Segment = CustomerSegment.New,
                TotalVisits = 3,
                TotalSpend = 156.75m,
                DietaryRestrictions = ["Vegetarian"],
                Tags = ["New Customer"]
            },
            new()
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000005"),
                FirstName = "Rachel",
                LastName = "Taylor",
                Email = "rachel.taylor@btinternet.com",
                Phone = "+44 7700 900654",
                Source = CustomerSource.Website,
                Address = new Address { Street = "91 Kings Road", City = "London", State = "Greater London", PostalCode = "SW3 4NX", Country = "United Kingdom" },
                Segment = CustomerSegment.Loyal,
                TotalVisits = 35,
                TotalSpend = 2180.25m,
                DateOfBirth = new DateOnly(1990, 7, 22),
                Allergens = ["Nuts"],
                Tags = ["Regular", "Birthday Club"]
            },
            new()
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000006"),
                FirstName = "James",
                LastName = "Anderson",
                Email = "james.anderson@protonmail.com",
                Phone = "+44 7700 900987",
                Source = CustomerSource.Direct,
                Address = new Address { Street = "23 Deansgate", City = "Manchester", State = "Greater Manchester", PostalCode = "M3 1AZ", Country = "United Kingdom" },
                Segment = CustomerSegment.Regular,
                TotalVisits = 18,
                TotalSpend = 890.60m,
                Tags = ["Whisky Enthusiast"]
            },
            new()
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000007"),
                FirstName = "Lucy",
                LastName = "Harris",
                Email = "lucy.harris@gmail.com",
                Phone = "+44 7700 900147",
                Source = CustomerSource.Website,
                Address = new Address { Street = "56 Buchanan Street", City = "Glasgow", State = "Glasgow City", PostalCode = "G1 3HL", Country = "United Kingdom" },
                Segment = CustomerSegment.AtRisk,
                TotalVisits = 8,
                TotalSpend = 340.20m,
                Tags = ["Lapsed - Re-engage"]
            },
            new()
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000008"),
                FirstName = "Tom",
                LastName = "Clark",
                Email = "tom.clark@hotmail.co.uk",
                Phone = "+44 7700 900258",
                Source = CustomerSource.ThirdParty,
                Address = new Address { Street = "12 Broad Street", City = "Birmingham", State = "West Midlands", PostalCode = "B15 1AY", Country = "United Kingdom" },
                Segment = CustomerSegment.Regular,
                TotalVisits = 15,
                TotalSpend = 678.90m,
                DietaryRestrictions = ["Gluten-Free"],
                Tags = ["Dietary Requirements"]
            },
            new()
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000009"),
                FirstName = "Hannah",
                LastName = "Lewis",
                Email = "hannah.lewis@gmail.com",
                Phone = "+44 7700 900369",
                Source = CustomerSource.Mobile,
                Address = new Address { Street = "78 Rose Street", City = "Edinburgh", State = "City of Edinburgh", PostalCode = "EH2 2NN", Country = "United Kingdom" },
                Segment = CustomerSegment.Loyal,
                TotalVisits = 22,
                TotalSpend = 1120.45m,
                Tags = ["Regular", "Cocktail Lover"]
            },
            new()
            {
                Id = Guid.Parse("70000000-0000-0000-0000-000000000010"),
                FirstName = "Daniel",
                LastName = "Walker",
                Email = "dan.walker@yahoo.com",
                Phone = "+44 7700 900471",
                Source = CustomerSource.Direct,
                Address = new Address { Street = "34 Hoxton Square", City = "London", State = "Greater London", PostalCode = "N1 6PB", Country = "United Kingdom" },
                Segment = CustomerSegment.Champion,
                TotalVisits = 67,
                TotalSpend = 4520.80m,
                DateOfBirth = new DateOnly(1978, 11, 8),
                Tags = ["VIP", "Founding Member", "Wine Club"]
            }
        ];
    }

    public record CustomerData
    {
        public required Guid Id { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
        public required string Phone { get; init; }
        public required CustomerSource Source { get; init; }
        public Address? Address { get; init; }
        public CustomerSegment Segment { get; init; }
        public int TotalVisits { get; init; }
        public decimal TotalSpend { get; init; }
        public DateOnly? DateOfBirth { get; init; }
        public List<string> DietaryRestrictions { get; init; } = [];
        public List<string> Allergens { get; init; } = [];
        public List<string> Tags { get; init; } = [];
        public string FullName => $"{FirstName} {LastName}";
    }

    #endregion

    #region Inventory / Ingredients

    public static class Ingredients
    {
        public static IEnumerable<IngredientData> All =>
        [
            // Proteins
            new() { Id = Guid.Parse("80000000-0000-0000-0001-000000000001"), Name = "North Sea Haddock Fillets", Sku = "ING-FISH-001", Unit = "kg", Category = "Proteins", ReorderPoint = 5, ParLevel = 15 },
            new() { Id = Guid.Parse("80000000-0000-0000-0001-000000000002"), Name = "28-Day Aged Beef (Stewing)", Sku = "ING-MEAT-001", Unit = "kg", Category = "Proteins", ReorderPoint = 8, ParLevel = 20 },
            new() { Id = Guid.Parse("80000000-0000-0000-0001-000000000003"), Name = "Cumberland Sausages", Sku = "ING-MEAT-002", Unit = "kg", Category = "Proteins", ReorderPoint = 5, ParLevel = 12 },
            new() { Id = Guid.Parse("80000000-0000-0000-0001-000000000004"), Name = "Lamb Shoulder (Bone-in)", Sku = "ING-MEAT-003", Unit = "kg", Category = "Proteins", ReorderPoint = 4, ParLevel = 10 },
            new() { Id = Guid.Parse("80000000-0000-0000-0001-000000000005"), Name = "Free-Range Chicken Breast", Sku = "ING-MEAT-004", Unit = "kg", Category = "Proteins", ReorderPoint = 6, ParLevel = 15 },
            new() { Id = Guid.Parse("80000000-0000-0000-0001-000000000006"), Name = "Gammon Joint", Sku = "ING-MEAT-005", Unit = "kg", Category = "Proteins", ReorderPoint = 3, ParLevel = 8 },
            new() { Id = Guid.Parse("80000000-0000-0000-0001-000000000007"), Name = "28-Day Ribeye Steak", Sku = "ING-MEAT-006", Unit = "kg", Category = "Proteins", ReorderPoint = 4, ParLevel = 10 },
            new() { Id = Guid.Parse("80000000-0000-0000-0001-000000000008"), Name = "Pork Belly", Sku = "ING-MEAT-007", Unit = "kg", Category = "Proteins", ReorderPoint = 3, ParLevel = 8 },
            new() { Id = Guid.Parse("80000000-0000-0000-0001-000000000009"), Name = "Cromer Crab (Dressed)", Sku = "ING-FISH-002", Unit = "each", Category = "Proteins", ReorderPoint = 6, ParLevel = 12 },
            new() { Id = Guid.Parse("80000000-0000-0000-0001-000000000010"), Name = "North Atlantic Prawns", Sku = "ING-FISH-003", Unit = "kg", Category = "Proteins", ReorderPoint = 2, ParLevel = 5 },

            // Dairy
            new() { Id = Guid.Parse("80000000-0000-0000-0002-000000000001"), Name = "Free-Range Eggs", Sku = "ING-DAIRY-001", Unit = "dozen", Category = "Dairy", ReorderPoint = 10, ParLevel = 30 },
            new() { Id = Guid.Parse("80000000-0000-0000-0002-000000000002"), Name = "Mature Cheddar", Sku = "ING-DAIRY-002", Unit = "kg", Category = "Dairy", ReorderPoint = 3, ParLevel = 8 },
            new() { Id = Guid.Parse("80000000-0000-0000-0002-000000000003"), Name = "Stilton", Sku = "ING-DAIRY-003", Unit = "kg", Category = "Dairy", ReorderPoint = 1, ParLevel = 3 },
            new() { Id = Guid.Parse("80000000-0000-0000-0002-000000000004"), Name = "Clotted Cream", Sku = "ING-DAIRY-004", Unit = "litres", Category = "Dairy", ReorderPoint = 2, ParLevel = 5 },
            new() { Id = Guid.Parse("80000000-0000-0000-0002-000000000005"), Name = "Double Cream", Sku = "ING-DAIRY-005", Unit = "litres", Category = "Dairy", ReorderPoint = 4, ParLevel = 10 },
            new() { Id = Guid.Parse("80000000-0000-0000-0002-000000000006"), Name = "Unsalted Butter", Sku = "ING-DAIRY-006", Unit = "kg", Category = "Dairy", ReorderPoint = 5, ParLevel = 12 },
            new() { Id = Guid.Parse("80000000-0000-0000-0002-000000000007"), Name = "Halloumi", Sku = "ING-DAIRY-007", Unit = "kg", Category = "Dairy", ReorderPoint = 2, ParLevel = 5 },

            // Vegetables
            new() { Id = Guid.Parse("80000000-0000-0000-0003-000000000001"), Name = "Maris Piper Potatoes", Sku = "ING-VEG-001", Unit = "kg", Category = "Vegetables", ReorderPoint = 20, ParLevel = 50 },
            new() { Id = Guid.Parse("80000000-0000-0000-0003-000000000002"), Name = "Bramley Apples", Sku = "ING-VEG-002", Unit = "kg", Category = "Vegetables", ReorderPoint = 5, ParLevel = 12 },
            new() { Id = Guid.Parse("80000000-0000-0000-0003-000000000003"), Name = "Leeks", Sku = "ING-VEG-003", Unit = "kg", Category = "Vegetables", ReorderPoint = 3, ParLevel = 8 },
            new() { Id = Guid.Parse("80000000-0000-0000-0003-000000000004"), Name = "Garden Peas (Frozen)", Sku = "ING-VEG-004", Unit = "kg", Category = "Vegetables", ReorderPoint = 5, ParLevel = 15 },
            new() { Id = Guid.Parse("80000000-0000-0000-0003-000000000005"), Name = "Carrots", Sku = "ING-VEG-005", Unit = "kg", Category = "Vegetables", ReorderPoint = 8, ParLevel = 20 },
            new() { Id = Guid.Parse("80000000-0000-0000-0003-000000000006"), Name = "Parsnips", Sku = "ING-VEG-006", Unit = "kg", Category = "Vegetables", ReorderPoint = 4, ParLevel = 10 },
            new() { Id = Guid.Parse("80000000-0000-0000-0003-000000000007"), Name = "Savoy Cabbage", Sku = "ING-VEG-007", Unit = "each", Category = "Vegetables", ReorderPoint = 5, ParLevel = 12 },
            new() { Id = Guid.Parse("80000000-0000-0000-0003-000000000008"), Name = "White Onions", Sku = "ING-VEG-008", Unit = "kg", Category = "Vegetables", ReorderPoint = 10, ParLevel = 25 },

            // Dry Goods
            new() { Id = Guid.Parse("80000000-0000-0000-0004-000000000001"), Name = "Plain Flour", Sku = "ING-DRY-001", Unit = "kg", Category = "Dry Goods", ReorderPoint = 10, ParLevel = 25 },
            new() { Id = Guid.Parse("80000000-0000-0000-0004-000000000002"), Name = "Panko Breadcrumbs", Sku = "ING-DRY-002", Unit = "kg", Category = "Dry Goods", ReorderPoint = 3, ParLevel = 8 },
            new() { Id = Guid.Parse("80000000-0000-0000-0004-000000000003"), Name = "Dried Dates", Sku = "ING-DRY-003", Unit = "kg", Category = "Dry Goods", ReorderPoint = 2, ParLevel = 5 },
            new() { Id = Guid.Parse("80000000-0000-0000-0004-000000000004"), Name = "Oats", Sku = "ING-DRY-004", Unit = "kg", Category = "Dry Goods", ReorderPoint = 3, ParLevel = 8 },
            new() { Id = Guid.Parse("80000000-0000-0000-0004-000000000005"), Name = "Sultanas", Sku = "ING-DRY-005", Unit = "kg", Category = "Dry Goods", ReorderPoint = 2, ParLevel = 5 },
            new() { Id = Guid.Parse("80000000-0000-0000-0004-000000000006"), Name = "Chestnuts (Vacuum Packed)", Sku = "ING-DRY-006", Unit = "kg", Category = "Dry Goods", ReorderPoint = 2, ParLevel = 5 },

            // Beverages - Beer
            new() { Id = Guid.Parse("80000000-0000-0000-0005-000000000001"), Name = "Timothy Taylor Landlord (Cask)", Sku = "BEV-BEER-001", Unit = "firkin", Category = "Beverages", ReorderPoint = 2, ParLevel = 4 },
            new() { Id = Guid.Parse("80000000-0000-0000-0005-000000000002"), Name = "London Pride (Cask)", Sku = "BEV-BEER-002", Unit = "firkin", Category = "Beverages", ReorderPoint = 2, ParLevel = 4 },
            new() { Id = Guid.Parse("80000000-0000-0000-0005-000000000003"), Name = "Camden Hells (Keg)", Sku = "BEV-BEER-003", Unit = "keg", Category = "Beverages", ReorderPoint = 2, ParLevel = 5 },
            new() { Id = Guid.Parse("80000000-0000-0000-0005-000000000004"), Name = "Guinness (Keg)", Sku = "BEV-BEER-004", Unit = "keg", Category = "Beverages", ReorderPoint = 2, ParLevel = 4 },

            // Beverages - Wine
            new() { Id = Guid.Parse("80000000-0000-0000-0006-000000000001"), Name = "House Pinot Grigio", Sku = "BEV-WINE-001", Unit = "bottles", Category = "Beverages", ReorderPoint = 12, ParLevel = 36 },
            new() { Id = Guid.Parse("80000000-0000-0000-0006-000000000002"), Name = "House Merlot", Sku = "BEV-WINE-002", Unit = "bottles", Category = "Beverages", ReorderPoint = 12, ParLevel = 36 },
            new() { Id = Guid.Parse("80000000-0000-0000-0006-000000000003"), Name = "Prosecco", Sku = "BEV-WINE-003", Unit = "bottles", Category = "Beverages", ReorderPoint = 12, ParLevel = 24 },
            new() { Id = Guid.Parse("80000000-0000-0000-0006-000000000004"), Name = "Chapel Down Bacchus", Sku = "BEV-WINE-004", Unit = "bottles", Category = "Beverages", ReorderPoint = 6, ParLevel = 12 },

            // Beverages - Spirits
            new() { Id = Guid.Parse("80000000-0000-0000-0007-000000000001"), Name = "Sipsmith London Dry Gin", Sku = "BEV-SPRT-001", Unit = "bottles", Category = "Beverages", ReorderPoint = 3, ParLevel = 6 },
            new() { Id = Guid.Parse("80000000-0000-0000-0007-000000000002"), Name = "Tanqueray 10", Sku = "BEV-SPRT-002", Unit = "bottles", Category = "Beverages", ReorderPoint = 2, ParLevel = 4 },
            new() { Id = Guid.Parse("80000000-0000-0000-0007-000000000003"), Name = "Aperol", Sku = "BEV-SPRT-003", Unit = "bottles", Category = "Beverages", ReorderPoint = 2, ParLevel = 4 },
            new() { Id = Guid.Parse("80000000-0000-0000-0007-000000000004"), Name = "Woodford Reserve Bourbon", Sku = "BEV-SPRT-004", Unit = "bottles", Category = "Beverages", ReorderPoint = 2, ParLevel = 4 },
            new() { Id = Guid.Parse("80000000-0000-0000-0007-000000000005"), Name = "Famous Grouse", Sku = "BEV-SPRT-005", Unit = "bottles", Category = "Beverages", ReorderPoint = 2, ParLevel = 4 }
        ];
    }

    public record IngredientData
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string Sku { get; init; }
        public required string Unit { get; init; }
        public required string Category { get; init; }
        public decimal ReorderPoint { get; init; }
        public decimal ParLevel { get; init; }
    }

    #endregion

    #region Suppliers

    public static class Suppliers
    {
        public static IEnumerable<SupplierData> All =>
        [
            new()
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000001"),
                Name = "Smithfield Meat Company",
                ContactName = "Robert Smithfield",
                Email = "orders@smithfieldmeat.co.uk",
                Phone = "+44 20 7329 4567",
                Address = new Address { Street = "Unit 12, Smithfield Market", City = "London", State = "Greater London", PostalCode = "EC1A 9PS", Country = "United Kingdom" },
                Categories = ["Proteins"],
                LeadTimeDays = 1,
                MinimumOrderValue = 150m
            },
            new()
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000002"),
                Name = "North Sea Fish Supplies",
                ContactName = "Margaret Whitby",
                Email = "supply@northseafish.co.uk",
                Phone = "+44 1472 345678",
                Address = new Address { Street = "Dock Road", City = "Grimsby", State = "Lincolnshire", PostalCode = "DN31 3LL", Country = "United Kingdom" },
                Categories = ["Proteins"],
                LeadTimeDays = 1,
                MinimumOrderValue = 200m
            },
            new()
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000003"),
                Name = "Fresh Farm Produce",
                ContactName = "Alan Greenfield",
                Email = "orders@freshfarmproduce.co.uk",
                Phone = "+44 1234 567890",
                Address = new Address { Street = "Manor Farm", Street2 = "Covent Garden Market", City = "London", State = "Greater London", PostalCode = "WC2E 8RF", Country = "United Kingdom" },
                Categories = ["Vegetables", "Dairy"],
                LeadTimeDays = 1,
                MinimumOrderValue = 100m
            },
            new()
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000004"),
                Name = "Booker Wholesale",
                ContactName = "Customer Service",
                Email = "orders@booker.co.uk",
                Phone = "+44 800 123 4567",
                Address = new Address { Street = "Booker Distribution Centre", City = "Wellingborough", State = "Northamptonshire", PostalCode = "NN8 6GR", Country = "United Kingdom" },
                Categories = ["Dry Goods", "Beverages"],
                LeadTimeDays = 2,
                MinimumOrderValue = 250m
            },
            new()
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000005"),
                Name = "Matthew Clark Wines",
                ContactName = "Sarah Vintage",
                Email = "sales@matthewclark.co.uk",
                Phone = "+44 117 927 6500",
                Address = new Address { Street = "Whitchurch Lane", City = "Bristol", State = "Bristol", PostalCode = "BS14 0JZ", Country = "United Kingdom" },
                Categories = ["Beverages"],
                LeadTimeDays = 3,
                MinimumOrderValue = 500m
            },
            new()
            {
                Id = Guid.Parse("90000000-0000-0000-0000-000000000006"),
                Name = "Carlsberg Marstons Brewing",
                ContactName = "Account Manager",
                Email = "pubpartners@carlsbergmarstons.co.uk",
                Phone = "+44 1onal 789012",
                Address = new Address { Street = "Marston's House", City = "Wolverhampton", State = "West Midlands", PostalCode = "WV1 4JT", Country = "United Kingdom" },
                Categories = ["Beverages"],
                LeadTimeDays = 3,
                MinimumOrderValue = 400m
            }
        ];
    }

    public record SupplierData
    {
        public required Guid Id { get; init; }
        public required string Name { get; init; }
        public required string ContactName { get; init; }
        public required string Email { get; init; }
        public required string Phone { get; init; }
        public required Address Address { get; init; }
        public List<string> Categories { get; init; } = [];
        public int LeadTimeDays { get; init; }
        public decimal MinimumOrderValue { get; init; }
    }

    #endregion

    #region Tables

    public static class Tables
    {
        public static IEnumerable<TableData> ForSite(Guid siteId)
        {
            var baseId = siteId == LondonSiteId ? "A1000000" :
                         siteId == ManchesterSiteId ? "A2000000" :
                         siteId == BirminghamSiteId ? "A3000000" : "A4000000";

            return
            [
                // Main Dining - 2 tops
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000001"), SiteId = siteId, Number = "1", Name = "Window 1", MinCapacity = 1, MaxCapacity = 2, Shape = TableShape.Square, Section = "Main Dining", Tags = ["Window", "Romantic"] },
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000002"), SiteId = siteId, Number = "2", Name = "Window 2", MinCapacity = 1, MaxCapacity = 2, Shape = TableShape.Square, Section = "Main Dining", Tags = ["Window"] },
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000003"), SiteId = siteId, Number = "3", MinCapacity = 1, MaxCapacity = 2, Shape = TableShape.Round, Section = "Main Dining" },
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000004"), SiteId = siteId, Number = "4", MinCapacity = 1, MaxCapacity = 2, Shape = TableShape.Round, Section = "Main Dining" },

                // Main Dining - 4 tops
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000005"), SiteId = siteId, Number = "5", MinCapacity = 2, MaxCapacity = 4, Shape = TableShape.Rectangle, Section = "Main Dining" },
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000006"), SiteId = siteId, Number = "6", MinCapacity = 2, MaxCapacity = 4, Shape = TableShape.Rectangle, Section = "Main Dining" },
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000007"), SiteId = siteId, Number = "7", MinCapacity = 2, MaxCapacity = 4, Shape = TableShape.Rectangle, Section = "Main Dining" },
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000008"), SiteId = siteId, Number = "8", MinCapacity = 2, MaxCapacity = 4, Shape = TableShape.Rectangle, Section = "Main Dining" },

                // Main Dining - 6 tops
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000009"), SiteId = siteId, Number = "9", Name = "Large Booth", MinCapacity = 4, MaxCapacity = 6, Shape = TableShape.Rectangle, Section = "Main Dining", Tags = ["Booth", "Groups"] },
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000010"), SiteId = siteId, Number = "10", MinCapacity = 4, MaxCapacity = 6, Shape = TableShape.Rectangle, Section = "Main Dining", Tags = ["Groups"] },

                // Bar Area
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000011"), SiteId = siteId, Number = "B1", Name = "Bar Stool 1", MinCapacity = 1, MaxCapacity = 2, Shape = TableShape.Bar, Section = "Bar", Tags = ["Bar"] },
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000012"), SiteId = siteId, Number = "B2", Name = "Bar Stool 2", MinCapacity = 1, MaxCapacity = 2, Shape = TableShape.Bar, Section = "Bar", Tags = ["Bar"] },
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000013"), SiteId = siteId, Number = "B3", Name = "Bar Stool 3", MinCapacity = 1, MaxCapacity = 2, Shape = TableShape.Bar, Section = "Bar", Tags = ["Bar"] },
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000014"), SiteId = siteId, Number = "B4", Name = "Bar Stool 4", MinCapacity = 1, MaxCapacity = 2, Shape = TableShape.Bar, Section = "Bar", Tags = ["Bar"] },

                // Private Dining
                new() { Id = Guid.Parse($"{baseId}-0000-0000-000000000015"), SiteId = siteId, Number = "PD1", Name = "Private Dining", MinCapacity = 6, MaxCapacity = 12, Shape = TableShape.Rectangle, Section = "Private Dining", Tags = ["Private", "Groups", "Events"] }
            ];
        }
    }

    public record TableData
    {
        public required Guid Id { get; init; }
        public required Guid SiteId { get; init; }
        public required string Number { get; init; }
        public string? Name { get; init; }
        public required int MinCapacity { get; init; }
        public required int MaxCapacity { get; init; }
        public required TableShape Shape { get; init; }
        public required string Section { get; init; }
        public List<string> Tags { get; init; } = [];
    }

    #endregion

    #region Sample Bookings

    public static class SampleBookings
    {
        public static IEnumerable<BookingData> ForDate(DateOnly date, Guid siteId)
        {
            var dateTime = date.ToDateTime(TimeOnly.MinValue);
            return
            [
                new()
                {
                    Id = Guid.NewGuid(),
                    SiteId = siteId,
                    GuestName = "Thompson",
                    GuestPhone = "+44 7700 900111",
                    GuestEmail = "j.thompson@email.com",
                    RequestedTime = dateTime.AddHours(12).AddMinutes(30),
                    PartySize = 4,
                    SpecialRequests = "High chair needed",
                    Source = BookingSource.Website
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    SiteId = siteId,
                    GuestName = "Patel",
                    GuestPhone = "+44 7700 900222",
                    GuestEmail = "r.patel@email.com",
                    RequestedTime = dateTime.AddHours(13),
                    PartySize = 2,
                    Occasion = "Anniversary",
                    Source = BookingSource.Phone
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    SiteId = siteId,
                    GuestName = "O'Brien",
                    GuestPhone = "+44 7700 900333",
                    GuestEmail = "s.obrien@email.com",
                    RequestedTime = dateTime.AddHours(18).AddMinutes(30),
                    PartySize = 6,
                    SpecialRequests = "Birthday cake - need to bring our own",
                    Occasion = "Birthday",
                    Source = BookingSource.Website
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    SiteId = siteId,
                    GuestName = "Williams",
                    GuestPhone = "+44 7700 900444",
                    GuestEmail = "m.williams@email.com",
                    RequestedTime = dateTime.AddHours(19),
                    PartySize = 2,
                    Source = BookingSource.OpenTable
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    SiteId = siteId,
                    GuestName = "Singh",
                    GuestPhone = "+44 7700 900555",
                    GuestEmail = "a.singh@email.com",
                    RequestedTime = dateTime.AddHours(19).AddMinutes(30),
                    PartySize = 4,
                    SpecialRequests = "Vegetarian guests - please advise on options",
                    Source = BookingSource.Website
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    SiteId = siteId,
                    GuestName = "Corporate Event - Barclays",
                    GuestPhone = "+44 20 7116 1234",
                    GuestEmail = "events@barclays.com",
                    RequestedTime = dateTime.AddHours(18),
                    PartySize = 10,
                    SpecialRequests = "Pre-order menus, separate bill per person",
                    Occasion = "Business Dinner",
                    Source = BookingSource.Phone,
                    IsVip = true
                }
            ];
        }
    }

    public record BookingData
    {
        public required Guid Id { get; init; }
        public required Guid SiteId { get; init; }
        public required string GuestName { get; init; }
        public required string GuestPhone { get; init; }
        public required string GuestEmail { get; init; }
        public required DateTime RequestedTime { get; init; }
        public required int PartySize { get; init; }
        public string? SpecialRequests { get; init; }
        public string? Occasion { get; init; }
        public BookingSource Source { get; init; }
        public bool IsVip { get; init; }
    }

    #endregion

    #region Sample Orders

    public static class SampleOrders
    {
        /// <summary>
        /// Generates realistic sample orders for demo purposes.
        /// </summary>
        public static IEnumerable<OrderData> Generate(Guid siteId, Guid serverId, int count = 5)
        {
            var menuItems = MenuItems.All.ToList();
            var random = new Random(42); // Fixed seed for reproducibility

            for (int i = 0; i < count; i++)
            {
                var tableNumber = random.Next(1, 11).ToString();
                var guestCount = random.Next(1, 5);
                var orderType = i == 0 ? OrderType.TakeOut : OrderType.DineIn;

                var lines = new List<OrderLineData>();

                // Add 2-5 items per order
                var itemCount = random.Next(2, 6);
                for (int j = 0; j < itemCount; j++)
                {
                    var item = menuItems[random.Next(menuItems.Count)];
                    lines.Add(new OrderLineData
                    {
                        MenuItemId = item.Id,
                        Name = item.Name,
                        Quantity = random.Next(1, 3),
                        UnitPrice = item.Price
                    });
                }

                yield return new OrderData
                {
                    Id = Guid.NewGuid(),
                    SiteId = siteId,
                    ServerId = serverId,
                    Type = orderType,
                    TableNumber = orderType == OrderType.DineIn ? tableNumber : null,
                    GuestCount = guestCount,
                    Lines = lines
                };
            }
        }
    }

    public record OrderData
    {
        public required Guid Id { get; init; }
        public required Guid SiteId { get; init; }
        public required Guid ServerId { get; init; }
        public required OrderType Type { get; init; }
        public string? TableNumber { get; init; }
        public int GuestCount { get; init; }
        public List<OrderLineData> Lines { get; init; } = [];
    }

    public record OrderLineData
    {
        public required Guid MenuItemId { get; init; }
        public required string Name { get; init; }
        public required int Quantity { get; init; }
        public required decimal UnitPrice { get; init; }
        public string? Notes { get; init; }
    }

    #endregion
}
