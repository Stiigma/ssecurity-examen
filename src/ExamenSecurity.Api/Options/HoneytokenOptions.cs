namespace ExamenSecurity.Api.Options;

public sealed class HoneytokenOptions
{
    public const string SectionName = "Honeytoken";

    public bool Enabled { get; set; } = true;
    public bool EnableDelay { get; set; } = true;
    public int DelayMinMs { get; set; } = 2000;
    public int DelayMaxMs { get; set; } = 5000;
}
