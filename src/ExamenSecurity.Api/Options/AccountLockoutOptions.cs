namespace ExamenSecurity.Api.Options;

public sealed class AccountLockoutOptions
{
    public const string SectionName = "AccountLockout";

    public bool Enabled { get; set; } = true;

    public int BaseDurationMinutes { get; set; } = 15;

    public int MaxFailedAttemptsBeforeLockout { get; set; } = 5;

    public bool LockIpAddress { get; set; } = true;

    public bool LockUserAccount { get; set; } = true;
}
