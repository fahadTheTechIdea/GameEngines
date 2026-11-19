namespace GameRuntime.Alchemy.Headless;

/// <summary>The command line or script was wrong. The message names the argument; nothing is guessed or defaulted around it.</summary>
public sealed class AlchemyUsageException(string message) : Exception(message);
