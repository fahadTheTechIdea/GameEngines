using System.Text.Json;
using WorldCreator.Core.Geometry;
using static WorldCreator.Core.Spatial.WorldRegionGeometry;

namespace WorldCreator.Core.Spatial;

public sealed record WorldRegionSeed(string Id, string Purpose, string SourceRuleId, WorldPoint Position, double MinimumArea,WorldSuitabilityProfile? Profile=null);
public enum WorldRegionRepresentation { TerrainFaces, Membership }
public sealed record WorldRegionRequest(string Id, string Purpose, string SourceRuleId, WorldPolygon Boundary,
    WorldSuitabilityProfile Profile, IReadOnlyList<WorldRegionSeed> Seeds, int MaximumCells=1024, long MaximumWork=50000000,
    IReadOnlyList<WorldRegionIntent>? Intents=null,double AnchorPrecision=.1,int MaximumAnchorCells=65536,int MaximumIntentCandidates=1024,int MaximumIntentStates=100000,
    IReadOnlyList<IReadOnlyList<int>>? ExcludedAssignments=null,IReadOnlyList<WorldRegionIntentRelation>? IntentRelations=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] WorldRegionRepresentation Representation=WorldRegionRepresentation.TerrainFaces,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WorldRegionIntentPartition? IntentPartition=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? RequiredAssignment=null);
public sealed record WorldRegionEdge(WorldPoint From, WorldPoint To);
public sealed record WorldRegion(string Id, WorldRegionSeed Seed, IReadOnlyList<WorldPolygon> Cells,
    IReadOnlyList<WorldRegionEdge> BoundaryEdges, double Area);
public sealed record WorldRegionPlan(WorldRegionRequest Request, IReadOnlyList<WorldRegion> Regions,
    double BoundaryArea, double SuitableArea, double UnclaimedSuitableArea, double IneligibleArea, long Work,WorldRegionIntentPlan? IntentPlan=null);

/// <summary>Connected suitable regions selected by protected seeds or terrain intents. Convex pieces are computational geometry,
/// not streets or parcels. Point contact never connects regions; no required seed is discarded.</summary>
public sealed partial class WorldRegionPlanner
{
    private sealed record ProfileGroup(WorldSuitabilityProfile Profile,List<WorldRegionSeed> Seeds,List<WorldRegionIntent> Intents,List<WorldPolygon> Cells);
    public static WorldSuitabilityProfile ProfileFor(WorldRegionPlan plan,string regionId)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var region=plan.Regions.SingleOrDefault(r=>r.Id==regionId)??throw new ArgumentException("WORLD_REGION_PROFILE_REQUIRED:"+regionId);
        return region.Seed.Profile??plan.Request.Profile;
    }

    public WorldRegionPlan Plan(WorldEnvironmentQuery environment, WorldRegionRequest request, CancellationToken token=default)
        =>Plan(environment,request,null,token);

    internal WorldRegionPlan Plan(WorldEnvironmentQuery environment,WorldRegionRequest request,WorldSpatialWorkBudget? parent,CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(environment);token.ThrowIfCancellationRequested();
        if(request is null||!WorldEnvironmentQuery.Id(request.Id)||!WorldEnvironmentQuery.Text(request.Purpose)||
            !WorldEnvironmentQuery.Text(request.SourceRuleId)||request.Seeds is null||request.Seeds.Count>32||request.Intents?.Count>32||
            request.Seeds.Count+(request.Intents?.Count??0) is <1 or >32||!double.IsFinite(request.AnchorPrecision)||request.AnchorPrecision is <.000001 or >100000||
            request.MaximumAnchorCells is <1 or >1000000||request.MaximumIntentCandidates is <1 or >4096||request.MaximumIntentStates is <1 or >1000000||
            request.MaximumCells is <1 or >4096||request.MaximumWork is <1 or >200000000||!Enum.IsDefined(request.Representation))
            throw new ArgumentException("WORLD_REGION_REQUEST_INVALID");
        foreach(var intent in request.Intents??[])ValidateIntent(intent);
        if(request.IntentPartition is null&&(request.Intents??[]).Any(i=>i.AllocationWeight is not null))
            throw new ArgumentException("WORLD_REGION_INTENT_WEIGHT_REQUIRES_PARTITION");
        if(request.RequiredAssignment is {} required&&(request.Intents is not {Count:>0}||required.Count!=request.Intents.Count||required.Any(c=>c is <0 or >4095)))
            throw new ArgumentException("WORLD_REGION_INTENT_ASSIGNMENT_INVALID");
        ValidateIntentRelations(request,request.Seeds.Where(s=>s is not null).Select(s=>s.Id).Concat((request.Intents??[]).Select(i=>i.Id)).ToHashSet(StringComparer.Ordinal));
        if(request.ExcludedAssignments is {} excluded&&(request.Intents is not {Count:>0}||excluded.Count>64||
            excluded.Any(v=>v is null||v.Count!=request.Intents.Count||v.Any(c=>c is <0 or >4095))||
            excluded.Select(v=>string.Join(",",v)).Distinct(StringComparer.Ordinal).Count()!=excluded.Count))throw new ArgumentException("WORLD_REGION_INTENT_EXCLUSIONS_INVALID");
        request=JsonSerializer.Deserialize<WorldRegionRequest>(JsonSerializer.Serialize(request,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        var boundary=request.Boundary;
        if(boundary?.Vertices is null||boundary.Vertices.Count is <3 or >64||boundary.Vertices.Any(p=>!double.IsFinite(p.X)||!double.IsFinite(p.Y)||Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))
            throw new ArgumentException("WORLD_REGION_BOUNDARY_INVALID");
        boundary.Validate();
        var pointQuery=new WorldPointSuitability(environment,request.Profile);
        request=request with{Profile=pointQuery.Profile,Seeds=Array.AsReadOnly(request.Seeds.OrderBy(s=>s?.Id,StringComparer.Ordinal).ToArray()),
            Intents=request.Intents?.OrderBy(i=>i?.Id,StringComparer.Ordinal).ToArray()};
        var ids=new HashSet<string>(StringComparer.Ordinal);
        var normalized=new List<WorldRegionSeed>();
        foreach(var seed in request.Seeds)
        {
            var query=seed?.Profile is {} profile?new WorldPointSuitability(environment,profile):pointQuery;
            if(seed is null||!WorldEnvironmentQuery.Id(seed.Id)||!ids.Add(seed.Id)||!WorldEnvironmentQuery.Text(seed.Purpose)||
                !WorldEnvironmentQuery.Text(seed.SourceRuleId)||!double.IsFinite(seed.MinimumArea)||seed.MinimumArea<=0||seed.MinimumArea>4e14||
                !boundary.ContainsPoint(seed.Position)||!query.Evaluate(seed.Position,token).Eligible)
                throw new ArgumentException("WORLD_REGION_SEED_INVALID");
            normalized.Add(seed.Profile is null?seed:seed with{Profile=query.Profile});
        }
        var intents=new List<WorldRegionIntent>();
        foreach(var intent in request.Intents??[])
        {
            ValidateIntent(intent);
            if(intent.PreferSuitableAnchor&&(intent.RequiredPosition is not null||
                (intent.Profile??request.Profile).Preferences is not {Count:>0}))
                throw new ArgumentException("WORLD_REGION_PREFERRED_ANCHOR_INVALID");
            if(!ids.Add(intent.Id))throw new ArgumentException("WORLD_REGION_INTENT_INVALID");
            intents.Add(intent.Profile is null?intent:intent with{Profile=new WorldPointSuitability(environment,intent.Profile).Profile});
        }
        request=request with{Seeds=normalized.AsReadOnly(),Intents=request.Intents is null?null:intents.AsReadOnly()};
        if(request.IntentRelations is not null)request=request with{IntentRelations=request.IntentRelations.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray()};
        var map=new SortedDictionary<string,ProfileGroup>(StringComparer.Ordinal);
        ProfileGroup Group(WorldSuitabilityProfile profile)
        {
            // Shared partitions require the same hard suitability domain, but
            // their intents may score that domain with different soft preferences.
            var keyProfile=request.IntentPartition is not null&&(request.Intents??[]).Any(i=>i.PreferSuitableAnchor)?
                profile with{Preferences=[]}:profile;
            string key=JsonSerializer.Serialize(keyProfile,WorldJson.CreateOptions());
            if(!map.TryGetValue(key,out var group)){group=new(profile,[],[],[]);map.Add(key,group);}return group;
        }
        foreach(var seed in request.Seeds)Group(seed.Profile??request.Profile).Seeds.Add(seed);
        foreach(var intent in request.Intents??[])Group(intent.Profile??request.Profile).Intents.Add(intent);
        var groups=map.Values.ToArray();
        if(request.IntentPartition is not null)
        {
            if(request.IntentPartition.MaximumAssignments is <1 or >4096)throw new ArgumentException("WORLD_REGION_PARTITION_POLICY_INVALID");
            if(request.Intents is not {Count:>0})throw new ArgumentException("WORLD_REGION_PARTITION_INTENTS_REQUIRED");
            if(groups.Length!=1)throw new ArgumentException("WORLD_REGION_PARTITION_SINGLE_PROFILE_REQUIRED");
            WorldPopulationGeometry.ValidateAreaPattern(environment,request.IntentPartition.Pattern,request.Intents.Count);
        }
        if(groups.Length>16)throw new ArgumentException("WORLD_REGION_PROFILE_BUDGET");
        // Environment pieces are already split and clamped at each declared field
        // bound. Recutting that same boundary can manufacture slivers when an
        // intersection evaluates a few ulps to the other side. Bounds are semantic
        // facts, so only remove cuts at or outside them; interior cuts remain exact.
        var fields=environment.OrderedFields.ToDictionary(f=>f.Id,StringComparer.Ordinal);
        var planes=groups.SelectMany(g=>g.Profile.Requirements.SelectMany(r=>new[]{(r.FieldId,Threshold:r.Minimum),(r.FieldId,Threshold:r.Maximum)}))
            .Distinct().Where(p=>{var field=fields[p.FieldId];return !(field.Minimum is {} min&&p.Threshold<=min||field.Maximum is {} max&&p.Threshold>=max);})
            .OrderBy(p=>p.FieldId,StringComparer.Ordinal).ThenBy(p=>p.Threshold).ToArray();
        var budget=new WorldSpatialWorkBudget(request.MaximumWork,token,parent);
        var cells=new List<WorldPolygon>();
        var faces=new WorldRegionFaces(request.MaximumCells,budget,request.Representation);
        new WorldSurfaceAreaQuery(environment.Surface).VisitRegions(boundary,(sample,vertices)=>
        {
            if(vertices.Count<3||Area(vertices)==0||sample.Status!=WorldSurfaceStatus.Sampled||
                !groups.Any(g=>g.Profile.AllowedKinds.Contains(sample.Kind!,StringComparer.Ordinal)))return true;
            foreach(var piece in WorldEnvironmentRegions.Evaluate(environment,sample,vertices,4096,budget))
            {
                if(Area(piece.Vertices)==0)continue;
                // Environmental clamp pieces can repeat a seam endpoint even when no
                // suitability threshold is selected. Apply the common topology cleanup
                // before retaining a region cell; do not depend on a later clip to do it.
                var atoms=new List<IReadOnlyList<WorldPoint>>{Clip(piece.Vertices,_=>1,budget)};
                foreach(var plane in planes)
                {
                    var next=new List<IReadOnlyList<WorldPoint>>();
                    double Signed(WorldPoint p)=>WorldEnvironmentRegions.Value(piece.Fields[plane.FieldId],sample.Position,p)-plane.Threshold;
                    foreach(var atom in atoms)
                    {
                        budget.Charge(atom.Count);var values=atom.Select(Signed).ToArray();double minimum=values.Min(),maximum=values.Max();
                        if(minimum>=0)Add(Clip(atom,Signed,budget));
                        else if(maximum<=0)Add(Clip(atom,p=>-Signed(p),budget));
                        else{Add(Clip(atom,Signed,budget));Add(Clip(atom,p=>-Signed(p),budget));}
                    }
                    void Add(IReadOnlyList<WorldPoint> polygon)
                    {
                        double area=Math.Abs(Area(polygon));if(area==0)return;
                        if(area<=1e-8){var error=new ArgumentException("WORLD_REGION_GEOMETRY_RESOLUTION");error.Data["RegionId"]=request.Id;error.Data["CellArea"]=area;error.Data["CellVertices"]=polygon.ToArray();error.Data["FieldId"]=plane.FieldId;error.Data["FieldThreshold"]=plane.Threshold;throw error;}
                        if(next.Count>=16384)throw new ArgumentException("WORLD_REGION_PARTITION_BUDGET");next.Add(polygon);
                    }
                    atoms=next;
                }
                foreach(var atom in atoms)
                {
                    double area=Area(atom);if(area==0)continue;
                    if(Math.Abs(area)<=1e-8)throw new ArgumentException("WORLD_REGION_GEOMETRY_RESOLUTION");
                    var polygon=new WorldPolygon(Array.AsReadOnly((area<0?atom.Reverse():atom).ToArray()));polygon.Validate();var center=polygon.Centroid();uint eligible=0;
                    for(int groupIndex=0;groupIndex<groups.Length;groupIndex++)
                    {
                        var group=groups[groupIndex];budget.Charge(group.Profile.Requirements.Count+1);
                        if(!group.Profile.AllowedKinds.Contains(sample.Kind!,StringComparer.Ordinal)||group.Profile.Requirements.Any(r=>{
                            double value=WorldEnvironmentRegions.Value(piece.Fields[r.FieldId],sample.Position,center);return value<r.Minimum||value>r.Maximum;}))continue;
                        eligible|=1u<<groupIndex;
                    }
                    if(eligible!=0)faces.Add(sample,piece,polygon,eligible);
                }
            }
            return true;
        },budget,token);
        foreach(var face in faces.Complete())
        {
            cells.Add(face.Polygon);
            for(int groupIndex=0;groupIndex<groups.Length;groupIndex++)
                if((face.Profiles&(1u<<groupIndex))!=0)groups[groupIndex].Cells.Add(face.Polygon);
        }
        var regions=new List<WorldRegion>();var owned=new HashSet<WorldPolygon>(ReferenceEqualityComparer.Instance);
        var components=groups.Select(g=>(Group:g,Components:Components(environment,g.Profile,g.Cells,budget,token))).ToArray();
        foreach(var entry in components)
        foreach(var region in FromComponents(entry.Components,entry.Group.Seeds,budget))
        {
            foreach(var cell in region.Cells){budget.Charge();if(!owned.Add(cell))throw new ArgumentException("WORLD_REGION_PROFILE_OWNERSHIP_CONFLICT:"+region.Id);}
            regions.Add(region);
        }
        var intentPlan=request.Intents is not {Count:>0}?null:request.IntentPartition is null?
            AssignIntents(environment,request,components,owned,regions,budget,token):
            new WorldRegionIntentPartitionPlanner().Allocate(environment,request,components[0].Group.Profile,components[0].Components,owned,regions,budget,token);
        double total=Math.Abs(Area(boundary.Vertices)),suitable=cells.Sum(c=>Area(c.Vertices)),selected=regions.Sum(r=>r.Area);
        if(suitable>total+Math.Max(1,total)*1e-10||selected>suitable+Math.Max(1,total)*1e-10)throw new ArgumentException("WORLD_REGION_AREA_ACCOUNTING_INVALID");
        return new(request,Array.AsReadOnly(regions.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray()),total,suitable,Math.Max(0,suitable-selected),Math.Max(0,total-suitable),budget.Used,intentPlan);
    }

}
