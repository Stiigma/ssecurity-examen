namespace ExamenSecurity.Api.Options;

public sealed class LogIntegrityOptions
{
    public const string SectionName = "LogIntegrity";

    public bool Enabled { get; set; } = true;
    public string HmacSecretKey { get; set; } = string.Empty;
}
