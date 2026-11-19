using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

internal sealed record MetadataStudyMeasurement(IReadOnlyList<MetadataStudyMetric> Metrics,MetadataPhysicalAccounting Accounting,
    string HierarchySha256,string GeometrySha256,string ConnectionGraphSha256,long Work);

internal static class MetadataStudyMetrics
{
    private static (string Id,string Unit)[] Descriptors(string unit)=>[
        ("nodes","count"),("explicit-connections","count"),("weak-components","count"),("explicit-cycle-rank","count"),
        ("branch-fraction","ratio"),("physical-sites","count"),("solid-domain-coverage","ratio"),
        ("mean-directed-route-detour","ratio"),("directed-route-length",unit),("regions","count"),("interior-rooms","count")];

    internal static MetadataStudyMeasurement Measure(WorldMetadataDocument world,long maximumWork,CancellationToken token)
    {
        var budget=new WorldSpatialWorkBudget(maximumWork,token,failureCode:"METADATA_STUDY_MEASUREMENT_WORK_BUDGET");
        budget.Charge(world.Nodes.Count+world.Connections.Count);
        var nodes=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var neighbors=nodes.Keys.ToDictionary(id=>id,_=>new HashSet<string>(StringComparer.Ordinal),StringComparer.Ordinal);
        var parent=nodes.Keys.ToDictionary(id=>id,id=>id,StringComparer.Ordinal);
        string Find(string id)
        {
            while(parent[id]!=id){budget.Charge();parent[id]=parent[parent[id]];id=parent[id];}return id;
        }
        int components=nodes.Count;
        foreach(var edge in world.Connections)
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            if(edge.FromId!=edge.ToId){neighbors[edge.FromId].Add(edge.ToId);neighbors[edge.ToId].Add(edge.FromId);}
            string a=Find(edge.FromId),b=Find(edge.ToId);if(a!=b){parent[a]=b;components--;}
        }
        var children=world.Nodes.Where(n=>n.ParentId is not null).GroupBy(n=>n.ParentId!).ToDictionary(g=>g.Key,g=>g.Select(n=>n.Id).ToArray(),StringComparer.Ordinal);
        var signatures=new Dictionary<string,string>(StringComparer.Ordinal);
        // Generation emits parents before children; reverse order avoids native
        // stack recursion for large nested catalogs.
        foreach(var node in world.Nodes.Reverse())
        {
            budget.Charge();
            signatures[node.Id]=MetadataExecutionCache.Hash(new{node.Kind,node.ArchetypeId,
                Children=children.GetValueOrDefault(node.Id,[]).Select(id=>signatures[id]).Order(StringComparer.Ordinal).ToArray()});
        }
        var paths=new List<WorldRouteResult>();
        foreach(var r in world.RoutePlan?.Routes??[]){paths.Add(r.Forward);if(r.Reverse is {} reverse)paths.Add(reverse);}
        foreach(var network in world.NetworkPlan?.Networks??[])
        foreach(var r in network.Plan.Edges){paths.Add(r.Forward);if(r.Reverse is {} reverse)paths.Add(reverse);}
        foreach(var interior in world.InteriorPlan?.Results??[])
        foreach(var r in interior.Routes){paths.Add(r.Forward);if(r.Reverse is {} reverse)paths.Add(reverse);}
        double distance=0,detour=0;int detours=0;
        foreach(var path in paths)
        {
            token.ThrowIfCancellationRequested();var points=path.Path!.Points;double length=0;
            for(int i=1;i<points.Count;i++){budget.Charge();length+=points[i-1].DistanceTo(points[i]);}
            distance+=length;double straight=points[0].DistanceTo(points[^1]);
            if(straight>0){detour+=length/straight;detours++;}
        }
        var accounting=new MetadataPhysicalAccountingPlanner().Measure(world,maximumWork,token);budget.Charge(accounting.Work);
        double? coverage=accounting.Status==MetadataAccountingStatus.Measured&&accounting.GrossDomainArea is >0&&accounting.SolidUnionArea is {} solid?
            solid/accounting.GrossDomainArea.Value:null;
        var metrics=new MetadataStudyMetric[]{
            new("nodes","count",nodes.Count,1),new("explicit-connections","count",world.Connections.Count,1),
            new("weak-components","count",components,1),new("explicit-cycle-rank","count",world.Connections.Count-nodes.Count+components,1),
            new("branch-fraction","ratio",nodes.Count==0?null:(double)neighbors.Count(p=>p.Value.Count>=3)/nodes.Count,nodes.Count),
            new("physical-sites","count",world.AccessPlan?.Sites.Count,world.AccessPlan is null?0:1),
            new("solid-domain-coverage","ratio",coverage,coverage is null?0:1),
            new("mean-directed-route-detour","ratio",detours==0?null:detour/detours,detours),
            new("directed-route-length",world.Space.Unit,paths.Count==0?null:distance,paths.Count),
            new("regions","count",world.RegionPlan?.Regions.Count,world.RegionPlan is null?0:1),
            new("interior-rooms","count",world.InteriorPlan?.Results.Sum(r=>r.RoomNodeIds.Count),world.InteriorPlan?.Results.Count??0)};
        if(metrics.Any(m=>m.Value is {} v&&!double.IsFinite(v)))throw new ArgumentException("METADATA_STUDY_METRIC_NONFINITE");
        budget.Charge((world.AccessPlan?.Sites.Count??0)+paths.Count);
        string geometry=MetadataExecutionCache.Hash(new{world.Space,world.Surface,
            InteriorGeometry=world.InteriorPlan?.Results.Select(r=>new{r.Walls,r.Openings}).ToArray(),
            Sites=(world.AccessPlan?.Sites??[]).Select(s=>MetadataExecutionCache.Hash(new{s.Occupancy,s.Footprint,
                Ports=s.Ports.Select(p=>new{p.BoundaryPosition,p.ApproachPosition,p.Width}).ToArray()})).Order(StringComparer.Ordinal).ToArray(),
            Paths=paths.Select(p=>MetadataExecutionCache.Hash(p.Path)).Order(StringComparer.Ordinal).ToArray()});
        string graph=MetadataExecutionCache.Hash(new{Nodes=world.Nodes.OrderBy(n=>n.Id,StringComparer.Ordinal).Select(n=>new{n.Id,n.Kind}).ToArray(),
            Edges=world.Connections.OrderBy(e=>e.Id,StringComparer.Ordinal).Select(e=>new{e.Id,e.Kind,e.FromId,e.ToId,e.Bidirectional}).ToArray()});
        return new(Array.AsReadOnly(metrics),accounting,signatures[world.Nodes.Single(n=>n.ParentId is null).Id],geometry,graph,budget.Used);
    }

    internal static IReadOnlyList<MetadataStudyHistogram> Histograms(MetadataStudyDefinition definition,IReadOnlyList<MetadataStudySample> samples)
    {
        var result=new List<MetadataStudyHistogram>();
        foreach(var cohort in definition.Cases.GroupBy(c=>c.Cohort).OrderBy(g=>g.Key,StringComparer.Ordinal))
        {
            var selected=samples.Where(s=>s.Cohort==cohort.Key).ToArray();int generated=selected.Count(s=>s.Outcome==MetadataStudyOutcome.Generated);
            foreach(var descriptor in Descriptors(cohort.First().Recipe.Space.Unit))
            {
                var values=selected.SelectMany(s=>s.Metrics.Where(m=>m.Id==descriptor.Id&&m.Unit==descriptor.Unit&&m.Value is not null).Select(m=>m.Value!.Value)).ToArray();
                double? min=values.Length==0?null:values.Min(),max=values.Length==0?null:values.Max();var bins=new int[definition.HistogramBins];
                foreach(double value in values)
                {
                    int index=min==max?0:Math.Clamp((int)Math.Floor((value-min!.Value)/(max!.Value-min.Value)*bins.Length),0,bins.Length-1);bins[index]++;
                }
                result.Add(new(cohort.Key,descriptor.Id,descriptor.Unit,generated,selected.Length-generated,values.Length,generated-values.Length,
                    min,max,values.Length==0?null:values.Average(),Array.AsReadOnly(bins)));
            }
        }
        return result.AsReadOnly();
    }
}
