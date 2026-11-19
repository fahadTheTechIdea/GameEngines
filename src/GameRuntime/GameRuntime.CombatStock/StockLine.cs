namespace GameRuntime;

/// <summary>One movement of one resource of one squad's stock. An <see cref="IStockFlow.Everything"/> kind carries amount 0.</summary>
public readonly record struct StockLine(IStockFlow Flow, int Squad, string Resource, int Amount);
