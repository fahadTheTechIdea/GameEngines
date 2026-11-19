namespace GameRuntime.Combat.Headless;

/// <summary>The settings every commander bot is built from: where the sides start, how long each round lasts and the ground they fight over. How often a bot acts and how close it comes are in its behaviour document.</summary>
public sealed record SkirmishPlan(int StartMetres, int RoundSeconds, string Terrain);
