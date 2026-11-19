using System.Text.Json;
using System.Text;
namespace WorldCreator.Core.Operations;

public enum OperationSeverity { Debug, Information, Warning, Error, Critical }
public sealed record OperationEvent(
    string EventId, DateTimeOffset OccurredUtc, OperationSeverity Severity, string Source,
    string Operation, string CorrelationId, string Message, string? TargetId = null,
    string? Actor = null, string? Outcome = null,IReadOnlyDictionary<string,string>? Context = null);

public interface IOperationLog
{
    Task WriteAsync(OperationEvent entry, CancellationToken cancellationToken = default);
}
public interface IOperationAudit
{
    Task RecordAsync(OperationEvent entry, CancellationToken cancellationToken = default);
}
public interface IOperationNotifications
{
    Task PublishAsync(OperationEvent entry, CancellationToken cancellationToken = default);
}

/// <summary>Append-only JSONL writer; serializes each instance and retries bounded
/// Windows sharing conflicts at open time. Write failures are never replayed.</summary>
public sealed class JsonLineOperationStore(string filePath) : IOperationLog, IOperationAudit, IOperationNotifications
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _path = Path.GetFullPath(filePath);
    public async Task WriteAsync(OperationEvent entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
            await using var stream=await OpenAppendAsync(cancellationToken);
            await stream.WriteAsync(Encoding.UTF8.GetBytes(line),cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally { _mutex.Release(); }
    }
    private async Task<FileStream> OpenAppendAsync(CancellationToken token)
    {
        int[] delays=[25,75,225,675];
        for(int attempt=0;;attempt++)
        {
            token.ThrowIfCancellationRequested();
            try{return new FileStream(_path,FileMode.Append,FileAccess.Write,FileShare.Read,4096,FileOptions.Asynchronous);}
            catch(IOException error)when(OperatingSystem.IsWindows()&&(error.HResult&0xffff) is 32 or 33&&attempt<delays.Length)
            {
                // Only opening is retried: no bytes have been written, so a partial
                // record can never be duplicated by retrying an append operation.
                await Task.Delay(delays[attempt],token);
            }
        }
    }
    public Task RecordAsync(OperationEvent entry, CancellationToken cancellationToken = default)
        => WriteAsync(entry, cancellationToken);
    public Task PublishAsync(OperationEvent entry, CancellationToken cancellationToken = default)
        => WriteAsync(entry, cancellationToken);
}
/// <summary>Routes notifications to a configured durable JSONL channel and to standard error for warnings/failures.</summary>
public sealed class ConsoleAndFileNotificationService(JsonLineOperationStore store) : IOperationNotifications
{
    public async Task PublishAsync(OperationEvent entry, CancellationToken cancellationToken = default)
    {
        await store.PublishAsync(entry, cancellationToken);
        if (entry.Severity >= OperationSeverity.Warning)
            Console.Error.WriteLine($"[{entry.EventId}] {entry.Message} (correlation: {entry.CorrelationId})");
    }
}
public sealed class WorldOperationReporter(IOperationLog log, IOperationAudit audit, IOperationNotifications notifications)
{
    public async Task ReportAsync(OperationEvent entry, bool notify = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if(entry.Context is {} context)
        {
            if(context.Count>32||context.Any(p=>string.IsNullOrEmpty(p.Key)||p.Key.Length>64||p.Key.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not '_' and not '-' and not '.')||p.Value is null||p.Value.Length>2048))
                throw new ArgumentException("WORLD_OPERATION_CONTEXT_INVALID");
            entry=entry with{Context=new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(new Dictionary<string,string>(context,StringComparer.Ordinal))};
        }
        await log.WriteAsync(entry, cancellationToken);
        await audit.RecordAsync(entry, cancellationToken);
        if (notify) await notifications.PublishAsync(entry, cancellationToken);
    }
}
