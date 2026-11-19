using Godot;
using Microsoft.Extensions.Logging;
using ScenarioEngine.Contracts;

namespace GameRuntime.GodotHost;

/// <summary>Routes ScenarioEngine's log and notices to Godot's own logging.</summary>
public sealed class GodotLogger : ILogger, INotificationSink
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var text = formatter(state, exception) + (exception is null ? "" : " :: " + exception);
        if (logLevel >= LogLevel.Error) GD.PushError(text);
        else GD.PushWarning(text);
    }
    public void Publish(EngineNotice notice) => GD.Print($"[notice] {notice.Code}: {notice.Message}");
}
