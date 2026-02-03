using System.Security.Cryptography;
using System.Text;
using DarkVelocity.Host;
using DarkVelocity.Host.Grains;
using DarkVelocity.Host.State;
using Orleans.Runtime;

namespace DarkVelocity.Host.Grains;

// ============================================================================
// Merchant Grain Implementation
// ============================================================================

public class MerchantGrain : Grain, IMerchantGrain
{
    private readonly IPersistentState<MerchantState> _state;
    private const int MaxApiKeys = 20;

    public MerchantGrain(
        [PersistentState("merchant", "OrleansStorage")]
        IPersistentState<MerchantState> state)
    {
        _state = state;
    }

    public async Task<MerchantSnapshot> CreateAsync(CreateMerchantCommand command)
    {
        if (_state.State.MerchantId != Guid.Empty)
            throw new InvalidOperationException("Merchant already exists");

        var (orgId, _, merchantId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        _state.State = new MerchantState
        {
            OrgId = orgId,
            MerchantId = merchantId,
            Name = command.Name,
            Email = command.Email,
            BusinessName = command.BusinessName,
            BusinessType = command.BusinessType,
            Country = command.Country,
            DefaultCurrency = command.DefaultCurrency,
            StatementDescriptor = command.StatementDescriptor,
            AddressLine1 = command.AddressLine1,
            AddressLine2 = command.AddressLine2,
            City = command.City,
            State = command.State,
            PostalCode = command.PostalCode,
            Status = "active",
            ChargesEnabled = true,
            PayoutsEnabled = false,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<MerchantSnapshot> UpdateAsync(UpdateMerchantCommand command)
    {
        EnsureExists();

        if (command.Name != null) _state.State.Name = command.Name;
        if (command.BusinessName != null) _state.State.BusinessName = command.BusinessName;
        if (command.BusinessType != null) _state.State.BusinessType = command.BusinessType;
        if (command.StatementDescriptor != null) _state.State.StatementDescriptor = command.StatementDescriptor;
        if (command.AddressLine1 != null) _state.State.AddressLine1 = command.AddressLine1;
        if (command.AddressLine2 != null) _state.State.AddressLine2 = command.AddressLine2;
        if (command.City != null) _state.State.City = command.City;
        if (command.State != null) _state.State.State = command.State;
        if (command.PostalCode != null) _state.State.PostalCode = command.PostalCode;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<MerchantSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.MerchantId != Guid.Empty);
    }

    public async Task<ApiKeySnapshot> CreateApiKeyAsync(string name, string keyType, bool isLive, DateTime? expiresAt)
    {
        EnsureExists();

        if (_state.State.ApiKeys.Count(k => k.RevokedAt == null) >= MaxApiKeys)
            throw new InvalidOperationException("Maximum number of API keys reached");

        var prefix = (keyType, isLive) switch
        {
            ("secret", true) => "sk_live_",
            ("secret", false) => "sk_test_",
            ("publishable", true) => "pk_live_",
            ("publishable", false) => "pk_test_",
            _ => "sk_test_"
        };

        var keyValue = GenerateRandomKey(32);
        var fullKey = $"{prefix}{keyValue}";
        var keyHash = ComputeHash(fullKey);
        var keyHint = $"{prefix}...{keyValue[^4..]}";

        var apiKey = new ApiKeyState
        {
            KeyId = Guid.NewGuid(),
            Name = name,
            KeyType = keyType,
            KeyPrefix = prefix,
            KeyHash = keyHash,
            KeyHint = keyHint,
            IsLive = isLive,
            IsActive = true,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _state.State.ApiKeys.Add(apiKey);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new ApiKeySnapshot(
            apiKey.KeyId,
            apiKey.Name,
            apiKey.KeyType,
            apiKey.KeyPrefix,
            apiKey.KeyHint,
            apiKey.IsLive,
            apiKey.IsActive,
            apiKey.LastUsedAt,
            apiKey.ExpiresAt,
            apiKey.CreatedAt);
    }

    public Task<IReadOnlyList<ApiKeySnapshot>> GetApiKeysAsync()
    {
        EnsureExists();

        var keys = _state.State.ApiKeys
            .Where(k => k.RevokedAt == null)
            .Select(k => new ApiKeySnapshot(
                k.KeyId,
                k.Name,
                k.KeyType,
                k.KeyPrefix,
                k.KeyHint,
                k.IsLive,
                k.IsActive,
                k.LastUsedAt,
                k.ExpiresAt,
                k.CreatedAt))
            .ToList();

        return Task.FromResult<IReadOnlyList<ApiKeySnapshot>>(keys);
    }

    public async Task RevokeApiKeyAsync(Guid keyId)
    {
        EnsureExists();

        var key = _state.State.ApiKeys.FirstOrDefault(k => k.KeyId == keyId && k.RevokedAt == null)
            ?? throw new InvalidOperationException("API key not found");

        key.IsActive = false;
        key.RevokedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
    }

    public async Task<ApiKeySnapshot> RollApiKeyAsync(Guid keyId, DateTime? expiresAt)
    {
        EnsureExists();

        var oldKey = _state.State.ApiKeys.FirstOrDefault(k => k.KeyId == keyId && k.RevokedAt == null)
            ?? throw new InvalidOperationException("API key not found");

        // Revoke old key
        oldKey.IsActive = false;
        oldKey.RevokedAt = DateTime.UtcNow;

        // Create new key with same settings
        var keyValue = GenerateRandomKey(32);
        var fullKey = $"{oldKey.KeyPrefix}{keyValue}";
        var keyHash = ComputeHash(fullKey);
        var keyHint = $"{oldKey.KeyPrefix}...{keyValue[^4..]}";

        var newKey = new ApiKeyState
        {
            KeyId = Guid.NewGuid(),
            Name = oldKey.Name,
            KeyType = oldKey.KeyType,
            KeyPrefix = oldKey.KeyPrefix,
            KeyHash = keyHash,
            KeyHint = keyHint,
            IsLive = oldKey.IsLive,
            IsActive = true,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _state.State.ApiKeys.Add(newKey);
        _state.State.Version++;

        await _state.WriteStateAsync();

        return new ApiKeySnapshot(
            newKey.KeyId,
            newKey.Name,
            newKey.KeyType,
            newKey.KeyPrefix,
            newKey.KeyHint,
            newKey.IsLive,
            newKey.IsActive,
            newKey.LastUsedAt,
            newKey.ExpiresAt,
            newKey.CreatedAt);
    }

    public Task<bool> ValidateApiKeyAsync(string keyHash)
    {
        var key = _state.State.ApiKeys.FirstOrDefault(k =>
            k.KeyHash == keyHash &&
            k.IsActive &&
            k.RevokedAt == null &&
            (k.ExpiresAt == null || k.ExpiresAt > DateTime.UtcNow));

        return Task.FromResult(key != null);
    }

    public async Task EnableChargesAsync()
    {
        EnsureExists();
        _state.State.ChargesEnabled = true;
        _state.State.UpdatedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DisableChargesAsync()
    {
        EnsureExists();
        _state.State.ChargesEnabled = false;
        _state.State.UpdatedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task EnablePayoutsAsync()
    {
        EnsureExists();
        _state.State.PayoutsEnabled = true;
        _state.State.UpdatedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DisablePayoutsAsync()
    {
        EnsureExists();
        _state.State.PayoutsEnabled = false;
        _state.State.UpdatedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private void EnsureExists()
    {
        if (_state.State.MerchantId == Guid.Empty)
            throw new InvalidOperationException("Merchant not found");
    }

    private MerchantSnapshot CreateSnapshot()
    {
        return new MerchantSnapshot(
            _state.State.MerchantId,
            _state.State.Name,
            _state.State.Email,
            _state.State.BusinessName,
            _state.State.BusinessType,
            _state.State.Country,
            _state.State.DefaultCurrency,
            _state.State.Status,
            _state.State.PayoutsEnabled,
            _state.State.ChargesEnabled,
            _state.State.StatementDescriptor,
            _state.State.AddressLine1,
            _state.State.AddressLine2,
            _state.State.City,
            _state.State.State,
            _state.State.PostalCode,
            _state.State.CreatedAt,
            _state.State.UpdatedAt);
    }

    private static string GenerateRandomKey(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[length];
        var bytes = RandomNumberGenerator.GetBytes(length);
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes);
    }
}

// ============================================================================
// Terminal Grain Implementation
// ============================================================================

public class TerminalGrain : Grain, ITerminalGrain
{
    private readonly IPersistentState<TerminalState> _state;
    private static readonly TimeSpan OfflineThreshold = TimeSpan.FromMinutes(5);

    public TerminalGrain(
        [PersistentState("terminal", "OrleansStorage")]
        IPersistentState<TerminalState> state)
    {
        _state = state;
    }

    public async Task<TerminalSnapshot> RegisterAsync(RegisterTerminalCommand command)
    {
        if (_state.State.TerminalId != Guid.Empty)
            throw new InvalidOperationException("Terminal already exists");

        var (orgId, _, terminalId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        _state.State = new TerminalState
        {
            OrgId = orgId,
            TerminalId = terminalId,
            MerchantId = orgId, // Simplified - merchant is same as org
            LocationId = command.LocationId,
            Label = command.Label,
            DeviceType = command.DeviceType,
            SerialNumber = command.SerialNumber,
            Status = TerminalStatus.Active,
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<TerminalSnapshot> UpdateAsync(UpdateTerminalCommand command)
    {
        EnsureExists();

        if (command.Label != null) _state.State.Label = command.Label;
        if (command.LocationId.HasValue) _state.State.LocationId = command.LocationId.Value;
        if (command.Status.HasValue) _state.State.Status = command.Status.Value;

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<TerminalSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.TerminalId != Guid.Empty);
    }

    public async Task DeactivateAsync()
    {
        EnsureExists();
        _state.State.Status = TerminalStatus.Inactive;
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task HeartbeatAsync(string? ipAddress, string? softwareVersion)
    {
        EnsureExists();

        _state.State.LastSeenAt = DateTime.UtcNow;
        _state.State.IpAddress = ipAddress;
        _state.State.SoftwareVersion = softwareVersion;

        if (_state.State.Status == TerminalStatus.Offline)
        {
            _state.State.Status = TerminalStatus.Active;
        }

        await _state.WriteStateAsync();
    }

    public Task<bool> IsOnlineAsync()
    {
        if (_state.State.Status != TerminalStatus.Active)
            return Task.FromResult(false);

        if (_state.State.LastSeenAt == null)
            return Task.FromResult(false);

        var isOnline = DateTime.UtcNow - _state.State.LastSeenAt.Value < OfflineThreshold;
        return Task.FromResult(isOnline);
    }

    private void EnsureExists()
    {
        if (_state.State.TerminalId == Guid.Empty)
            throw new InvalidOperationException("Terminal not found");
    }

    private TerminalSnapshot CreateSnapshot()
    {
        return new TerminalSnapshot(
            _state.State.TerminalId,
            _state.State.MerchantId,
            _state.State.LocationId,
            _state.State.Label,
            _state.State.DeviceType,
            _state.State.SerialNumber,
            _state.State.Status,
            _state.State.LastSeenAt,
            _state.State.IpAddress,
            _state.State.SoftwareVersion,
            _state.State.CreatedAt,
            _state.State.UpdatedAt);
    }
}

// ============================================================================
// Refund Grain Implementation
// ============================================================================

public class RefundGrain : Grain, IRefundGrain
{
    private readonly IPersistentState<RefundState> _state;

    public RefundGrain(
        [PersistentState("refund", "OrleansStorage")]
        IPersistentState<RefundState> state)
    {
        _state = state;
    }

    public async Task<RefundSnapshot> CreateAsync(CreateRefundCommand command)
    {
        if (_state.State.RefundId != Guid.Empty)
            throw new InvalidOperationException("Refund already exists");

        var (orgId, _, refundId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        _state.State = new RefundState
        {
            OrgId = orgId,
            RefundId = refundId,
            MerchantId = orgId, // Simplified
            PaymentIntentId = command.PaymentIntentId,
            Amount = command.Amount ?? 0,
            Currency = command.Currency,
            Status = RefundStatus.Pending,
            Reason = command.Reason,
            ReceiptNumber = GenerateReceiptNumber(),
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<RefundSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.RefundId != Guid.Empty);
    }

    public async Task<RefundSnapshot> ProcessAsync()
    {
        EnsureExists();

        if (_state.State.Status != RefundStatus.Pending)
            throw new InvalidOperationException("Refund is not pending");

        _state.State.Status = RefundStatus.Succeeded;
        _state.State.SucceededAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<RefundSnapshot> FailAsync(string reason)
    {
        EnsureExists();

        if (_state.State.Status != RefundStatus.Pending)
            throw new InvalidOperationException("Refund is not pending");

        _state.State.Status = RefundStatus.Failed;
        _state.State.FailureReason = reason;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<RefundSnapshot> CancelAsync()
    {
        EnsureExists();

        if (_state.State.Status != RefundStatus.Pending)
            throw new InvalidOperationException("Refund is not pending");

        _state.State.Status = RefundStatus.Cancelled;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<RefundStatus> GetStatusAsync()
    {
        return Task.FromResult(_state.State.Status);
    }

    private void EnsureExists()
    {
        if (_state.State.RefundId == Guid.Empty)
            throw new InvalidOperationException("Refund not found");
    }

    private RefundSnapshot CreateSnapshot()
    {
        return new RefundSnapshot(
            _state.State.RefundId,
            _state.State.MerchantId,
            _state.State.PaymentIntentId,
            _state.State.Amount,
            _state.State.Currency,
            _state.State.Status,
            _state.State.Reason,
            _state.State.ReceiptNumber,
            _state.State.FailureReason,
            _state.State.CreatedAt,
            _state.State.SucceededAt);
    }

    private static string GenerateReceiptNumber()
    {
        return $"RF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }
}

// ============================================================================
// Webhook Endpoint Grain Implementation
// ============================================================================

public class WebhookEndpointGrain : Grain, IWebhookEndpointGrain
{
    private readonly IPersistentState<WebhookEndpointState> _state;
    private const int MaxRecentDeliveries = 20;

    public WebhookEndpointGrain(
        [PersistentState("webhookEndpoint", "OrleansStorage")]
        IPersistentState<WebhookEndpointState> state)
    {
        _state = state;
    }

    public async Task<WebhookEndpointSnapshot> CreateAsync(CreateWebhookEndpointCommand command)
    {
        if (_state.State.EndpointId != Guid.Empty)
            throw new InvalidOperationException("Webhook endpoint already exists");

        var (orgId, _, endpointId) = GrainKeys.ParseOrgEntity(this.GetPrimaryKeyString());

        _state.State = new WebhookEndpointState
        {
            OrgId = orgId,
            EndpointId = endpointId,
            MerchantId = orgId, // Simplified
            Url = command.Url,
            Description = command.Description,
            EnabledEvents = command.EnabledEvents.ToList(),
            Secret = command.Secret,
            Enabled = true,
            Status = "enabled",
            CreatedAt = DateTime.UtcNow,
            Version = 1
        };

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public async Task<WebhookEndpointSnapshot> UpdateAsync(UpdateWebhookEndpointCommand command)
    {
        EnsureExists();

        if (command.Url != null) _state.State.Url = command.Url;
        if (command.Description != null) _state.State.Description = command.Description;
        if (command.EnabledEvents != null) _state.State.EnabledEvents = command.EnabledEvents.ToList();
        if (command.Enabled.HasValue)
        {
            _state.State.Enabled = command.Enabled.Value;
            _state.State.Status = command.Enabled.Value ? "enabled" : "disabled";
        }

        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;

        await _state.WriteStateAsync();
        return CreateSnapshot();
    }

    public Task<WebhookEndpointSnapshot> GetSnapshotAsync()
    {
        EnsureExists();
        return Task.FromResult(CreateSnapshot());
    }

    public Task<bool> ExistsAsync()
    {
        return Task.FromResult(_state.State.EndpointId != Guid.Empty);
    }

    public async Task DeleteAsync()
    {
        EnsureExists();
        _state.State.Enabled = false;
        _state.State.Status = "deleted";
        _state.State.UpdatedAt = DateTime.UtcNow;
        _state.State.Version++;
        await _state.WriteStateAsync();
    }

    public async Task RecordDeliveryAttemptAsync(int statusCode, bool success, string? error)
    {
        EnsureExists();

        var delivery = new WebhookDeliveryState
        {
            AttemptedAt = DateTime.UtcNow,
            StatusCode = statusCode,
            Success = success,
            Error = error
        };

        _state.State.RecentDeliveries.Add(delivery);
        _state.State.LastDeliveryAt = DateTime.UtcNow;

        // Keep only recent deliveries
        if (_state.State.RecentDeliveries.Count > MaxRecentDeliveries)
        {
            _state.State.RecentDeliveries = _state.State.RecentDeliveries
                .OrderByDescending(d => d.AttemptedAt)
                .Take(MaxRecentDeliveries)
                .ToList();
        }

        await _state.WriteStateAsync();
    }

    public async Task EnableAsync()
    {
        EnsureExists();
        _state.State.Enabled = true;
        _state.State.Status = "enabled";
        _state.State.UpdatedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DisableAsync()
    {
        EnsureExists();
        _state.State.Enabled = false;
        _state.State.Status = "disabled";
        _state.State.UpdatedAt = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<bool> ShouldReceiveEventAsync(string eventType)
    {
        if (!_state.State.Enabled)
            return Task.FromResult(false);

        // Check if "*" (all events) is enabled or specific event is enabled
        var shouldReceive = _state.State.EnabledEvents.Contains("*") ||
                           _state.State.EnabledEvents.Contains(eventType) ||
                           _state.State.EnabledEvents.Any(e => eventType.StartsWith(e.TrimEnd('*')));

        return Task.FromResult(shouldReceive);
    }

    private void EnsureExists()
    {
        if (_state.State.EndpointId == Guid.Empty)
            throw new InvalidOperationException("Webhook endpoint not found");
    }

    private WebhookEndpointSnapshot CreateSnapshot()
    {
        return new WebhookEndpointSnapshot(
            _state.State.EndpointId,
            _state.State.MerchantId,
            _state.State.Url,
            _state.State.Description,
            _state.State.EnabledEvents,
            _state.State.Enabled,
            _state.State.Status,
            _state.State.LastDeliveryAt,
            _state.State.RecentDeliveries.Select(d => new WebhookDeliveryAttempt(
                d.AttemptedAt,
                d.StatusCode,
                d.Success,
                d.Error)).ToList(),
            _state.State.CreatedAt,
            _state.State.UpdatedAt);
    }
}
