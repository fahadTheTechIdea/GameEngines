namespace SpatialEngine;

/// <summary>A cell of the grid, counted from the top-left corner.</summary>
public readonly record struct Cell(int X, int Y);

/// <summary>A point in millimetres. All space is whole millimetres: there is no floating point anywhere in the library.</summary>
public readonly record struct Point(long X, long Y);