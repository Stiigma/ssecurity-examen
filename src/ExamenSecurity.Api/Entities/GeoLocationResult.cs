namespace ExamenSecurity.Api.Entities;

public sealed class GeoLocationResult
{
    public string CountryCode { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool IsVpnOrProxy { get; set; }
    public bool IsLocal { get; set; }
}
