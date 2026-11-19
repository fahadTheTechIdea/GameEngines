namespace DialogueEngine;

/// <summary>Whether an answer can be taken now.</summary>
public enum OptionAvailability { Available, ShownDisabled, Hidden }

/// <summary>Why an answer is available, shown disabled or hidden: <paramref name="Why"/> is its condition with the values the facts have, or says it has none.</summary>
public sealed record OptionExplanation(string Id, OptionAvailability Availability, string Why);