using System.Globalization;
using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ExamenSecurity.Api.Services;

public sealed class ImpossibleTravelService(
    AppDbContext dbContext,
    IGeoLocationService geoLocationService,
    ISecurityAuditService securityAuditService,
    IOptions<ImpossibleTravelOptions> options,
    ILogger<ImpossibleTravelService> logger) : IImpossibleTravelService
{
    public async Task EvaluateAsync(AppUser user, string ipAddress, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;

        if (!opts.Enabled)
        {
            return;
        }

        var location = await geoLocationService.GetLocationAsync(ipAddress, cancellationToken);

        // Ignore local IPs if configured
        if (location.IsLocal && opts.IgnoreLocalIps)
        {
            logger.LogInformation("Ignoring local IP {IpAddress} for impossible travel check.", ipAddress);
            return;
        }

        // Ignore configured country codes (e.g. VPN, TOR)
        if (!string.IsNullOrEmpty(location.CountryCode) &&
            opts.IgnoredCountryCodes.Contains(location.CountryCode, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogInformation("Ignoring IP {IpAddress} from ignored country code {CountryCode}.", ipAddress, location.CountryCode);
            return;
        }

        // Save current login location
        var userLoginLocation = new UserLoginLocation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            IpAddress = ipAddress,
            CountryCode = location.CountryCode,
            CountryName = location.CountryName,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            LoginAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.UserLoginLocations.Add(userLoginLocation);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Find previous login location
        var previousLocation = await dbContext.UserLoginLocations
            .AsNoTracking()
            .Where(ull => ull.UserId == user.Id && ull.Id != userLoginLocation.Id)
            .OrderByDescending(ull => ull.LoginAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (previousLocation is null)
        {
            logger.LogInformation("No previous login location found for user {UserId}. Skipping impossible travel check.", user.Id);
            return;
        }

        // Calculate distance using Haversine formula
        var distanceKm = CalculateHaversineDistance(
            (double)previousLocation.Latitude, (double)previousLocation.Longitude,
            (double)userLoginLocation.Latitude, (double)userLoginLocation.Longitude);

        // Calculate elapsed time in hours
        var timeSpan = userLoginLocation.LoginAtUtc - previousLocation.LoginAtUtc;
        var elapsedHours = timeSpan.TotalHours;

        if (elapsedHours <= 0)
        {
            elapsedHours = 0.001; // Prevent division by zero; assume 3.6 seconds
        }

        var speedKmh = distanceKm / elapsedHours;
        var maxSpeed = opts.MaxSpeedKmh;

        logger.LogInformation(
            "User {UserId} login from {CurrentCountry} (prev: {PreviousCountry}). Distance: {Distance:F1} km, Time: {Time:F2} h, Speed: {Speed:F1} km/h",
            user.Id,
            userLoginLocation.CountryName,
            previousLocation.CountryName,
            distanceKm,
            elapsedHours,
            speedKmh);

        if (speedKmh > maxSpeed)
        {
            var metadata = new Dictionary<string, object?>
            {
                ["previousCountry"] = previousLocation.CountryName,
                ["previousCountryCode"] = previousLocation.CountryCode,
                ["currentCountry"] = userLoginLocation.CountryName,
                ["currentCountryCode"] = userLoginLocation.CountryCode,
                ["distanceKm"] = Math.Round(distanceKm, 1),
                ["elapsedHours"] = Math.Round(elapsedHours, 2),
                ["speedKmh"] = Math.Round(speedKmh, 1),
                ["maxSpeedKmh"] = maxSpeed,
                ["previousIp"] = previousLocation.IpAddress,
                ["currentIp"] = ipAddress
            };

            await securityAuditService.AuditAsync(
                new SecurityAuditRequest(
                    SecurityEventType.ImpossibleTravelDetected,
                    SecuritySeverity.High,
                    "Detected",
                    $"Viaje imposible detectado: {previousLocation.CountryName} -> {userLoginLocation.CountryName} a {speedKmh:F1} km/h (max {maxSpeed} km/h).",
                    UserId: user.Id,
                    Username: user.Email,
                    Role: user.Role,
                    Metadata: metadata),
                cancellationToken);
        }
    }

    private static double CalculateHaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth radius in kilometers

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }
}
