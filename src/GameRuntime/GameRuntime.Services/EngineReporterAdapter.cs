using ScenarioEngine.Contracts;

namespace GameRuntime;

/// <summary>
/// The runtime's one failure path into ScenarioEngine's reporter, so log, audit and exceptions share one service.
/// Every fault the session catches arrives here with its exception.
/// </summary>
public sealed class EngineReporterAdapter(IEngineReporter reporter) : IRuntimeReporter
{
    public void Report(string code, string message, Exception? exception) =>
        reporter.Report(new EngineNotice(code, message, Severity.Error, null, exception));
}
