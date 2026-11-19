using AlchemyEngine;

namespace GameContent.Catalog;

/// <summary>The alchemy content a pack became, and the notes on everything that was reduced or left out.</summary>
public sealed record AlchemyConversion(AlchemyContent Content, ConversionReport Report, int RecipeCount, int ElementCount);
