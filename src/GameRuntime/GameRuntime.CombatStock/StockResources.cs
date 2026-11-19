namespace GameRuntime;

/// <summary>The one place the names of the stock-flow resources are made: people, and one ammunition resource per ammunition type.</summary>
public static class StockResources
{
    public const string People = "stock.people";
    public const string CountUnit = "stock.unit.count";
    public const string SlotUnit = "stock.unit.slot";
    public const string SquadStore = "stock.squad-store";

    /// <summary>Platform units (vehicles, guns) a squad still has.</summary>
    public const string Platforms = "stock.platforms";

    public static string Ammo(string ammunition) => "stock.ammo." + ammunition;

    /// <summary>Fuel in thousandths of a unit of the catalog fuel resource.</summary>
    public static string Fuel(string fuel) => "stock.fuel." + fuel;
}
