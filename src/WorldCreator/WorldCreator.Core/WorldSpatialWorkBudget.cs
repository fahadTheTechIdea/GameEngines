namespace WorldCreator.Core.Spatial;

/// <summary>Shared aggregate accounting for one bounded spatial operation.</summary>
internal sealed class WorldSpatialWorkBudget(long maximum, CancellationToken token,WorldSpatialWorkBudget? parent=null,string failureCode="WORLD_ROUTE_WORK_BUDGET")
{
    public long Used { get; private set; }
    public void Charge(long amount = 1)
    {
        token.ThrowIfCancellationRequested();
        if (amount < 0 || amount > maximum - Used)
        {
            var error=new ArgumentException(failureCode);
            error.Data["MaximumWork"]=maximum;error.Data["UsedWork"]=Used;error.Data["RequestedWork"]=amount;
            throw error;
        }
        parent?.Charge(amount);
        Used += amount;
    }
}
