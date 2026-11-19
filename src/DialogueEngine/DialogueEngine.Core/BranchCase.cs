namespace DialogueEngine;

public sealed record BranchCase(Guard When, string Next);