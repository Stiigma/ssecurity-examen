using System.Diagnostics;
using System.Net;
using System.Text.Json;
using ExamenSecurity.Api.Data;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ExamenSecurity.Api.Tests;

public class ExternalLogForwardingTests : IDisposable
{
    private readonly List<string> _filesToCleanup = new();

    public void Dispose()
    {
        foreach (var file in _filesToCleanup)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }

                var directory = Path.GetDirectoryName(file);
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    var rotatedFiles = Directory.GetFiles(directory, $"{Path.GetFileNameWithoutExtension(file)}-*");
                    foreach (var rotated in rotatedFiles)
                    {
                        File.Delete(rotated);
                    }
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public async Task EventForwardedToFile_AfterAudit_CreatesJsonLine()
    {
        // Arrange
        var logFilePath = GetTempLogPath();
        var factory = CreateFactory(logFilePath);
        var client = factory.CreateClient();

        // Act: generate a security event via successful login
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "student1@demo.local",
            password = "Student123!"
        });

        // Assert request succeeded
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Wait for background forwarder to write
        await WaitForFileContentAsync(logFilePath, TimeSpan.FromSeconds(5));

        var lines = (await File.ReadAllLinesAsync(logFilePath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        Assert.NotEmpty(lines);

        var lastLine = lines.Last();
        using var json = JsonDocument.Parse(lastLine);
        var root = json.RootElement;

        Assert.Equal("LoginSucceeded", root.GetProperty("eventType").GetString());
        Assert.NotNull(root.GetProperty("correlationId").GetString());
        Assert.NotNull(root.GetProperty("createdAtUtc").GetString());
    }

    [Fact]
    public async Task EventForwardedToFile_SanitizesSensitiveMetadata()
    {
        // Arrange
        var logFilePath = GetTempLogPath();
        var factory = CreateFactory(logFilePath);

        using var scope = factory.Services.CreateScope();
        var auditService = scope.ServiceProvider.GetRequiredService<ISecurityAuditService>();

        // Act: audit an event with sensitive metadata
        await auditService.AuditAsync(
            new SecurityAuditRequest(
                SecurityEventType.LoginFailed,
                SecuritySeverity.Warning,
                "Rejected",
                "Test login failed for sanitization",
                Metadata: new Dictionary<string, object?>
                {
                    ["password"] = "secret123",
                    ["normalField"] = "visibleValue"
                }),
            CancellationToken.None);

        await WaitForFileContentAsync(logFilePath, TimeSpan.FromSeconds(5));

        // Assert
        var lines = (await File.ReadAllLinesAsync(logFilePath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        Assert.NotEmpty(lines);

        var lastLine = lines.Last();
        using var json = JsonDocument.Parse(lastLine);
        var metadataJson = json.RootElement.GetProperty("metadataJson").GetString();

        Assert.NotNull(metadataJson);
        Assert.Contains("***redacted***", metadataJson);
        Assert.DoesNotContain("secret123", metadataJson);
        Assert.Contains("visibleValue", metadataJson);
    }

    [Fact]
    public async Task FileRotation_WhenMaxSizeExceeded_CreatesNewFile()
    {
        // Arrange: pre-create a file that already exceeds the tiny max size
        var logFilePath = GetTempLogPath();
        await File.WriteAllTextAsync(logFilePath, new string('x', 1024));

        var factory = new CustomWebApplicationFactory().WithConfiguration(new Dictionary<string, string?>
        {
            ["SecurityLogForwarding:Enabled"] = "true",
            ["SecurityLogForwarding:File:Enabled"] = "true",
            ["SecurityLogForwarding:File:Path"] = logFilePath,
            ["SecurityLogForwarding:File:MaxFileSizeMb"] = "0",
            ["SecurityLogForwarding:Stdout:Enabled"] = "false"
        });
        var client = factory.CreateClient();

        // Act: generate an event
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "student1@demo.local",
            password = "Student123!"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await WaitForFileContentAsync(logFilePath, TimeSpan.FromSeconds(5));

        // Assert: rotated file should exist
        var directory = Path.GetDirectoryName(logFilePath)!;
        var fileName = Path.GetFileNameWithoutExtension(logFilePath);
        var rotatedFiles = Directory.GetFiles(directory, $"{fileName}-*.jsonl");
        Assert.NotEmpty(rotatedFiles);

        // The new file should contain the event
        var newLines = (await File.ReadAllLinesAsync(logFilePath))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();
        Assert.NotEmpty(newLines);

        using var json = JsonDocument.Parse(newLines.Last());
        Assert.Equal("LoginSucceeded", json.RootElement.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task FileWriteFailure_DoesNotBreakRequest()
    {
        // Arrange: use an invalid path that cannot be written
        string invalidPath;
        if (OperatingSystem.IsWindows())
        {
            invalidPath = @"C:\Windows\System32\security-events-test.jsonl";
        }
        else
        {
            invalidPath = "/root/cannot-write-here/security-events.jsonl";
        }

        var factory = new CustomWebApplicationFactory().WithConfiguration(new Dictionary<string, string?>
        {
            ["SecurityLogForwarding:Enabled"] = "true",
            ["SecurityLogForwarding:File:Enabled"] = "true",
            ["SecurityLogForwarding:File:Path"] = invalidPath,
            ["SecurityLogForwarding:Stdout:Enabled"] = "false"
        });
        var client = factory.CreateClient();

        // Act: this should succeed even though file writing will fail
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "student1@demo.local",
            password = "Student123!"
        });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private CustomWebApplicationFactory CreateFactory(string logFilePath)
    {
        _filesToCleanup.Add(logFilePath);
        return new CustomWebApplicationFactory().WithConfiguration(new Dictionary<string, string?>
        {
            ["SecurityLogForwarding:Enabled"] = "true",
            ["SecurityLogForwarding:File:Enabled"] = "true",
            ["SecurityLogForwarding:File:Path"] = logFilePath,
            ["SecurityLogForwarding:Stdout:Enabled"] = "false"
        });
    }

    private static string GetTempLogPath()
    {
        return Path.Combine(Path.GetTempPath(), $"security-events-{Guid.NewGuid()}.jsonl");
    }

    private static async Task WaitForFileContentAsync(string path, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (File.Exists(path))
            {
                var info = new FileInfo(path);
                if (info.Length > 0)
                {
                    // Give extra time for the write to fully complete
                    await Task.Delay(200);
                    return;
                }
            }

            await Task.Delay(100);
        }
    }
}
