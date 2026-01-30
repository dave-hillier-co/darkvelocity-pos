using DarkVelocity.GiftCards.Api.Data;
using DarkVelocity.GiftCards.Api.Dtos;
using DarkVelocity.GiftCards.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.GiftCards.Api.Controllers;

[ApiController]
[Route("api/giftcard-reports")]
public class GiftCardReportsController : ControllerBase
{
    private readonly GiftCardsDbContext _context;

    public GiftCardReportsController(GiftCardsDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get gift card liability report (outstanding balances)
    /// </summary>
    [HttpGet("liability")]
    public async Task<ActionResult<GiftCardLiabilityReportDto>> GetLiabilityReport(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? locationId = null)
    {
        var now = DateTime.UtcNow;

        var query = _context.GiftCards
            .Include(g => g.Program)
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(g => g.TenantId == tenantId.Value);

        if (locationId.HasValue)
            query = query.Where(g => g.LocationId == locationId.Value);

        // Get all cards with their status
        var cards = await query.ToListAsync();

        // Calculate totals by status
        var activeCards = cards.Where(c => c.Status == "active").ToList();
        var suspendedCards = cards.Where(c => c.Status == "suspended").ToList();
        var expiredCards = cards.Where(c => c.Status == "expired" || (c.ExpiryDate.HasValue && c.ExpiryDate.Value < now)).ToList();
        var depletedCards = cards.Where(c => c.Status == "depleted").ToList();

        var totalOutstanding = activeCards.Sum(c => c.CurrentBalance) + suspendedCards.Sum(c => c.CurrentBalance);

        // Determine currency (use most common or first found)
        var currency = cards.FirstOrDefault()?.CurrencyCode ?? "EUR";

        // Group by program
        var byProgram = cards
            .Where(c => c.Status == "active" || c.Status == "suspended")
            .GroupBy(c => new { c.ProgramId, c.Program?.Name })
            .Select(g => new ProgramLiabilityDto
            {
                ProgramId = g.Key.ProgramId,
                ProgramName = g.Key.Name ?? "Unknown",
                OutstandingBalance = g.Sum(c => c.CurrentBalance),
                ActiveCardsCount = g.Count(c => c.Status == "active")
            })
            .OrderByDescending(p => p.OutstandingBalance)
            .ToList();

        // Group by age (based on issued date)
        var thirtyDaysAgo = now.AddDays(-30);
        var sixtyDaysAgo = now.AddDays(-60);
        var ninetyDaysAgo = now.AddDays(-90);

        var activeAndSuspended = cards.Where(c => c.Status == "active" || c.Status == "suspended").ToList();

        var byAge = new List<AgeBucketLiabilityDto>
        {
            new()
            {
                Bucket = "0-30 days",
                OutstandingBalance = activeAndSuspended.Where(c => c.IssuedAt >= thirtyDaysAgo).Sum(c => c.CurrentBalance),
                CardsCount = activeAndSuspended.Count(c => c.IssuedAt >= thirtyDaysAgo)
            },
            new()
            {
                Bucket = "30-60 days",
                OutstandingBalance = activeAndSuspended.Where(c => c.IssuedAt >= sixtyDaysAgo && c.IssuedAt < thirtyDaysAgo).Sum(c => c.CurrentBalance),
                CardsCount = activeAndSuspended.Count(c => c.IssuedAt >= sixtyDaysAgo && c.IssuedAt < thirtyDaysAgo)
            },
            new()
            {
                Bucket = "60-90 days",
                OutstandingBalance = activeAndSuspended.Where(c => c.IssuedAt >= ninetyDaysAgo && c.IssuedAt < sixtyDaysAgo).Sum(c => c.CurrentBalance),
                CardsCount = activeAndSuspended.Count(c => c.IssuedAt >= ninetyDaysAgo && c.IssuedAt < sixtyDaysAgo)
            },
            new()
            {
                Bucket = "90+ days",
                OutstandingBalance = activeAndSuspended.Where(c => c.IssuedAt < ninetyDaysAgo).Sum(c => c.CurrentBalance),
                CardsCount = activeAndSuspended.Count(c => c.IssuedAt < ninetyDaysAgo)
            }
        };

        var dto = new GiftCardLiabilityReportDto
        {
            AsOfDate = now,
            TotalOutstandingBalance = totalOutstanding,
            CurrencyCode = currency,
            TotalActiveCards = activeCards.Count,
            TotalSuspendedCards = suspendedCards.Count,
            TotalExpiredCards = expiredCards.Count,
            TotalDepletedCards = depletedCards.Count,
            ByProgram = byProgram,
            ByAge = byAge
        };

        dto.AddSelfLink("/api/giftcard-reports/liability");

        return Ok(dto);
    }

    /// <summary>
    /// Get gift card activity report
    /// </summary>
    [HttpGet("activity")]
    public async Task<ActionResult<GiftCardActivityReportDto>> GetActivityReport(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var end = endDate ?? DateTime.UtcNow;
        var start = startDate ?? end.AddDays(-30);

        var transactionQuery = _context.GiftCardTransactions
            .Where(t => t.ProcessedAt >= start && t.ProcessedAt <= end);

        var cardQuery = _context.GiftCards
            .Where(c => c.IssuedAt >= start && c.IssuedAt <= end);

        if (locationId.HasValue)
        {
            transactionQuery = transactionQuery.Where(t => t.LocationId == locationId.Value);
            cardQuery = cardQuery.Where(c => c.LocationId == locationId.Value);
        }

        if (tenantId.HasValue)
        {
            cardQuery = cardQuery.Where(c => c.TenantId == tenantId.Value);
            // Need to join through GiftCard for tenant filtering on transactions
            var cardIds = await _context.GiftCards
                .Where(c => c.TenantId == tenantId.Value)
                .Select(c => c.Id)
                .ToListAsync();
            transactionQuery = transactionQuery.Where(t => cardIds.Contains(t.GiftCardId));
        }

        var transactions = await transactionQuery.ToListAsync();
        var cardsIssued = await cardQuery.ToListAsync();

        // Determine currency
        var currency = cardsIssued.FirstOrDefault()?.CurrencyCode ?? "EUR";

        // Calculate summaries
        var activations = transactions.Where(t => t.TransactionType == "activation").ToList();
        var redemptions = transactions.Where(t => t.TransactionType == "redemption").ToList();
        var reloads = transactions.Where(t => t.TransactionType == "reload").ToList();
        var expirations = transactions.Where(t => t.TransactionType == "expiry").ToList();

        // Daily breakdown
        var dailyData = transactions
            .GroupBy(t => DateOnly.FromDateTime(t.ProcessedAt))
            .Select(g => new DailyActivityDto
            {
                Date = g.Key,
                CardsIssued = cardsIssued.Count(c => DateOnly.FromDateTime(c.IssuedAt) == g.Key),
                ActivationAmount = g.Where(t => t.TransactionType == "activation").Sum(t => t.Amount),
                RedemptionCount = g.Count(t => t.TransactionType == "redemption"),
                RedemptionAmount = Math.Abs(g.Where(t => t.TransactionType == "redemption").Sum(t => t.Amount)),
                ReloadCount = g.Count(t => t.TransactionType == "reload"),
                ReloadAmount = g.Where(t => t.TransactionType == "reload").Sum(t => t.Amount)
            })
            .OrderBy(d => d.Date)
            .ToList();

        var dto = new GiftCardActivityReportDto
        {
            StartDate = start,
            EndDate = end,
            CurrencyCode = currency,
            CardsIssued = cardsIssued.Count,
            CardsActivated = activations.Count,
            TotalActivationAmount = activations.Sum(t => t.Amount),
            RedemptionCount = redemptions.Count,
            TotalRedemptionAmount = Math.Abs(redemptions.Sum(t => t.Amount)),
            ReloadCount = reloads.Count,
            TotalReloadAmount = reloads.Sum(t => t.Amount),
            CardsExpired = expirations.Count,
            ExpiredAmount = Math.Abs(expirations.Sum(t => t.Amount)),
            DailyActivity = dailyData
        };

        dto.AddSelfLink("/api/giftcard-reports/activity");

        return Ok(dto);
    }

    /// <summary>
    /// Get expiring cards report
    /// </summary>
    [HttpGet("expiring")]
    public async Task<ActionResult<ExpiringCardsReportDto>> GetExpiringCardsReport(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] int limit = 100)
    {
        var now = DateTime.UtcNow;
        var thirtyDays = now.AddDays(30);
        var sixtyDays = now.AddDays(60);
        var ninetyDays = now.AddDays(90);

        var query = _context.GiftCards
            .Include(g => g.Program)
            .Where(g => g.Status == "active" && g.ExpiryDate.HasValue && g.ExpiryDate <= ninetyDays);

        if (tenantId.HasValue)
            query = query.Where(g => g.TenantId == tenantId.Value);

        if (locationId.HasValue)
            query = query.Where(g => g.LocationId == locationId.Value);

        var expiringCards = await query
            .OrderBy(g => g.ExpiryDate)
            .ToListAsync();

        var currency = expiringCards.FirstOrDefault()?.CurrencyCode ?? "EUR";

        var expiring30 = expiringCards.Where(c => c.ExpiryDate <= thirtyDays).ToList();
        var expiring60 = expiringCards.Where(c => c.ExpiryDate <= sixtyDays).ToList();
        var expiring90 = expiringCards;

        var dto = new ExpiringCardsReportDto
        {
            AsOfDate = now,
            CurrencyCode = currency,
            ExpiringInNext30Days = expiring30.Count,
            ExpiringIn30DaysAmount = expiring30.Sum(c => c.CurrentBalance),
            ExpiringInNext60Days = expiring60.Count,
            ExpiringIn60DaysAmount = expiring60.Sum(c => c.CurrentBalance),
            ExpiringInNext90Days = expiring90.Count,
            ExpiringIn90DaysAmount = expiring90.Sum(c => c.CurrentBalance),
            ExpiringCards = expiringCards
                .Take(limit)
                .Select(c => new GiftCardSummaryDto
                {
                    Id = c.Id,
                    MaskedCardNumber = CardNumberHelper.MaskCardNumber(c.CardNumber),
                    CardType = c.CardType,
                    CurrentBalance = c.CurrentBalance,
                    CurrencyCode = c.CurrencyCode,
                    Status = c.Status,
                    ExpiryDate = c.ExpiryDate,
                    IsExpired = false,
                    LastUsedAt = c.LastUsedAt,
                    RecipientName = c.RecipientName,
                    ProgramName = c.Program?.Name
                })
                .ToList()
        };

        foreach (var card in dto.ExpiringCards)
        {
            card.AddSelfLink($"/api/giftcards/{card.Id}");
        }

        dto.AddSelfLink("/api/giftcard-reports/expiring");

        return Ok(dto);
    }
}
