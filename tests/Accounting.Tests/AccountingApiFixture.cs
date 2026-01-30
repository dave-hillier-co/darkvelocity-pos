using DarkVelocity.Accounting.Api.Data;
using DarkVelocity.Accounting.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Accounting.Tests;

public class AccountingApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    // Test data IDs
    public static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public Guid TestLocationId { get; private set; }
    public Guid TestAccountId { get; private set; }
    public Guid TestJournalEntryId { get; private set; }
    public Guid TestCostCenterId { get; private set; }
    public Guid TestAccountingPeriodId { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // Create SQLite in-memory connection (keep open for test lifetime)
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<AccountingDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public async Task InitializeAsync()
    {
        Client = CreateClient();

        // Seed test data
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AccountingDbContext>();

        await db.Database.EnsureCreatedAsync();

        TestLocationId = Guid.NewGuid();

        // Create test accounts (Chart of Accounts)
        var cashAccount = new Account
        {
            TenantId = TestTenantId,
            AccountCode = "1000",
            Name = "Cash in Drawer",
            AccountType = AccountType.Asset,
            SubType = "Cash",
            IsSystemAccount = false,
            IsActive = true
        };
        db.Accounts.Add(cashAccount);
        TestAccountId = cashAccount.Id;

        var salesAccount = new Account
        {
            TenantId = TestTenantId,
            AccountCode = "4100",
            Name = "Food Sales",
            AccountType = AccountType.Revenue,
            SubType = "Sales",
            TaxCode = "A",
            IsSystemAccount = false,
            IsActive = true
        };
        db.Accounts.Add(salesAccount);

        var vatPayableAccount = new Account
        {
            TenantId = TestTenantId,
            AccountCode = "2100",
            Name = "VAT Payable",
            AccountType = AccountType.Liability,
            SubType = "Tax",
            IsSystemAccount = false,
            IsActive = true
        };
        db.Accounts.Add(vatPayableAccount);

        // Create test cost center
        var costCenter = new CostCenter
        {
            TenantId = TestTenantId,
            Code = "FOH",
            Name = "Front of House",
            IsActive = true
        };
        db.CostCenters.Add(costCenter);
        TestCostCenterId = costCenter.Id;

        // Create test accounting period
        var period = new AccountingPeriod
        {
            TenantId = TestTenantId,
            LocationId = TestLocationId,
            PeriodType = PeriodType.Monthly,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 1, 31),
            Status = PeriodStatus.Open
        };
        db.AccountingPeriods.Add(period);
        TestAccountingPeriodId = period.Id;

        await db.SaveChangesAsync();

        // Create test journal entry
        var journalEntry = new JournalEntry
        {
            TenantId = TestTenantId,
            LocationId = TestLocationId,
            EntryNumber = "JE-2026-00001",
            EntryDate = new DateOnly(2026, 1, 15),
            PostedAt = DateTime.UtcNow,
            SourceType = JournalEntrySourceType.Order,
            Description = "Test order sale",
            TotalDebit = 11.90m,
            TotalCredit = 11.90m,
            Currency = "EUR",
            Status = JournalEntryStatus.Posted,
            AccountingPeriodId = TestAccountingPeriodId
        };

        journalEntry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = journalEntry.Id,
            AccountCode = "1000",
            AccountName = "Cash in Drawer",
            DebitAmount = 11.90m,
            CreditAmount = 0,
            LineNumber = 1
        });

        journalEntry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = journalEntry.Id,
            AccountCode = "4100",
            AccountName = "Food Sales",
            DebitAmount = 0,
            CreditAmount = 10.00m,
            TaxCode = "A",
            TaxAmount = 1.90m,
            LineNumber = 2
        });

        journalEntry.Lines.Add(new JournalEntryLine
        {
            JournalEntryId = journalEntry.Id,
            AccountCode = "2100",
            AccountName = "VAT Payable",
            DebitAmount = 0,
            CreditAmount = 1.90m,
            TaxCode = "A",
            LineNumber = 3
        });

        db.JournalEntries.Add(journalEntry);
        TestJournalEntryId = journalEntry.Id;

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

    public AccountingDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<AccountingDbContext>();
    }
}
