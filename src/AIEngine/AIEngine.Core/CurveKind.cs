namespace AIEngine;

/// <summary>The shape that turns a consideration input, 0 to 1000, into a score, 0 to 1000. All integer: no floating point, so no machine can differ.</summary>
public enum CurveKind { Linear, Quadratic, Cubic, Smooth, Step }