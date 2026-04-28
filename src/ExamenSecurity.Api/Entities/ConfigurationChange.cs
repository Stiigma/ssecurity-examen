namespace ExamenSecurity.Api.Entities;

public sealed class ConfigurationChange
{
    public Guid Id { get; set; }
    public Guid ChangedByUserId { get; set; }
    public AppUser ChangedByUser { get; set; } = null!;
    public string SettingKey { get; set; } = string.Empty;
    public string PreviousValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public DateTimeOffset ChangedAtUtc { get; set; }
}
