namespace ExamenSecurity.Api.Entities;

public sealed class ApiClient
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerTeam { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DisabledAtUtc { get; set; }
}
