namespace GameRuntime.Field.Headless;

/// <summary>The command line was wrong. The message names the argument; nothing is guessed or defaulted around it.</summary>
public sealed class FieldUsageException(string message) : Exception(message);
