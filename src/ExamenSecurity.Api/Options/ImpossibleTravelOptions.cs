namespace ExamenSecurity.Api.Options;

public sealed class ImpossibleTravelOptions
{
    public const string SectionName = "ImpossibleTravel";

    public bool Enabled { get; set; } = true;

    public double MaxSpeedKmh { get; set; } = 900;

    public bool IgnoreLocalIps { get; set; } = true;

    public List<string> IgnoredCountryCodes { get; set; } = ["VPN", "TOR"];
}
