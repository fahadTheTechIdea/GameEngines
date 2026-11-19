namespace GameRuntime.Headless;

public enum PlayerActionKind { Sell, Speed }

/// <summary>What a policy wants done this tick.</summary>
public readonly record struct PlayerAction(PlayerActionKind Kind, int A, int B)
{
    public static PlayerAction Sell(int units) => new(PlayerActionKind.Sell, units, 0);
    public static PlayerAction Speed(int numerator, int denominator) => new(PlayerActionKind.Speed, numerator, denominator);
}
