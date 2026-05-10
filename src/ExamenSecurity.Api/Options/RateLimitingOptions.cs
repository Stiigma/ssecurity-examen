namespace ExamenSecurity.Api.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public bool Enabled { get; set; } = true;

    public List<string> WhitelistedIps { get; set; } = [];

    public List<string> ExcludedPaths { get; set; } = [];

    public RateLimitRule Auth { get; set; } = new();

    public RateLimitRule Admin { get; set; } = new();

    public RateLimitRule General { get; set; } = new();
}

public sealed class RateLimitRule
{
    public int MaxRequests { get; set; }

    public int WindowSeconds { get; set; }
}
