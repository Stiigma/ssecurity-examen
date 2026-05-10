namespace ExamenSecurity.Api.Options;

public sealed class ExternalLogForwardingOptions
{
    public const string SectionName = "SecurityLogForwarding";

    public bool Enabled { get; set; } = true;
    public ExternalLogForwardingFileOptions File { get; set; } = new();
    public ExternalLogForwardingStdoutOptions Stdout { get; set; } = new();
}

public sealed class ExternalLogForwardingFileOptions
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "logs/security-events.jsonl";
    public int MaxFileSizeMb { get; set; } = 10;
    public int RetainFiles { get; set; } = 30;
}

public sealed class ExternalLogForwardingStdoutOptions
{
    public bool Enabled { get; set; } = true;
}
