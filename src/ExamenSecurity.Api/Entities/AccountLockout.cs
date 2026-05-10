namespace ExamenSecurity.Api.Entities;

public enum LockoutTargetType
{
    User = 1,
    IpAddress = 2
}

public sealed class AccountLockout
{
    public Guid Id { get; set; }
    public LockoutTargetType TargetType { get; set; }
    public string TargetValue { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset LockedUntilUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? AlertId { get; set; }
    public SecurityAlert? Alert { get; set; }
    public DateTimeOffset? UnlockedAtUtc { get; set; }
    public Guid? UnlockedByUserId { get; set; }
    public AppUser? UnlockedByUser { get; set; }
}
