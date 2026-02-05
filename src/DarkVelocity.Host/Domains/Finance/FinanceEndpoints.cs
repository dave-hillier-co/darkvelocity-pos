using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Microsoft.AspNetCore.Mvc;

namespace DarkVelocity.Host.Endpoints;

public static class FinanceEndpoints
{
    public static WebApplication MapFinanceEndpoints(this WebApplication app)
    {
        // Chart of Accounts endpoints
        MapChartOfAccountsEndpoints(app);

        // Journal Entry endpoints
        MapJournalEntryEndpoints(app);

        // Accounting Period endpoints
        MapAccountingPeriodEndpoints(app);

        // Financial Reports endpoints
        MapFinancialReportsEndpoints(app);

        // Bank Reconciliation endpoints
        MapBankReconciliationEndpoints(app);

        return app;
    }

    // ============================================================================
    // Chart of Accounts
    // ============================================================================

    private static void MapChartOfAccountsEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/chart-of-accounts")
            .WithTags("Chart of Accounts");

        // Initialize chart of accounts
        group.MapPost("/initialize", async (
            Guid orgId,
            [FromBody] InitializeChartOfAccountsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChartOfAccountsGrain>(
                GrainKeys.ChartOfAccounts(orgId));

            await grain.InitializeAsync(new InitializeChartOfAccountsCommand(
                orgId,
                request.InitializedBy,
                request.CreateDefaultAccounts,
                request.Currency ?? "USD"));

            var summary = await grain.GetSummaryAsync();
            return Results.Ok(Hal.Resource(summary, BuildChartOfAccountsLinks(orgId)));
        });

        // Get chart summary
        group.MapGet("/", async (
            Guid orgId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChartOfAccountsGrain>(
                GrainKeys.ChartOfAccounts(orgId));

            if (!await grain.IsInitializedAsync())
                return Results.NotFound(Hal.Error("not_found", "Chart of accounts not initialized"));

            var summary = await grain.GetSummaryAsync();
            return Results.Ok(Hal.Resource(summary, BuildChartOfAccountsLinks(orgId)));
        });

        // Get all accounts
        group.MapGet("/accounts", async (
            Guid orgId,
            [FromQuery] bool includeInactive,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChartOfAccountsGrain>(
                GrainKeys.ChartOfAccounts(orgId));

            var accounts = await grain.GetAllAccountsAsync(includeInactive);
            return Results.Ok(Hal.Collection(
                $"/api/orgs/{orgId}/chart-of-accounts/accounts",
                accounts.Cast<object>(),
                accounts.Count));
        });

        // Get hierarchical view
        group.MapGet("/hierarchy", async (
            Guid orgId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChartOfAccountsGrain>(
                GrainKeys.ChartOfAccounts(orgId));

            var hierarchy = await grain.GetHierarchyAsync();
            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/chart-of-accounts/hierarchy" } },
                hierarchy
            });
        });

        // Get accounts by type
        group.MapGet("/accounts/by-type/{accountType}", async (
            Guid orgId,
            AccountType accountType,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChartOfAccountsGrain>(
                GrainKeys.ChartOfAccounts(orgId));

            var accounts = await grain.GetAccountsByTypeAsync(accountType);
            return Results.Ok(Hal.Collection(
                $"/api/orgs/{orgId}/chart-of-accounts/accounts/by-type/{accountType}",
                accounts.Cast<object>(),
                accounts.Count));
        });

        // Get single account
        group.MapGet("/accounts/{accountNumber}", async (
            Guid orgId,
            string accountNumber,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChartOfAccountsGrain>(
                GrainKeys.ChartOfAccounts(orgId));

            var account = await grain.GetAccountAsync(accountNumber);
            if (account == null)
                return Results.NotFound(Hal.Error("not_found", $"Account {accountNumber} not found"));

            return Results.Ok(Hal.Resource(account, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/chart-of-accounts/accounts/{accountNumber}" },
                ["collection"] = new { href = $"/api/orgs/{orgId}/chart-of-accounts/accounts" }
            }));
        });

        // Add account
        group.MapPost("/accounts", async (
            Guid orgId,
            [FromBody] AddAccountRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChartOfAccountsGrain>(
                GrainKeys.ChartOfAccounts(orgId));

            var account = await grain.AddAccountAsync(new AddAccountToChartCommand(
                request.AccountNumber,
                request.Name,
                request.AccountType,
                request.NormalBalance,
                request.AddedBy,
                request.ParentAccountNumber,
                request.Description,
                request.IsSystemAccount,
                request.TaxCode));

            return Results.Created(
                $"/api/orgs/{orgId}/chart-of-accounts/accounts/{account.AccountNumber}",
                Hal.Resource(account, new Dictionary<string, object>
                {
                    ["self"] = new { href = $"/api/orgs/{orgId}/chart-of-accounts/accounts/{account.AccountNumber}" }
                }));
        });

        // Update account
        group.MapPatch("/accounts/{accountNumber}", async (
            Guid orgId,
            string accountNumber,
            [FromBody] UpdateChartAccountRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChartOfAccountsGrain>(
                GrainKeys.ChartOfAccounts(orgId));

            var account = await grain.UpdateAccountAsync(new UpdateChartAccountCommand(
                accountNumber,
                request.UpdatedBy,
                request.Name,
                request.Description,
                request.ParentAccountNumber,
                request.TaxCode));

            return Results.Ok(Hal.Resource(account, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/chart-of-accounts/accounts/{accountNumber}" }
            }));
        });

        // Deactivate account
        group.MapDelete("/accounts/{accountNumber}", async (
            Guid orgId,
            string accountNumber,
            [FromQuery] Guid deactivatedBy,
            [FromQuery] string? reason,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChartOfAccountsGrain>(
                GrainKeys.ChartOfAccounts(orgId));

            await grain.DeactivateAccountAsync(new DeactivateChartAccountCommand(
                accountNumber,
                deactivatedBy,
                reason));

            return Results.NoContent();
        });

        // Reactivate account
        group.MapPost("/accounts/{accountNumber}/reactivate", async (
            Guid orgId,
            string accountNumber,
            [FromBody] ReactivateAccountRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IChartOfAccountsGrain>(
                GrainKeys.ChartOfAccounts(orgId));

            var account = await grain.ReactivateAccountAsync(new ReactivateChartAccountCommand(
                accountNumber,
                request.ReactivatedBy));

            return Results.Ok(Hal.Resource(account, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/chart-of-accounts/accounts/{accountNumber}" }
            }));
        });
    }

    // ============================================================================
    // Journal Entries
    // ============================================================================

    private static void MapJournalEntryEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/journal-entries")
            .WithTags("Journal Entries");

        // Create journal entry
        group.MapPost("/", async (
            Guid orgId,
            [FromBody] CreateJournalEntryRequest request,
            IGrainFactory grainFactory) =>
        {
            var journalEntryId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IJournalEntryGrain>(
                GrainKeys.JournalEntry(orgId, journalEntryId));

            var lines = request.Lines.Select(l => new JournalEntryLineCommand(
                l.AccountNumber,
                l.DebitAmount,
                l.CreditAmount,
                l.Description,
                l.CostCenterId,
                l.TaxCode)).ToList();

            var entry = await grain.CreateAsync(new CreateJournalEntryCommand(
                orgId,
                journalEntryId,
                request.PostingDate,
                lines,
                request.CreatedBy,
                request.Memo,
                request.EffectiveDate,
                request.ReferenceNumber,
                request.ReferenceType,
                request.ReferenceId,
                request.AutoPost,
                request.IsReversing,
                request.ReversalDate));

            return Results.Created(
                $"/api/orgs/{orgId}/journal-entries/{journalEntryId}",
                Hal.Resource(entry, BuildJournalEntryLinks(orgId, journalEntryId)));
        });

        // Get journal entry
        group.MapGet("/{journalEntryId}", async (
            Guid orgId,
            Guid journalEntryId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IJournalEntryGrain>(
                GrainKeys.JournalEntry(orgId, journalEntryId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Journal entry not found"));

            var entry = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(entry, BuildJournalEntryLinks(orgId, journalEntryId)));
        });

        // Post journal entry
        group.MapPost("/{journalEntryId}/post", async (
            Guid orgId,
            Guid journalEntryId,
            [FromBody] PostJournalEntryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IJournalEntryGrain>(
                GrainKeys.JournalEntry(orgId, journalEntryId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Journal entry not found"));

            var entry = await grain.PostAsync(new PostJournalEntryCommand(
                request.PostedBy,
                request.Notes));

            return Results.Ok(Hal.Resource(entry, BuildJournalEntryLinks(orgId, journalEntryId)));
        });

        // Approve journal entry
        group.MapPost("/{journalEntryId}/approve", async (
            Guid orgId,
            Guid journalEntryId,
            [FromBody] ApproveJournalEntryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IJournalEntryGrain>(
                GrainKeys.JournalEntry(orgId, journalEntryId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Journal entry not found"));

            var entry = await grain.ApproveAsync(new ApproveJournalEntryCommand(
                request.ApprovedBy,
                request.Notes));

            return Results.Ok(Hal.Resource(entry, BuildJournalEntryLinks(orgId, journalEntryId)));
        });

        // Reject journal entry
        group.MapPost("/{journalEntryId}/reject", async (
            Guid orgId,
            Guid journalEntryId,
            [FromBody] RejectJournalEntryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IJournalEntryGrain>(
                GrainKeys.JournalEntry(orgId, journalEntryId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Journal entry not found"));

            var entry = await grain.RejectAsync(new RejectJournalEntryCommand(
                request.RejectedBy,
                request.Reason));

            return Results.Ok(Hal.Resource(entry, BuildJournalEntryLinks(orgId, journalEntryId)));
        });

        // Reverse journal entry
        group.MapPost("/{journalEntryId}/reverse", async (
            Guid orgId,
            Guid journalEntryId,
            [FromBody] ReverseJournalEntryRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IJournalEntryGrain>(
                GrainKeys.JournalEntry(orgId, journalEntryId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Journal entry not found"));

            var entry = await grain.ReverseAsync(new ReverseJournalEntryCommand(
                request.ReversedBy,
                request.ReversalDate,
                request.Reason));

            return Results.Ok(Hal.Resource(entry, BuildJournalEntryLinks(orgId, journalEntryId)));
        });

        // Void journal entry
        group.MapDelete("/{journalEntryId}", async (
            Guid orgId,
            Guid journalEntryId,
            [FromQuery] Guid voidedBy,
            [FromQuery] string reason,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IJournalEntryGrain>(
                GrainKeys.JournalEntry(orgId, journalEntryId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Journal entry not found"));

            await grain.VoidAsync(new VoidJournalEntryCommand(voidedBy, reason));
            return Results.NoContent();
        });
    }

    // ============================================================================
    // Accounting Periods
    // ============================================================================

    private static void MapAccountingPeriodEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/accounting-periods")
            .WithTags("Accounting Periods");

        // Initialize fiscal year
        group.MapPost("/{year}/initialize", async (
            Guid orgId,
            int year,
            [FromBody] InitializeFiscalYearRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IAccountingPeriodGrain>(
                GrainKeys.AccountingPeriod(orgId, year));

            await grain.InitializeAsync(new InitializeFiscalYearCommand(
                orgId,
                year,
                request.InitializedBy,
                request.FiscalYearStartMonth,
                request.Frequency));

            var summary = await grain.GetSummaryAsync();
            return Results.Ok(Hal.Resource(summary, BuildAccountingPeriodLinks(orgId, year)));
        });

        // Get fiscal year summary
        group.MapGet("/{year}", async (
            Guid orgId,
            int year,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IAccountingPeriodGrain>(
                GrainKeys.AccountingPeriod(orgId, year));

            if (!await grain.IsInitializedAsync())
                return Results.NotFound(Hal.Error("not_found", $"Fiscal year {year} not initialized"));

            var summary = await grain.GetSummaryAsync();
            return Results.Ok(Hal.Resource(summary, BuildAccountingPeriodLinks(orgId, year)));
        });

        // Get all periods for a year
        group.MapGet("/{year}/periods", async (
            Guid orgId,
            int year,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IAccountingPeriodGrain>(
                GrainKeys.AccountingPeriod(orgId, year));

            var periods = await grain.GetAllPeriodsAsync();
            return Results.Ok(Hal.Collection(
                $"/api/orgs/{orgId}/accounting-periods/{year}/periods",
                periods.Cast<object>(),
                periods.Count));
        });

        // Open period
        group.MapPost("/{year}/periods/{periodNumber}/open", async (
            Guid orgId,
            int year,
            int periodNumber,
            [FromBody] OpenPeriodRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IAccountingPeriodGrain>(
                GrainKeys.AccountingPeriod(orgId, year));

            var period = await grain.OpenPeriodAsync(new OpenPeriodCommand(
                periodNumber,
                request.OpenedBy,
                request.Notes));

            return Results.Ok(Hal.Resource(period, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/accounting-periods/{year}/periods/{periodNumber}" }
            }));
        });

        // Close period
        group.MapPost("/{year}/periods/{periodNumber}/close", async (
            Guid orgId,
            int year,
            int periodNumber,
            [FromBody] ClosePeriodRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IAccountingPeriodGrain>(
                GrainKeys.AccountingPeriod(orgId, year));

            var period = await grain.ClosePeriodAsync(new ClosePeriodCommand2(
                periodNumber,
                request.ClosedBy,
                request.Notes,
                request.Force));

            return Results.Ok(Hal.Resource(period, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/accounting-periods/{year}/periods/{periodNumber}" }
            }));
        });

        // Reopen period
        group.MapPost("/{year}/periods/{periodNumber}/reopen", async (
            Guid orgId,
            int year,
            int periodNumber,
            [FromBody] ReopenPeriodRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IAccountingPeriodGrain>(
                GrainKeys.AccountingPeriod(orgId, year));

            var period = await grain.ReopenPeriodAsync(new ReopenPeriodCommand(
                periodNumber,
                request.ReopenedBy,
                request.Reason));

            return Results.Ok(Hal.Resource(period, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/accounting-periods/{year}/periods/{periodNumber}" }
            }));
        });

        // Lock period
        group.MapPost("/{year}/periods/{periodNumber}/lock", async (
            Guid orgId,
            int year,
            int periodNumber,
            [FromBody] LockPeriodRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IAccountingPeriodGrain>(
                GrainKeys.AccountingPeriod(orgId, year));

            var period = await grain.LockPeriodAsync(new LockPeriodCommand(
                periodNumber,
                request.LockedBy,
                request.Reason));

            return Results.Ok(Hal.Resource(period, new Dictionary<string, object>
            {
                ["self"] = new { href = $"/api/orgs/{orgId}/accounting-periods/{year}/periods/{periodNumber}" }
            }));
        });

        // Year-end close
        group.MapPost("/{year}/year-end-close", async (
            Guid orgId,
            int year,
            [FromBody] YearEndCloseRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IAccountingPeriodGrain>(
                GrainKeys.AccountingPeriod(orgId, year));

            var summary = await grain.YearEndCloseAsync(new YearEndCloseCommand(
                request.ClosedBy,
                request.RetainedEarningsAccountNumber,
                request.Notes));

            return Results.Ok(Hal.Resource(summary, BuildAccountingPeriodLinks(orgId, year)));
        });
    }

    // ============================================================================
    // Financial Reports
    // ============================================================================

    private static void MapFinancialReportsEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/financial-reports")
            .WithTags("Financial Reports");

        // Trial Balance
        group.MapGet("/trial-balance", async (
            Guid orgId,
            [FromQuery] DateOnly asOfDate,
            [FromQuery] bool includeInactiveAccounts,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFinancialReportsGrain>(
                GrainKeys.FinancialReports(orgId));

            var report = await grain.GenerateTrialBalanceAsync(new TrialBalanceRequest(
                asOfDate,
                includeInactiveAccounts));

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/financial-reports/trial-balance" } },
                report
            });
        });

        // Income Statement (P&L)
        group.MapGet("/income-statement", async (
            Guid orgId,
            [FromQuery] DateOnly fromDate,
            [FromQuery] DateOnly toDate,
            [FromQuery] bool includeComparison,
            [FromQuery] DateOnly? comparisonFromDate,
            [FromQuery] DateOnly? comparisonToDate,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFinancialReportsGrain>(
                GrainKeys.FinancialReports(orgId));

            var report = await grain.GenerateIncomeStatementAsync(new IncomeStatementRequest(
                fromDate,
                toDate,
                includeComparison,
                comparisonFromDate,
                comparisonToDate));

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/financial-reports/income-statement" } },
                report
            });
        });

        // Balance Sheet
        group.MapGet("/balance-sheet", async (
            Guid orgId,
            [FromQuery] DateOnly asOfDate,
            [FromQuery] bool includeComparison,
            [FromQuery] DateOnly? comparisonAsOfDate,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFinancialReportsGrain>(
                GrainKeys.FinancialReports(orgId));

            var report = await grain.GenerateBalanceSheetAsync(new BalanceSheetRequest(
                asOfDate,
                includeComparison,
                comparisonAsOfDate));

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/financial-reports/balance-sheet" } },
                report
            });
        });

        // Cash Flow Statement
        group.MapGet("/cash-flow", async (
            Guid orgId,
            [FromQuery] DateOnly fromDate,
            [FromQuery] DateOnly toDate,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IFinancialReportsGrain>(
                GrainKeys.FinancialReports(orgId));

            var report = await grain.GenerateCashFlowStatementAsync(new CashFlowStatementRequest(
                fromDate,
                toDate));

            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/financial-reports/cash-flow" } },
                report
            });
        });
    }

    // ============================================================================
    // Bank Reconciliation
    // ============================================================================

    private static void MapBankReconciliationEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/orgs/{orgId}/reconciliations")
            .WithTags("Bank Reconciliation");

        // Start reconciliation
        group.MapPost("/", async (
            Guid orgId,
            [FromBody] StartReconciliationRequest request,
            IGrainFactory grainFactory) =>
        {
            var reconciliationId = Guid.NewGuid();
            var grain = grainFactory.GetGrain<IBankReconciliationGrain>(
                GrainKeys.BankReconciliation(orgId, reconciliationId));

            var summary = await grain.StartAsync(new StartReconciliationCommand(
                orgId,
                reconciliationId,
                request.BankAccountNumber,
                request.BankAccountName,
                request.StatementDate,
                request.StatementEndingBalance,
                request.StartedBy,
                request.StatementReference,
                request.StatementStartDate,
                request.StatementStartingBalance));

            return Results.Created(
                $"/api/orgs/{orgId}/reconciliations/{reconciliationId}",
                Hal.Resource(summary, BuildReconciliationLinks(orgId, reconciliationId)));
        });

        // Get reconciliation
        group.MapGet("/{reconciliationId}", async (
            Guid orgId,
            Guid reconciliationId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBankReconciliationGrain>(
                GrainKeys.BankReconciliation(orgId, reconciliationId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reconciliation not found"));

            var snapshot = await grain.GetSnapshotAsync();
            return Results.Ok(Hal.Resource(snapshot, BuildReconciliationLinks(orgId, reconciliationId)));
        });

        // Import bank transactions
        group.MapPost("/{reconciliationId}/transactions", async (
            Guid orgId,
            Guid reconciliationId,
            [FromBody] ImportBankTransactionsRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBankReconciliationGrain>(
                GrainKeys.BankReconciliation(orgId, reconciliationId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reconciliation not found"));

            var transactions = request.Transactions.Select(t => new BankTransactionImport(
                t.TransactionId,
                t.TransactionDate,
                t.Amount,
                t.Description,
                t.CheckNumber,
                t.ReferenceNumber,
                t.TransactionType)).ToList();

            var summary = await grain.ImportTransactionsAsync(new ImportBankTransactionsCommand(
                transactions,
                request.ImportedBy,
                request.ImportSource));

            return Results.Ok(Hal.Resource(summary, BuildReconciliationLinks(orgId, reconciliationId)));
        });

        // Get unmatched transactions
        group.MapGet("/{reconciliationId}/transactions/unmatched", async (
            Guid orgId,
            Guid reconciliationId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBankReconciliationGrain>(
                GrainKeys.BankReconciliation(orgId, reconciliationId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reconciliation not found"));

            var transactions = await grain.GetUnmatchedTransactionsAsync();
            return Results.Ok(Hal.Collection(
                $"/api/orgs/{orgId}/reconciliations/{reconciliationId}/transactions/unmatched",
                transactions.Cast<object>(),
                transactions.Count));
        });

        // Match transaction
        group.MapPost("/{reconciliationId}/transactions/{transactionId}/match", async (
            Guid orgId,
            Guid reconciliationId,
            string transactionId,
            [FromBody] MatchTransactionRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBankReconciliationGrain>(
                GrainKeys.BankReconciliation(orgId, reconciliationId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reconciliation not found"));

            await grain.MatchTransactionAsync(new MatchTransactionCommand(
                transactionId,
                request.JournalEntryId,
                request.MatchedBy,
                request.Notes));

            var summary = await grain.GetSummaryAsync();
            return Results.Ok(Hal.Resource(summary, BuildReconciliationLinks(orgId, reconciliationId)));
        });

        // Unmatch transaction
        group.MapPost("/{reconciliationId}/transactions/{transactionId}/unmatch", async (
            Guid orgId,
            Guid reconciliationId,
            string transactionId,
            [FromBody] UnmatchTransactionRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBankReconciliationGrain>(
                GrainKeys.BankReconciliation(orgId, reconciliationId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reconciliation not found"));

            await grain.UnmatchTransactionAsync(new UnmatchTransactionCommand(
                transactionId,
                request.UnmatchedBy,
                request.Reason));

            var summary = await grain.GetSummaryAsync();
            return Results.Ok(Hal.Resource(summary, BuildReconciliationLinks(orgId, reconciliationId)));
        });

        // Suggest matches for transaction
        group.MapGet("/{reconciliationId}/transactions/{transactionId}/suggestions", async (
            Guid orgId,
            Guid reconciliationId,
            string transactionId,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBankReconciliationGrain>(
                GrainKeys.BankReconciliation(orgId, reconciliationId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reconciliation not found"));

            var suggestions = await grain.SuggestMatchesAsync(transactionId);
            return Results.Ok(new
            {
                _links = new { self = new { href = $"/api/orgs/{orgId}/reconciliations/{reconciliationId}/transactions/{transactionId}/suggestions" } },
                suggestions
            });
        });

        // Complete reconciliation
        group.MapPost("/{reconciliationId}/complete", async (
            Guid orgId,
            Guid reconciliationId,
            [FromBody] CompleteReconciliationRequest request,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBankReconciliationGrain>(
                GrainKeys.BankReconciliation(orgId, reconciliationId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reconciliation not found"));

            var summary = await grain.CompleteAsync(new CompleteReconciliationCommand(
                request.CompletedBy,
                request.Notes,
                request.ForceComplete));

            return Results.Ok(Hal.Resource(summary, BuildReconciliationLinks(orgId, reconciliationId)));
        });

        // Void reconciliation
        group.MapDelete("/{reconciliationId}", async (
            Guid orgId,
            Guid reconciliationId,
            [FromQuery] Guid voidedBy,
            [FromQuery] string reason,
            IGrainFactory grainFactory) =>
        {
            var grain = grainFactory.GetGrain<IBankReconciliationGrain>(
                GrainKeys.BankReconciliation(orgId, reconciliationId));

            if (!await grain.ExistsAsync())
                return Results.NotFound(Hal.Error("not_found", "Reconciliation not found"));

            await grain.VoidAsync(new VoidReconciliationCommand(voidedBy, reason));
            return Results.NoContent();
        });
    }

    // ============================================================================
    // Link Builders
    // ============================================================================

    private static Dictionary<string, object> BuildChartOfAccountsLinks(Guid orgId)
    {
        var basePath = $"/api/orgs/{orgId}/chart-of-accounts";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["accounts"] = new { href = $"{basePath}/accounts" },
            ["hierarchy"] = new { href = $"{basePath}/hierarchy" }
        };
    }

    private static Dictionary<string, object> BuildJournalEntryLinks(Guid orgId, Guid journalEntryId)
    {
        var basePath = $"/api/orgs/{orgId}/journal-entries/{journalEntryId}";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["post"] = new { href = $"{basePath}/post" },
            ["approve"] = new { href = $"{basePath}/approve" },
            ["reject"] = new { href = $"{basePath}/reject" },
            ["reverse"] = new { href = $"{basePath}/reverse" },
            ["collection"] = new { href = $"/api/orgs/{orgId}/journal-entries" }
        };
    }

    private static Dictionary<string, object> BuildAccountingPeriodLinks(Guid orgId, int year)
    {
        var basePath = $"/api/orgs/{orgId}/accounting-periods/{year}";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["periods"] = new { href = $"{basePath}/periods" },
            ["yearEndClose"] = new { href = $"{basePath}/year-end-close" }
        };
    }

    private static Dictionary<string, object> BuildReconciliationLinks(Guid orgId, Guid reconciliationId)
    {
        var basePath = $"/api/orgs/{orgId}/reconciliations/{reconciliationId}";
        return new Dictionary<string, object>
        {
            ["self"] = new { href = basePath },
            ["transactions"] = new { href = $"{basePath}/transactions" },
            ["unmatched"] = new { href = $"{basePath}/transactions/unmatched" },
            ["complete"] = new { href = $"{basePath}/complete" },
            ["collection"] = new { href = $"/api/orgs/{orgId}/reconciliations" }
        };
    }
}

// ============================================================================
// Request DTOs
// ============================================================================

public record InitializeChartOfAccountsRequest
{
    public required Guid InitializedBy { get; init; }
    public bool CreateDefaultAccounts { get; init; } = true;
    public string? Currency { get; init; }
}

public record AddAccountRequest
{
    public required string AccountNumber { get; init; }
    public required string Name { get; init; }
    public required AccountType AccountType { get; init; }
    public required NormalBalance NormalBalance { get; init; }
    public required Guid AddedBy { get; init; }
    public string? ParentAccountNumber { get; init; }
    public string? Description { get; init; }
    public bool IsSystemAccount { get; init; }
    public string? TaxCode { get; init; }
}

public record UpdateChartAccountRequest
{
    public required Guid UpdatedBy { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? ParentAccountNumber { get; init; }
    public string? TaxCode { get; init; }
}

public record ReactivateAccountRequest
{
    public required Guid ReactivatedBy { get; init; }
}

public record CreateJournalEntryRequest
{
    public required DateOnly PostingDate { get; init; }
    public required IReadOnlyList<JournalEntryLineRequest> Lines { get; init; }
    public required Guid CreatedBy { get; init; }
    public string? Memo { get; init; }
    public DateOnly? EffectiveDate { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? ReferenceType { get; init; }
    public Guid? ReferenceId { get; init; }
    public bool AutoPost { get; init; }
    public bool IsReversing { get; init; }
    public DateOnly? ReversalDate { get; init; }
}

public record JournalEntryLineRequest
{
    public required string AccountNumber { get; init; }
    public decimal DebitAmount { get; init; }
    public decimal CreditAmount { get; init; }
    public string? Description { get; init; }
    public Guid? CostCenterId { get; init; }
    public string? TaxCode { get; init; }
}

public record PostJournalEntryRequest
{
    public required Guid PostedBy { get; init; }
    public string? Notes { get; init; }
}

public record ApproveJournalEntryRequest
{
    public required Guid ApprovedBy { get; init; }
    public string? Notes { get; init; }
}

public record RejectJournalEntryRequest
{
    public required Guid RejectedBy { get; init; }
    public required string Reason { get; init; }
}

public record ReverseJournalEntryRequest
{
    public required Guid ReversedBy { get; init; }
    public required DateOnly ReversalDate { get; init; }
    public string? Reason { get; init; }
}

public record InitializeFiscalYearRequest
{
    public required Guid InitializedBy { get; init; }
    public int FiscalYearStartMonth { get; init; } = 1;
    public PeriodFrequency Frequency { get; init; } = PeriodFrequency.Monthly;
}

public record OpenPeriodRequest
{
    public required Guid OpenedBy { get; init; }
    public string? Notes { get; init; }
}

public record ClosePeriodRequest
{
    public required Guid ClosedBy { get; init; }
    public string? Notes { get; init; }
    public bool Force { get; init; }
}

public record ReopenPeriodRequest
{
    public required Guid ReopenedBy { get; init; }
    public required string Reason { get; init; }
}

public record LockPeriodRequest
{
    public required Guid LockedBy { get; init; }
    public string? Reason { get; init; }
}

public record YearEndCloseRequest
{
    public required Guid ClosedBy { get; init; }
    public required string RetainedEarningsAccountNumber { get; init; }
    public string? Notes { get; init; }
}

public record StartReconciliationRequest
{
    public required string BankAccountNumber { get; init; }
    public required string BankAccountName { get; init; }
    public required DateOnly StatementDate { get; init; }
    public required decimal StatementEndingBalance { get; init; }
    public required Guid StartedBy { get; init; }
    public string? StatementReference { get; init; }
    public DateOnly? StatementStartDate { get; init; }
    public decimal? StatementStartingBalance { get; init; }
}

public record ImportBankTransactionsRequest
{
    public required IReadOnlyList<BankTransactionRequest> Transactions { get; init; }
    public required Guid ImportedBy { get; init; }
    public string? ImportSource { get; init; }
}

public record BankTransactionRequest
{
    public required string TransactionId { get; init; }
    public required DateOnly TransactionDate { get; init; }
    public required decimal Amount { get; init; }
    public required string Description { get; init; }
    public string? CheckNumber { get; init; }
    public string? ReferenceNumber { get; init; }
    public string? TransactionType { get; init; }
}

public record MatchTransactionRequest
{
    public required Guid JournalEntryId { get; init; }
    public required Guid MatchedBy { get; init; }
    public string? Notes { get; init; }
}

public record UnmatchTransactionRequest
{
    public required Guid UnmatchedBy { get; init; }
    public string? Reason { get; init; }
}

public record CompleteReconciliationRequest
{
    public required Guid CompletedBy { get; init; }
    public string? Notes { get; init; }
    public bool ForceComplete { get; init; }
}
