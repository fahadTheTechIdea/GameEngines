namespace AIEngine;

/// <summary>The operations an expression can do. All whole-number arithmetic is exact (division and remainder truncate toward zero); <see cref="Concat"/> joins strings.</summary>
public enum OpKind { Tick, Add, Sub, Mul, Div, Mod, Min, Max, Concat }