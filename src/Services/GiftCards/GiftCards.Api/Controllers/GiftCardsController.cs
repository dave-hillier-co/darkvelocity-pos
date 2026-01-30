using DarkVelocity.GiftCards.Api.Data;
using DarkVelocity.GiftCards.Api.Dtos;
using DarkVelocity.GiftCards.Api.Entities;
using DarkVelocity.GiftCards.Api.Services;
using DarkVelocity.Shared.Contracts.Hal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.GiftCards.Api.Controllers;

[ApiController]
[Route("api/giftcards")]
public class GiftCardsController : ControllerBase
{
    private readonly GiftCardsDbContext _context;
    private readonly ICardNumberGenerator _cardNumberGenerator;

    public GiftCardsController(
        GiftCardsDbContext context,
        ICardNumberGenerator cardNumberGenerator)
    {
        _context = context;
        _cardNumberGenerator = cardNumberGenerator;
    }

    /// <summary>
    /// List gift cards with optional filters
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<HalCollection<GiftCardSummaryDto>>> GetAll(
        [FromQuery] Guid? tenantId = null,
        [FromQuery] Guid? locationId = null,
        [FromQuery] Guid? programId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? cardType = null,
        [FromQuery] string? cardNumber = null,
        [FromQuery] bool? expiringSoon = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var query = _context.GiftCards
            .Include(g => g.Program)
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(g => g.TenantId == tenantId.Value);

        if (locationId.HasValue)
            query = query.Where(g => g.LocationId == locationId.Value);

        if (programId.HasValue)
            query = query.Where(g => g.ProgramId == programId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(g => g.Status == status);

        if (!string.IsNullOrEmpty(cardType))
            query = query.Where(g => g.CardType == cardType);

        if (!string.IsNullOrEmpty(cardNumber))
            query = query.Where(g => g.CardNumber.Contains(cardNumber));

        if (expiringSoon == true)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(30);
            query = query.Where(g => g.ExpiryDate != null && g.ExpiryDate <= cutoffDate && g.Status == "active");
        }

        var total = await query.CountAsync();

        var cards = await query
            .OrderByDescending(g => g.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var dtos = cards.Select(g => MapToSummaryDto(g, now)).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/giftcards/{dto.Id}");
        }

        return Ok(HalCollection<GiftCardSummaryDto>.Create(dtos, "/api/giftcards", total));
    }

    /// <summary>
    /// Get gift card by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GiftCardDto>> GetById(Guid id)
    {
        var card = await _context.GiftCards
            .Include(g => g.Program)
            .Include(g => g.Design)
            .Include(g => g.Transactions.OrderByDescending(t => t.ProcessedAt).Take(10))
            .FirstOrDefaultAsync(g => g.Id == id);

        if (card == null)
            return NotFound();

        var dto = MapToDto(card, DateTime.UtcNow);
        AddLinks(dto, card);

        return Ok(dto);
    }

    /// <summary>
    /// Lookup gift card by card number
    /// </summary>
    [HttpGet("lookup")]
    public async Task<ActionResult<GiftCardDto>> Lookup(
        [FromQuery] string number,
        [FromQuery] string? pin = null)
    {
        var card = await _context.GiftCards
            .Include(g => g.Program)
            .Include(g => g.Design)
            .Include(g => g.Transactions.OrderByDescending(t => t.ProcessedAt).Take(10))
            .FirstOrDefaultAsync(g => g.CardNumber == number);

        if (card == null)
            return NotFound(new { message = "Gift card not found" });

        // Verify PIN if required
        if (card.Program?.RequirePin == true && !string.IsNullOrEmpty(card.PinHash))
        {
            if (string.IsNullOrEmpty(pin))
                return BadRequest(new { message = "PIN is required" });

            if (!_cardNumberGenerator.VerifyPin(pin, card.PinHash))
                return BadRequest(new { message = "Invalid PIN" });
        }

        var dto = MapToDto(card, DateTime.UtcNow);
        AddLinks(dto, card);

        return Ok(dto);
    }

    /// <summary>
    /// Get quick balance check (minimal response)
    /// </summary>
    [HttpGet("{id:guid}/balance")]
    public async Task<ActionResult<GiftCardBalanceDto>> GetBalance(Guid id)
    {
        var card = await _context.GiftCards
            .Include(g => g.Program)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (card == null)
            return NotFound();

        var now = DateTime.UtcNow;
        var isExpired = card.ExpiryDate.HasValue && card.ExpiryDate.Value < now;

        var dto = new GiftCardBalanceDto
        {
            Id = card.Id,
            MaskedCardNumber = CardNumberHelper.MaskCardNumber(card.CardNumber),
            CurrentBalance = card.CurrentBalance,
            CurrencyCode = card.CurrencyCode,
            Status = card.Status,
            ExpiryDate = card.ExpiryDate,
            IsExpired = isExpired,
            CanRedeem = card.Status == "active" && !isExpired && card.CurrentBalance > 0,
            CanReload = card.Status == "active" && !isExpired && (card.Program?.AllowReload ?? false)
        };

        dto.AddSelfLink($"/api/giftcards/{id}/balance");
        dto.AddLink("card", $"/api/giftcards/{id}");

        return Ok(dto);
    }

    /// <summary>
    /// Get transaction history for a gift card
    /// </summary>
    [HttpGet("{id:guid}/transactions")]
    public async Task<ActionResult<HalCollection<GiftCardTransactionDto>>> GetTransactions(
        Guid id,
        [FromQuery] string? type = null,
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        var card = await _context.GiftCards.FindAsync(id);
        if (card == null)
            return NotFound();

        var query = _context.GiftCardTransactions
            .Where(t => t.GiftCardId == id);

        if (!string.IsNullOrEmpty(type))
            query = query.Where(t => t.TransactionType == type);

        var total = await query.CountAsync();

        var transactions = await query
            .OrderByDescending(t => t.ProcessedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var dtos = transactions.Select(MapToTransactionDto).ToList();

        foreach (var dto in dtos)
        {
            dto.AddSelfLink($"/api/giftcards/{id}/transactions/{dto.Id}");
        }

        return Ok(HalCollection<GiftCardTransactionDto>.Create(
            dtos,
            $"/api/giftcards/{id}/transactions",
            total
        ));
    }

    /// <summary>
    /// Create/issue a new gift card
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<GiftCardDto>> Create(
        [FromQuery] Guid locationId,
        [FromBody] CreateGiftCardRequest request)
    {
        // Validate program
        var program = await _context.GiftCardPrograms.FindAsync(request.ProgramId);
        if (program == null)
            return BadRequest(new { message = "Program not found" });

        if (!program.IsActive)
            return BadRequest(new { message = "Program is not active" });

        // Validate initial balance
        if (request.InitialBalance < program.MinimumLoadAmount)
            return BadRequest(new { message = $"Initial balance must be at least {program.MinimumLoadAmount}" });

        if (request.InitialBalance > program.MaximumLoadAmount)
            return BadRequest(new { message = $"Initial balance cannot exceed {program.MaximumLoadAmount}" });

        if (request.InitialBalance > program.MaximumBalance)
            return BadRequest(new { message = $"Balance cannot exceed {program.MaximumBalance}" });

        // Validate design if provided
        if (request.DesignId.HasValue)
        {
            var design = await _context.GiftCardDesigns
                .FirstOrDefaultAsync(d => d.Id == request.DesignId && d.ProgramId == program.Id && d.IsActive);
            if (design == null)
                return BadRequest(new { message = "Design not found or not active" });
        }

        // Generate card number
        var cardNumber = await _cardNumberGenerator.GenerateAsync(program.CardNumberPrefix);

        // Calculate expiry date
        DateTime? expiryDate = null;
        if (program.DefaultExpiryMonths.HasValue)
        {
            expiryDate = DateTime.UtcNow.AddMonths(program.DefaultExpiryMonths.Value);
        }

        var card = new GiftCard
        {
            TenantId = program.TenantId,
            LocationId = locationId,
            ProgramId = program.Id,
            DesignId = request.DesignId,
            CardNumber = cardNumber,
            InitialBalance = request.InitialBalance,
            CurrentBalance = 0, // Will be set on activation
            CurrencyCode = program.CurrencyCode,
            Status = "pending_activation",
            CardType = request.CardType,
            ExpiryDate = expiryDate,
            IssuedAt = DateTime.UtcNow,
            IssuedByUserId = request.IssuedByUserId,
            RecipientName = request.RecipientName,
            RecipientEmail = request.RecipientEmail,
            GiftMessage = request.GiftMessage,
            PurchaserName = request.PurchaserName,
            PurchaserEmail = request.PurchaserEmail,
            Notes = request.Notes
        };

        _context.GiftCards.Add(card);
        await _context.SaveChangesAsync();

        // Reload with related data
        await _context.Entry(card).Reference(c => c.Program).LoadAsync();
        await _context.Entry(card).Reference(c => c.Design).LoadAsync();

        var dto = MapToDto(card, DateTime.UtcNow);
        AddLinks(dto, card);

        return CreatedAtAction(nameof(GetById), new { id = card.Id }, dto);
    }

    /// <summary>
    /// Activate a gift card
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<GiftCardDto>> Activate(
        Guid id,
        [FromQuery] Guid locationId,
        [FromBody] ActivateGiftCardRequest request)
    {
        var card = await _context.GiftCards
            .Include(g => g.Program)
            .Include(g => g.Design)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (card == null)
            return NotFound();

        if (card.Status != "pending_activation")
            return BadRequest(new { message = "Card is not pending activation" });

        // Hash PIN if provided and required
        if (card.Program?.RequirePin == true)
        {
            if (string.IsNullOrEmpty(request.Pin))
            {
                // Generate PIN if not provided
                var pin = _cardNumberGenerator.GeneratePin();
                card.PinHash = _cardNumberGenerator.HashPin(pin);
                // Note: In production, you'd return this PIN to the issuer securely
            }
            else
            {
                card.PinHash = _cardNumberGenerator.HashPin(request.Pin);
            }
        }

        // Set balance and activate
        card.CurrentBalance = card.InitialBalance;
        card.Status = "active";
        card.ActivatedAt = DateTime.UtcNow;
        card.ActivatedByUserId = request.ActivatedByUserId;

        // Create activation transaction
        var transaction = new GiftCardTransaction
        {
            GiftCardId = card.Id,
            LocationId = locationId,
            TransactionType = "activation",
            Amount = card.InitialBalance,
            BalanceBefore = 0,
            BalanceAfter = card.InitialBalance,
            UserId = request.ActivatedByUserId,
            ProcessedAt = DateTime.UtcNow,
            TransactionReference = GenerateTransactionReference()
        };

        _context.GiftCardTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        var dto = MapToDto(card, DateTime.UtcNow);
        AddLinks(dto, card);

        return Ok(dto);
    }

    /// <summary>
    /// Redeem (use) a gift card
    /// </summary>
    [HttpPost("{id:guid}/redeem")]
    public async Task<ActionResult<GiftCardDto>> Redeem(
        Guid id,
        [FromQuery] Guid locationId,
        [FromBody] RedeemGiftCardRequest request)
    {
        var card = await _context.GiftCards
            .Include(g => g.Program)
            .Include(g => g.Design)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (card == null)
            return NotFound();

        if (card.Status != "active")
            return BadRequest(new { message = "Card is not active" });

        var now = DateTime.UtcNow;
        if (card.ExpiryDate.HasValue && card.ExpiryDate.Value < now)
            return BadRequest(new { message = "Card has expired" });

        // Verify PIN if required
        if (card.Program?.RequirePin == true && !string.IsNullOrEmpty(card.PinHash))
        {
            if (string.IsNullOrEmpty(request.Pin))
                return BadRequest(new { message = "PIN is required" });

            if (!_cardNumberGenerator.VerifyPin(request.Pin, card.PinHash))
                return BadRequest(new { message = "Invalid PIN" });
        }

        // Validate amount
        if (request.Amount <= 0)
            return BadRequest(new { message = "Amount must be positive" });

        if (request.Amount > card.CurrentBalance)
            return BadRequest(new { message = "Insufficient balance" });

        // Check partial redemption
        if (request.Amount < card.CurrentBalance && card.Program?.AllowPartialRedemption != true)
            return BadRequest(new { message = "Partial redemption is not allowed" });

        var balanceBefore = card.CurrentBalance;
        card.CurrentBalance -= request.Amount;
        card.LastUsedAt = now;

        // Check if depleted
        if (card.CurrentBalance == 0)
        {
            card.Status = "depleted";
        }

        // Create transaction
        var transaction = new GiftCardTransaction
        {
            GiftCardId = card.Id,
            LocationId = locationId,
            TransactionType = "redemption",
            Amount = -request.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = card.CurrentBalance,
            OrderId = request.OrderId,
            PaymentId = request.PaymentId,
            UserId = request.UserId,
            Notes = request.Notes,
            ProcessedAt = now,
            TransactionReference = GenerateTransactionReference()
        };

        _context.GiftCardTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        var dto = MapToDto(card, now);
        AddLinks(dto, card);

        return Ok(dto);
    }

    /// <summary>
    /// Reload a gift card
    /// </summary>
    [HttpPost("{id:guid}/reload")]
    public async Task<ActionResult<GiftCardDto>> Reload(
        Guid id,
        [FromQuery] Guid locationId,
        [FromBody] ReloadGiftCardRequest request)
    {
        var card = await _context.GiftCards
            .Include(g => g.Program)
            .Include(g => g.Design)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (card == null)
            return NotFound();

        if (card.Status != "active")
            return BadRequest(new { message = "Card is not active" });

        var now = DateTime.UtcNow;
        if (card.ExpiryDate.HasValue && card.ExpiryDate.Value < now)
            return BadRequest(new { message = "Card has expired" });

        if (card.Program?.AllowReload != true)
            return BadRequest(new { message = "Reload is not allowed for this card" });

        // Validate amount
        if (request.Amount <= 0)
            return BadRequest(new { message = "Amount must be positive" });

        if (request.Amount < card.Program!.MinimumLoadAmount)
            return BadRequest(new { message = $"Minimum reload amount is {card.Program.MinimumLoadAmount}" });

        if (request.Amount > card.Program.MaximumLoadAmount)
            return BadRequest(new { message = $"Maximum reload amount is {card.Program.MaximumLoadAmount}" });

        var newBalance = card.CurrentBalance + request.Amount;
        if (newBalance > card.Program.MaximumBalance)
            return BadRequest(new { message = $"Balance would exceed maximum of {card.Program.MaximumBalance}" });

        var balanceBefore = card.CurrentBalance;
        card.CurrentBalance = newBalance;
        card.LastUsedAt = now;

        // Create transaction
        var transaction = new GiftCardTransaction
        {
            GiftCardId = card.Id,
            LocationId = locationId,
            TransactionType = "reload",
            Amount = request.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = card.CurrentBalance,
            UserId = request.UserId,
            Notes = request.Notes,
            ProcessedAt = now,
            TransactionReference = GenerateTransactionReference()
        };

        _context.GiftCardTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        var dto = MapToDto(card, now);
        AddLinks(dto, card);

        return Ok(dto);
    }

    /// <summary>
    /// Refund to a gift card
    /// </summary>
    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<GiftCardDto>> Refund(
        Guid id,
        [FromQuery] Guid locationId,
        [FromBody] RefundToGiftCardRequest request)
    {
        var card = await _context.GiftCards
            .Include(g => g.Program)
            .Include(g => g.Design)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (card == null)
            return NotFound();

        // Allow refunds to active or depleted cards
        if (card.Status != "active" && card.Status != "depleted")
            return BadRequest(new { message = "Cannot refund to this card" });

        var now = DateTime.UtcNow;
        if (card.ExpiryDate.HasValue && card.ExpiryDate.Value < now)
            return BadRequest(new { message = "Card has expired" });

        if (request.Amount <= 0)
            return BadRequest(new { message = "Amount must be positive" });

        var newBalance = card.CurrentBalance + request.Amount;
        if (newBalance > card.Program!.MaximumBalance)
            return BadRequest(new { message = $"Balance would exceed maximum of {card.Program.MaximumBalance}" });

        var balanceBefore = card.CurrentBalance;
        card.CurrentBalance = newBalance;
        card.LastUsedAt = now;

        // Reactivate if was depleted
        if (card.Status == "depleted")
        {
            card.Status = "active";
        }

        // Create transaction
        var transaction = new GiftCardTransaction
        {
            GiftCardId = card.Id,
            LocationId = locationId,
            TransactionType = "refund",
            Amount = request.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = card.CurrentBalance,
            OrderId = request.OrderId,
            UserId = request.UserId,
            Reason = request.Reason,
            ProcessedAt = now,
            TransactionReference = GenerateTransactionReference()
        };

        _context.GiftCardTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        var dto = MapToDto(card, now);
        AddLinks(dto, card);

        return Ok(dto);
    }

    /// <summary>
    /// Adjust gift card balance (admin only)
    /// </summary>
    [HttpPost("{id:guid}/adjust")]
    public async Task<ActionResult<GiftCardDto>> Adjust(
        Guid id,
        [FromQuery] Guid locationId,
        [FromBody] AdjustBalanceRequest request)
    {
        var card = await _context.GiftCards
            .Include(g => g.Program)
            .Include(g => g.Design)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (card == null)
            return NotFound();

        if (card.Status != "active" && card.Status != "depleted")
            return BadRequest(new { message = "Cannot adjust balance for this card" });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Reason is required for adjustments" });

        var newBalance = card.CurrentBalance + request.Amount;
        if (newBalance < 0)
            return BadRequest(new { message = "Balance cannot be negative" });

        if (newBalance > card.Program!.MaximumBalance)
            return BadRequest(new { message = $"Balance would exceed maximum of {card.Program.MaximumBalance}" });

        var now = DateTime.UtcNow;
        var balanceBefore = card.CurrentBalance;
        card.CurrentBalance = newBalance;

        // Update status based on balance
        if (newBalance == 0 && card.Status == "active")
        {
            card.Status = "depleted";
        }
        else if (newBalance > 0 && card.Status == "depleted")
        {
            card.Status = "active";
        }

        // Create transaction
        var transaction = new GiftCardTransaction
        {
            GiftCardId = card.Id,
            LocationId = locationId,
            TransactionType = "adjustment",
            Amount = request.Amount,
            BalanceBefore = balanceBefore,
            BalanceAfter = card.CurrentBalance,
            UserId = request.UserId,
            Reason = request.Reason,
            ProcessedAt = now,
            TransactionReference = GenerateTransactionReference()
        };

        _context.GiftCardTransactions.Add(transaction);
        await _context.SaveChangesAsync();

        var dto = MapToDto(card, now);
        AddLinks(dto, card);

        return Ok(dto);
    }

    /// <summary>
    /// Suspend a gift card
    /// </summary>
    [HttpPost("{id:guid}/suspend")]
    public async Task<ActionResult<GiftCardDto>> Suspend(
        Guid id,
        [FromBody] SuspendGiftCardRequest request)
    {
        var card = await _context.GiftCards
            .Include(g => g.Program)
            .Include(g => g.Design)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (card == null)
            return NotFound();

        if (card.Status != "active")
            return BadRequest(new { message = "Card is not active" });

        if (string.IsNullOrWhiteSpace(request.Reason))
            return BadRequest(new { message = "Reason is required" });

        card.Status = "suspended";
        card.SuspendedAt = DateTime.UtcNow;
        card.SuspensionReason = request.Reason;
        card.SuspendedByUserId = request.SuspendedByUserId;

        await _context.SaveChangesAsync();

        var dto = MapToDto(card, DateTime.UtcNow);
        AddLinks(dto, card);

        return Ok(dto);
    }

    /// <summary>
    /// Resume a suspended gift card
    /// </summary>
    [HttpPost("{id:guid}/resume")]
    public async Task<ActionResult<GiftCardDto>> Resume(
        Guid id,
        [FromBody] ResumeGiftCardRequest? request = null)
    {
        var card = await _context.GiftCards
            .Include(g => g.Program)
            .Include(g => g.Design)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (card == null)
            return NotFound();

        if (card.Status != "suspended")
            return BadRequest(new { message = "Card is not suspended" });

        var now = DateTime.UtcNow;
        if (card.ExpiryDate.HasValue && card.ExpiryDate.Value < now)
        {
            card.Status = "expired";
            return BadRequest(new { message = "Card has expired and cannot be resumed" });
        }

        card.Status = card.CurrentBalance > 0 ? "active" : "depleted";
        card.SuspendedAt = null;
        card.SuspensionReason = null;
        card.SuspendedByUserId = null;

        await _context.SaveChangesAsync();

        var dto = MapToDto(card, now);
        AddLinks(dto, card);

        return Ok(dto);
    }

    // ============================================
    // Helper Methods
    // ============================================

    private static GiftCardDto MapToDto(GiftCard card, DateTime now)
    {
        var isExpired = card.ExpiryDate.HasValue && card.ExpiryDate.Value < now;

        return new GiftCardDto
        {
            Id = card.Id,
            TenantId = card.TenantId,
            LocationId = card.LocationId,
            ProgramId = card.ProgramId,
            ProgramName = card.Program?.Name,
            DesignId = card.DesignId,
            DesignName = card.Design?.Name,
            CardNumber = card.CardNumber,
            MaskedCardNumber = CardNumberHelper.MaskCardNumber(card.CardNumber),
            HasPin = !string.IsNullOrEmpty(card.PinHash),
            CardType = card.CardType,
            InitialBalance = card.InitialBalance,
            CurrentBalance = card.CurrentBalance,
            CurrencyCode = card.CurrencyCode,
            Status = card.Status,
            ExpiryDate = card.ExpiryDate,
            IsExpired = isExpired,
            IsDepleted = card.Status == "depleted",
            IssuedAt = card.IssuedAt,
            ActivatedAt = card.ActivatedAt,
            LastUsedAt = card.LastUsedAt,
            SuspendedAt = card.SuspendedAt,
            SuspensionReason = card.SuspensionReason,
            CreatedAt = card.CreatedAt,
            RecipientName = card.RecipientName,
            RecipientEmail = card.RecipientEmail,
            GiftMessage = card.GiftMessage,
            PurchaserName = card.PurchaserName,
            PurchaserEmail = card.PurchaserEmail,
            IssuedByUserId = card.IssuedByUserId,
            ActivatedByUserId = card.ActivatedByUserId,
            Notes = card.Notes,
            ExternalReference = card.ExternalReference,
            RecentTransactions = card.Transactions.Select(MapToTransactionDto).ToList(),
            TotalTransactionCount = card.Transactions.Count
        };
    }

    private static GiftCardSummaryDto MapToSummaryDto(GiftCard card, DateTime now)
    {
        return new GiftCardSummaryDto
        {
            Id = card.Id,
            MaskedCardNumber = CardNumberHelper.MaskCardNumber(card.CardNumber),
            CardType = card.CardType,
            CurrentBalance = card.CurrentBalance,
            CurrencyCode = card.CurrencyCode,
            Status = card.Status,
            ExpiryDate = card.ExpiryDate,
            IsExpired = card.ExpiryDate.HasValue && card.ExpiryDate.Value < now,
            LastUsedAt = card.LastUsedAt,
            RecipientName = card.RecipientName,
            ProgramName = card.Program?.Name
        };
    }

    private static GiftCardTransactionDto MapToTransactionDto(GiftCardTransaction t)
    {
        return new GiftCardTransactionDto
        {
            Id = t.Id,
            GiftCardId = t.GiftCardId,
            LocationId = t.LocationId,
            TransactionType = t.TransactionType,
            Amount = t.Amount,
            BalanceBefore = t.BalanceBefore,
            BalanceAfter = t.BalanceAfter,
            OrderId = t.OrderId,
            PaymentId = t.PaymentId,
            UserId = t.UserId,
            Reason = t.Reason,
            ProcessedAt = t.ProcessedAt,
            ExternalReference = t.ExternalReference,
            TransactionReference = t.TransactionReference,
            Notes = t.Notes
        };
    }

    private static void AddLinks(GiftCardDto dto, GiftCard card)
    {
        dto.AddSelfLink($"/api/giftcards/{dto.Id}");
        dto.AddLink("balance", $"/api/giftcards/{dto.Id}/balance");
        dto.AddLink("transactions", $"/api/giftcards/{dto.Id}/transactions");
        dto.AddLink("program", $"/api/giftcard-programs/{dto.ProgramId}");

        if (dto.DesignId.HasValue)
            dto.AddLink("design", $"/api/giftcard-programs/{dto.ProgramId}/designs/{dto.DesignId}");

        // Add action links based on status
        AddActionLinks(dto, card);
    }

    private static void AddActionLinks(GiftCardDto dto, GiftCard card)
    {
        var baseUrl = $"/api/giftcards/{dto.Id}";

        switch (card.Status)
        {
            case "pending_activation":
                dto.AddLink("activate", $"{baseUrl}/activate");
                break;

            case "active":
                dto.AddLink("redeem", $"{baseUrl}/redeem");
                dto.AddLink("suspend", $"{baseUrl}/suspend");
                if (card.Program?.AllowReload == true)
                    dto.AddLink("reload", $"{baseUrl}/reload");
                dto.AddLink("refund", $"{baseUrl}/refund");
                dto.AddLink("adjust", $"{baseUrl}/adjust");
                break;

            case "suspended":
                dto.AddLink("resume", $"{baseUrl}/resume");
                break;

            case "depleted":
                dto.AddLink("refund", $"{baseUrl}/refund");
                dto.AddLink("adjust", $"{baseUrl}/adjust");
                break;
        }
    }

    private static string GenerateTransactionReference()
    {
        return $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }
}
