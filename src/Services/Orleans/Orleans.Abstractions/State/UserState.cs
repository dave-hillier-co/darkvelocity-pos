namespace DarkVelocity.Orleans.Abstractions.State;

public enum UserStatus
{
    Active,
    Inactive,
    Locked
}

public enum UserType
{
    Employee,
    Manager,
    Admin,
    Owner
}

public record UserPreferences
{
    public string? Language { get; init; }
    public string? Theme { get; init; }
    public bool ReceiveEmailNotifications { get; init; } = true;
    public bool ReceivePushNotifications { get; init; } = true;
}

[GenerateSerializer]
public sealed class UserState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public string Email { get; set; } = string.Empty;
    [Id(3)] public string DisplayName { get; set; } = string.Empty;
    [Id(4)] public string? FirstName { get; set; }
    [Id(5)] public string? LastName { get; set; }
    [Id(6)] public string? PinHash { get; set; }
    [Id(7)] public string? QrToken { get; set; }
    [Id(8)] public UserStatus Status { get; set; } = UserStatus.Active;
    [Id(9)] public UserType Type { get; set; } = UserType.Employee;
    [Id(10)] public List<Guid> SiteAccess { get; set; } = [];
    [Id(11)] public List<Guid> UserGroupIds { get; set; } = [];
    [Id(12)] public UserPreferences Preferences { get; set; } = new();
    [Id(13)] public DateTime CreatedAt { get; set; }
    [Id(14)] public DateTime? UpdatedAt { get; set; }
    [Id(15)] public DateTime? LastLoginAt { get; set; }
    [Id(16)] public int FailedLoginAttempts { get; set; }
    [Id(17)] public int Version { get; set; }
}

[GenerateSerializer]
public sealed class UserGroupState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public Guid OrganizationId { get; set; }
    [Id(2)] public string Name { get; set; } = string.Empty;
    [Id(3)] public string? Description { get; set; }
    [Id(4)] public List<Guid> MemberIds { get; set; } = [];
    [Id(5)] public bool IsSystemGroup { get; set; }
    [Id(6)] public DateTime CreatedAt { get; set; }
    [Id(7)] public DateTime? UpdatedAt { get; set; }
    [Id(8)] public int Version { get; set; }
}
