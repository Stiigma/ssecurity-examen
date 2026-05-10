using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using ExamenSecurity.Api.Entities;
using ExamenSecurity.Api.Options;
using Microsoft.Extensions.Options;

namespace ExamenSecurity.Api.Services;

public interface IExternalLogForwarder : IDisposable
{
    void Forward(SecurityEvent securityEvent);
}

public sealed class ExternalLogForwarder : IExternalLogForwarder
{
    private readonly Channel<SecurityEvent> _channel;
    private readonly Task _consumerTask;
    private readonly ExternalLogForwardingOptions _options;
    private readonly ILogger<ExternalLogForwarder> _logger;
    private readonly ILogger _securityEventLogger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private string _currentFilePath;
    private bool _disposed;

    public ExternalLogForwarder(
        IOptions<ExternalLogForwardingOptions> options,
        ILogger<ExternalLogForwarder> logger,
        ILoggerFactory loggerFactory)
    {
        _options = options.Value;
        _logger = logger;
        _securityEventLogger = loggerFactory.CreateLogger("ExamenSecurity.SecurityEvent");
        _currentFilePath = _options.File?.Path ?? "logs/security-events.jsonl";

        _channel = Channel.CreateUnbounded<SecurityEvent>();
        _consumerTask = Task.Run(ProcessChannelAsync);
    }

    public void Forward(SecurityEvent securityEvent)
    {
        if (_disposed)
        {
            return;
        }

        if (!_options.Enabled)
        {
            return;
        }

        _channel.Writer.TryWrite(securityEvent);
    }

    private async Task ProcessChannelAsync()
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync())
        {
            try
            {
                await ForwardToFileAsync(evt);
                ForwardToStdout(evt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to forward security event {EventId}", evt.Id);
            }
        }
    }

    private async Task ForwardToFileAsync(SecurityEvent evt)
    {
        if (!(_options.File?.Enabled ?? false))
        {
            return;
        }

        var json = SerializeEvent(evt);
        var filePath = _currentFilePath;

        await _fileLock.WaitAsync();
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var fileInfo = new FileInfo(filePath);
            var maxSizeBytes = (_options.File?.MaxFileSizeMb ?? 10) * 1024L * 1024L;

            if (fileInfo.Exists && fileInfo.Length > maxSizeBytes)
            {
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                var extension = Path.GetExtension(filePath);
                var directoryName = fileInfo.DirectoryName ?? ".";
                var rotatedPath = Path.Combine(
                    directoryName,
                    $"{fileName}-{DateTimeOffset.UtcNow:yyyy-MM-dd-HH-mm-ss}{extension}");

                File.Move(filePath, rotatedPath);
            }

            await using var stream = new FileStream(
                filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read);
            await using var writer = new StreamWriter(stream);
            await writer.WriteLineAsync(json);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private void ForwardToStdout(SecurityEvent evt)
    {
        if (!(_options.Stdout?.Enabled ?? false))
        {
            return;
        }

        var json = SerializeEvent(evt);
        _securityEventLogger.LogInformation("{SecurityEventJson}", json);
    }

    private static string SerializeEvent(SecurityEvent evt)
    {
        var obj = new
        {
            evt.Id,
            evt.EventType,
            evt.Severity,
            evt.UserId,
            evt.Username,
            evt.Role,
            evt.IpAddress,
            evt.UserAgent,
            evt.HttpMethod,
            evt.Path,
            evt.StatusCode,
            evt.ResourceType,
            evt.ResourceId,
            evt.Outcome,
            evt.Message,
            MetadataJson = evt.MetadataJson,
            evt.CorrelationId,
            evt.CreatedAtUtc,
            evt.EventHash,
            evt.PreviousHash
        };

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(obj, options);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _channel.Writer.Complete();
            _consumerTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing ExternalLogForwarder");
        }

        _fileLock.Dispose();
    }
}
