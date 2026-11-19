using System.Collections.ObjectModel;

namespace WorldCreator.Core.Planning;

/// <summary>One atomic owner for shared source/link accounting. Quantities are authored game units.</summary>
public sealed class ServiceCapacityLedger
{
    private readonly Dictionary<string,decimal> limits;
    private readonly Dictionary<string,decimal> used;
    public ServiceCapacityLedger(IReadOnlyDictionary<string,double> capacities)
    {
        ArgumentNullException.ThrowIfNull(capacities);
        if(capacities.Count>100000||capacities.Any(p=>string.IsNullOrWhiteSpace(p.Key)))throw new ArgumentException("SERVICE_CAPACITY_INVALID");
        limits=capacities.ToDictionary(p=>p.Key,p=>Quantity(p.Value),StringComparer.Ordinal);
        used=limits.ToDictionary(p=>p.Key,_=>0m,StringComparer.Ordinal);
    }
    private static decimal Quantity(double value)
    {
        if(!double.IsFinite(value)||value<0||value>1e18)throw new ArgumentException("SERVICE_CAPACITY_QUANTITY_INVALID");
        decimal result=(decimal)value;
        if(value>0&&result==0)throw new ArgumentException("SERVICE_CAPACITY_QUANTITY_INVALID");
        return result;
    }
    public bool CanReserve(string id,double quantity)
    {
        if(!limits.TryGetValue(id,out var maximum))throw new ArgumentException("SERVICE_CAPACITY_UNKNOWN:"+id);
        return Quantity(quantity)<=maximum-used[id];
    }
    public bool TryReserve(IReadOnlyDictionary<string,double> request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var values=request.ToDictionary(p=>p.Key,p=>Quantity(p.Value),StringComparer.Ordinal);
        foreach(var (id,value) in values)
        {
            if(!limits.TryGetValue(id,out var maximum))throw new ArgumentException("SERVICE_CAPACITY_UNKNOWN:"+id);
            if(value>maximum-used[id])return false;
        }
        foreach(var (id,value) in values)used[id]+=value;
        return true;
    }
    public IReadOnlyDictionary<string,double> Usage=>new ReadOnlyDictionary<string,double>(used.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>p.Key,p=>(double)p.Value,StringComparer.Ordinal));
}
