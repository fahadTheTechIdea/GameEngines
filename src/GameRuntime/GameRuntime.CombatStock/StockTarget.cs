using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime;

/// <summary>An entity with a storage and one resource of the content: what a stock line or a stock move acts on.</summary>
public readonly record struct StockTarget(Entity Holder, IInventory Stock, ResourceId Resource)
{
    /// <summary>Finds the holder by entity id and the resource by name; a refusal code when either does not exist or the holder has no storage.</summary>
    public static Code Find(World world, int entity, string resource, out StockTarget target)
    {
        target = default;
        var holder = world.Entities.Get(new EntityId(entity));
        if (holder == null || !world.Content.TryResource(resource, out var id)) return Codes.NotFound;
        var stock = holder.Get<IInventory>();
        if (stock == null) return Codes.NotFound;
        target = new StockTarget(holder, stock, id);
        return Code.None;
    }
}
