using WorldCreator.Core.Geometry;
using static WorldCreator.Core.Spatial.WorldRegionGeometry;

namespace WorldCreator.Core.Spatial;

public sealed record WorldRegionIntent(string Id,string Purpose,string SourceRuleId,double MinimumArea,double MinimumClearance=0,WorldSuitabilityProfile? Profile=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WorldPoint? RequiredPosition=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? AllocationWeight=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool PreferSuitableAnchor=false);
public sealed record WorldRegionInteriorAnchor(WorldPoint Position,double Clearance,double UpperBound,int Evaluations);
public sealed record WorldRegionInteriorSearch(double Precision=.1,int MaximumCells=65536,double MinimumClearance=0);
public sealed record WorldRegionIntentCandidate(string IntentId,int Component,double Area,WorldRegionInteriorAnchor? Anchor,bool Eligible,string? Rejection,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? AnchorPreferenceScore=null);
public sealed record WorldRegionIntentSelection(string IntentId,int Component,WorldRegionSeed Seed);
public sealed record WorldRegionIntentRelation(string Id,string FromId,string ToId,string Purpose,string SourceRuleId,double MinimumDistance,double MaximumDistance,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? MinimumSharedSpan=null);
public sealed record WorldRegionIntentRelationResult(WorldRegionIntentRelation Requirement,WorldPoint From,WorldPoint To,double Distance,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WorldRegionEdge? SharedSpan=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldRegionEdge>? SharedSegments=null);
public sealed record WorldRegionIntentPlan(IReadOnlyList<WorldRegionIntentCandidate> Candidates,IReadOnlyList<WorldRegionIntentSelection> Selections,int AnchorCells,int SearchStates,
    IReadOnlyList<WorldRegionIntentRelationResult>? Relations=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldRegionIntentPartitionResult>? Partitions=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<WorldRegionIntentPartitionAttempt>? PartitionAttempts=null);
public sealed record WorldRegionIntentFailure(IReadOnlyList<WorldRegionIntentCandidate> Candidates,int AnchorCells,int SearchStates);
public sealed class WorldRegionIntentUnsatisfiedException:ArgumentException
{
    internal WorldRegionIntentUnsatisfiedException(string? detail=null):base("WORLD_REGION_INTENT_UNSATISFIED"+(detail is null?"":":"+detail)){}
}

public sealed partial class WorldRegionPlanner
{
    private static void ValidateIntentRelations(WorldRegionRequest request,HashSet<string> regions)
    {
        if(request.IntentRelations is not {} relations)return;
        if(request.Intents is not {Count:>0}||relations.Count is <1 or >128)throw new ArgumentException("WORLD_REGION_INTENT_RELATIONS_INVALID");
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var relation in relations)
            if(relation is null||!WorldEnvironmentQuery.Id(relation.Id)||!ids.Add(relation.Id)||relation.FromId==relation.ToId||
                !regions.Contains(relation.FromId)||!regions.Contains(relation.ToId)||!WorldEnvironmentQuery.Text(relation.Purpose)||!WorldEnvironmentQuery.Text(relation.SourceRuleId)||
                !double.IsFinite(relation.MinimumDistance)||!double.IsFinite(relation.MaximumDistance)||relation.MinimumDistance<0||
                relation.MaximumDistance<relation.MinimumDistance||relation.MaximumDistance>30000000||
                relation.MinimumSharedSpan is {} span&&(!double.IsFinite(span)||span<=0||span>30000000||request.IntentPartition is null))
                throw new ArgumentException("WORLD_REGION_INTENT_RELATION_INVALID");
    }
    private static void ValidateIntent(WorldRegionIntent? intent)
    {
        if(intent is null||!WorldEnvironmentQuery.Id(intent.Id)||!WorldEnvironmentQuery.Text(intent.Purpose)||!WorldEnvironmentQuery.Text(intent.SourceRuleId)||
            !double.IsFinite(intent.MinimumArea)||intent.MinimumArea is <=0 or >4e14||!double.IsFinite(intent.MinimumClearance)||intent.MinimumClearance is <0 or >10000000||
            intent.AllocationWeight is {} weight&&(!double.IsFinite(weight)||weight<=0||weight>4e14)||
            intent.RequiredPosition is {} point&&(!double.IsFinite(point.X)||!double.IsFinite(point.Y)||Math.Abs(point.X)>1e7||Math.Abs(point.Y)>1e7))
            throw new ArgumentException("WORLD_REGION_INTENT_INVALID");
    }
    private sealed record IntentOption(WorldRegionIntent Intent,int Index,Component Shape,WorldSuitabilityProfile Profile,WorldRegionInteriorAnchor Anchor,double? PreferenceScore);
    private readonly record struct AnchorBox(WorldPoint Center,double HalfWidth,double HalfHeight,double Distance,double Upper);
    private static AnchorBox EvaluateAnchor(Component component,WorldPoint center,double hw,double hh,WorldSpatialWorkBudget budget)
    {
        budget.Charge(component.Cells.Sum(c=>(long)c.Vertices.Count)+component.BoundaryEdges.Count);
        bool inside=component.Cells.Any(c=>c.ContainsPoint(center));double distance=double.PositiveInfinity;
        foreach(var edge in component.BoundaryEdges)
        {
            double dx=edge.To.X-edge.From.X,dy=edge.To.Y-edge.From.Y;
            double t=Math.Clamp(((center.X-edge.From.X)*dx+(center.Y-edge.From.Y)*dy)/(dx*dx+dy*dy),0,1);
            distance=Math.Min(distance,center.DistanceTo(new(edge.From.X+t*dx,edge.From.Y+t*dy)));
        }
        distance=inside?distance:-distance;
        return new(center,hw,hh,distance,distance+Math.Sqrt(hw*hw+hh*hh));
    }
    internal static WorldRegionInteriorAnchor AnchorAt(WorldRegion region,WorldPoint position,WorldSpatialWorkBudget budget)
        =>AnchorAt(new Component(region.Cells,region.BoundaryEdges,region.Area),position,budget);
    internal static IReadOnlyList<(WorldRegionInteriorAnchor Anchor,double? Score)> PreferredAnchors(WorldRegion shape,WorldRegionInteriorAnchor original,
        WorldEnvironmentQuery environment,WorldSuitabilityProfile profile,double minimumClearance,string? positiveField,
        int maximumCells,ref int evaluations,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var query=new WorldPointSuitability(environment,profile);
        var candidates=new List<WorldRegionInteriorAnchor>{original};
        var positions=new HashSet<WorldPoint>{original.Position};
        foreach(var cell in shape.Cells)
        {
            token.ThrowIfCancellationRequested();budget.Charge(cell.Vertices.Count);
            var center=new WorldPoint(cell.Vertices.Average(p=>p.X),cell.Vertices.Average(p=>p.Y));
            foreach(var point in new[]{center}.Concat(cell.Vertices.Select(p=>new WorldPoint(
                center.X+(p.X-center.X)*.5,center.Y+(p.Y-center.Y)*.5))))
            {
                token.ThrowIfCancellationRequested();
                if(!positions.Add(point))continue;
                if(++evaluations>maximumCells)throw new ArgumentException("WORLD_REGION_ANCHOR_CELL_BUDGET");
                candidates.Add(AnchorAt(shape,point,budget));
            }
        }
        var qualified=new List<(WorldRegionInteriorAnchor Anchor,double Score)>();
        foreach(var candidate in candidates)
        {
            token.ThrowIfCancellationRequested();
            if(candidate.Clearance<=0||candidate.Clearance<minimumClearance)continue;
            budget.Charge(1+environment.OrderedFields.Sum(f=>f.Terms.Count+1)+environment.Surface.Definition.Patches.Sum(p=>p.Boundary.Vertices.Count)+environment.Surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count));
            var observation=query.Evaluate(candidate.Position,token);
            if(observation.Eligible&&observation.Score is {} score&&
                (positiveField is null||observation.Observation.Values.TryGetValue(positiveField,out var density)&&density>0))
                qualified.Add((candidate,score));
        }
        if(qualified.Count==0)return[(original,null)];
        return qualified.OrderByDescending(c=>c.Score).ThenByDescending(c=>c.Anchor.Clearance)
            .ThenBy(c=>c.Anchor.Position.X).ThenBy(c=>c.Anchor.Position.Y).Select(c=>(c.Anchor,(double?)c.Score)).ToArray();
    }
    private static WorldRegionInteriorAnchor AnchorAt(Component component,WorldPoint position,WorldSpatialWorkBudget budget)
    {
        var measured=EvaluateAnchor(component,position,0,0,budget);
        return new(position,measured.Distance,measured.Distance,1);
    }
    internal static void ValidateInteriorSearch(WorldRegionInteriorSearch search)
    {
        if(search is null||!double.IsFinite(search.Precision)||search.Precision is <.000001 or >100000||
            search.MaximumCells is <1 or >1000000||!double.IsFinite(search.MinimumClearance)||search.MinimumClearance is <0 or >10000000)
            throw new ArgumentException("WORLD_REGION_INTERIOR_SEARCH_INVALID");
    }
    internal static WorldRegionInteriorAnchor FindInterior(WorldRegion region,WorldRegionInteriorSearch search,WorldSpatialWorkBudget budget)
    {
        ValidateInteriorSearch(search);
        int evaluations=0;
        return FindInterior(new(region.Cells,region.BoundaryEdges,region.Area),search.Precision,search.MaximumCells,budget,ref evaluations);
    }
    private static WorldRegionInteriorAnchor FindInterior(Component component,double precision,int maximumCells,WorldSpatialWorkBudget budget,ref int evaluations)
    {
        var vertices=component.Cells.SelectMany(c=>c.Vertices).ToArray();
        double minX=vertices.Min(p=>p.X),maxX=vertices.Max(p=>p.X),minY=vertices.Min(p=>p.Y),maxY=vertices.Max(p=>p.Y);
        int before=evaluations;
        AnchorBox Evaluate(WorldPoint center,double hw,double hh)
            =>EvaluateAnchor(component,center,hw,hh,budget);
        void Guard(int count){if(count>maximumCells)throw new ArgumentException("WORLD_REGION_ANCHOR_CELL_BUDGET");}
        Guard(++evaluations);var first=Evaluate(new((minX+maxX)/2,(minY+maxY)/2),(maxX-minX)/2,(maxY-minY)/2);
        var best=first;var queue=new PriorityQueue<AnchorBox,(double Upper,double X,double Y,double W,double H)>();
        void Enqueue(AnchorBox box)=>queue.Enqueue(box,(-box.Upper,box.Center.X,box.Center.Y,box.HalfWidth,box.HalfHeight));
        Enqueue(first);
        while(queue.TryDequeue(out var box,out _))
        {
            budget.Charge();
            if(best.Distance>0&&box.Upper-best.Distance<=precision)
                return new(best.Center,best.Distance,Math.Max(best.Distance,box.Upper),evaluations-before);
            bool splitX=box.HalfWidth>=box.HalfHeight;double hw=splitX?box.HalfWidth/2:box.HalfWidth,hh=splitX?box.HalfHeight:box.HalfHeight/2;
            if(hw<=0||hh<=0)throw new ArgumentException("WORLD_REGION_ANCHOR_PRECISION");
            foreach(int sign in new[]{-1,1})
            {
                var center=box.Center.Offset(splitX?sign*hw:0,splitX?0:sign*hh);
                if(center==box.Center)throw new ArgumentException("WORLD_REGION_ANCHOR_PRECISION");
                Guard(++evaluations);var child=Evaluate(center,hw,hh);
                if(child.Distance>best.Distance)best=child;Enqueue(child);
            }
        }
        throw new ArgumentException("WORLD_REGION_ANCHOR_UNRESOLVED");
    }
    private static WorldRegionIntentPlan AssignIntents(WorldEnvironmentQuery environment,WorldRegionRequest request,
        IReadOnlyList<(ProfileGroup Group,IReadOnlyList<Component> Components)> groups,HashSet<WorldPolygon> owned,List<WorldRegion> regions,
        WorldSpatialWorkBudget budget,CancellationToken token)
    {
        int evaluations=0,index=0,states=0;var options=new Dictionary<string,List<IntentOption>>(StringComparer.Ordinal);
        var witnesses=new List<WorldRegionIntentCandidate>();
        var positions=regions.ToDictionary(r=>r.Id,r=>r.Seed.Position,StringComparer.Ordinal);
        var relations=request.IntentRelations??[];
        var incident=new Dictionary<string,List<WorldRegionIntentRelation>>(StringComparer.Ordinal);
        foreach(var relation in relations)
        {
            budget.Charge();
            foreach(string id in new[]{relation.FromId,relation.ToId})
            {if(!incident.TryGetValue(id,out var list))incident.Add(id,list=[]);list.Add(relation);}
            if(positions.TryGetValue(relation.FromId,out var from)&&positions.TryGetValue(relation.ToId,out var to)&&!Satisfies(relation,from,to))
                throw new WorldRegionIntentUnsatisfiedException();
        }
        static bool Satisfies(WorldRegionIntentRelation relation,WorldPoint from,WorldPoint to)
        {double distance=from.DistanceTo(to);return distance>=relation.MinimumDistance&&distance<=relation.MaximumDistance;}
        bool Compatible(IntentOption candidate)
        {
            foreach(var relation in incident.GetValueOrDefault(candidate.Intent.Id)??[])
            {
                budget.Charge();string other=relation.FromId==candidate.Intent.Id?relation.ToId:relation.FromId;
                if(positions.TryGetValue(other,out var point)&&!Satisfies(relation,candidate.Anchor.Position,point))return false;
            }
            return true;
        }
        foreach(var entry in groups)
        foreach(var shape in entry.Components)
        {
            int component=index++;WorldRegionInteriorAnchor? anchor=null;
            var pinned=new Dictionary<WorldPoint,WorldRegionInteriorAnchor>();
            foreach(var intent in entry.Group.Intents)
            {
                token.ThrowIfCancellationRequested();budget.Charge(shape.Cells.Count+1);
                if(witnesses.Count>=request.MaximumIntentCandidates)throw new ArgumentException("WORLD_REGION_INTENT_CANDIDATE_BUDGET");
                if(!options.TryGetValue(intent.Id,out var choices)){options.Add(intent.Id,choices=[]);}
                string? reason=shape.Area<intent.MinimumArea?"minimum-area":owned.Overlaps(shape.Cells)?"protected-ownership":null;
                WorldRegionInteriorAnchor? actual=null;double? preferenceScore=null;
                if(reason is null)
                {
                    if(intent.RequiredPosition is {} position)
                    {
                        if(!pinned.TryGetValue(position,out actual))
                        {
                            if(++evaluations>request.MaximumAnchorCells)throw new ArgumentException("WORLD_REGION_ANCHOR_CELL_BUDGET");
                            actual=AnchorAt(shape,position,budget);pinned.Add(position,actual);
                        }
                        if(actual.Clearance<=0)reason="required-position-outside";
                    }
                    else
                    {
                        actual=anchor??=FindInterior(shape,request.AnchorPrecision,request.MaximumAnchorCells,budget,ref evaluations);
                        if(intent.PreferSuitableAnchor)
                        {
                            var seed=new WorldRegionSeed(intent.Id,intent.Purpose,intent.SourceRuleId,actual.Position,intent.MinimumArea,entry.Group.Profile);
                            (actual,preferenceScore)=PreferredAnchors(new(seed.Id,seed,shape.Cells,shape.BoundaryEdges,shape.Area),actual,
                                environment,entry.Group.Profile,intent.MinimumClearance,null,request.MaximumAnchorCells,ref evaluations,budget,token)[0];
                        }
                    }
                    if(reason is null&&actual.Clearance<intent.MinimumClearance)
                    {
                        if(actual.UpperBound>=intent.MinimumClearance)throw new ArgumentException("WORLD_REGION_INTENT_CLEARANCE_PRECISION:"+intent.Id);
                        reason="minimum-clearance";
                    }
                    if(reason is null)
                    {
                        budget.Charge(1+environment.OrderedFields.Sum(f=>f.Terms.Count+1)+environment.Surface.Definition.Patches.Sum(p=>p.Boundary.Vertices.Count)+environment.Surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count));
                        if(!new WorldPointSuitability(environment,entry.Group.Profile).Evaluate(actual.Position,token).Eligible)
                            throw new ArgumentException("WORLD_REGION_ANCHOR_ELIGIBILITY:"+intent.Id);
                        choices.Add(new(intent,component,shape,entry.Group.Profile,actual,preferenceScore));
                    }
                }
                witnesses.Add(new(intent.Id,component,shape.Area,actual,reason is null,reason,preferenceScore));
            }
        }
        var intents=request.Intents!;var chosen=new List<IntentOption>();
        bool Search(int position)
        {
            token.ThrowIfCancellationRequested();budget.Charge();
            if(++states>request.MaximumIntentStates)throw new ArgumentException("WORLD_REGION_INTENT_STATE_BUDGET");
            if(position==intents.Count)
            {
                foreach(var excluded in request.ExcludedAssignments??[])
                {
                    budget.Charge(1+chosen.Count);
                    if(excluded.SequenceEqual(chosen.Select(c=>c.Index)))return false;
                }
                return true;
            }
            if(!options.TryGetValue(intents[position].Id,out var candidates))return false;
            foreach(var candidate in candidates.OrderByDescending(c=>c.PreferenceScore??0).ThenByDescending(c=>c.Anchor.Clearance).ThenByDescending(c=>c.Shape.Area).ThenBy(c=>c.Anchor.Position.X).ThenBy(c=>c.Anchor.Position.Y).ThenBy(c=>c.Index))
            {
                if(request.RequiredAssignment is {} required&&required[position]!=candidate.Index)continue;
                budget.Charge(2L*candidate.Shape.Cells.Count+1);if(owned.Overlaps(candidate.Shape.Cells)||!Compatible(candidate))continue;
                foreach(var cell in candidate.Shape.Cells)owned.Add(cell);chosen.Add(candidate);positions.Add(candidate.Intent.Id,candidate.Anchor.Position);
                if(Search(position+1))return true;
                chosen.RemoveAt(chosen.Count-1);positions.Remove(candidate.Intent.Id);foreach(var cell in candidate.Shape.Cells)owned.Remove(cell);
            }
            return false;
        }
        if(!Search(0))
        {
            var failure=new WorldRegionIntentUnsatisfiedException();
            failure.Data["RegionIntentFailure"]=new WorldRegionIntentFailure(witnesses.OrderBy(w=>w.IntentId,StringComparer.Ordinal).ThenBy(w=>w.Component).ToArray(),evaluations,states);
            throw failure;
        }
        var selections=new List<WorldRegionIntentSelection>();
        foreach(var selected in chosen)
        {
            var seed=new WorldRegionSeed(selected.Intent.Id,selected.Intent.Purpose,selected.Intent.SourceRuleId,selected.Anchor.Position,selected.Intent.MinimumArea,selected.Profile);
            regions.Add(new(seed.Id,seed,selected.Shape.Cells,selected.Shape.BoundaryEdges,selected.Shape.Area));
            selections.Add(new(seed.Id,selected.Index,seed));
        }
        var results=request.IntentRelations is null?null:relations.Select(relation=>{
            budget.Charge();var from=positions[relation.FromId];var to=positions[relation.ToId];return new WorldRegionIntentRelationResult(relation,from,to,from.DistanceTo(to));}).ToArray();
        return new(witnesses.OrderBy(w=>w.IntentId,StringComparer.Ordinal).ThenBy(w=>w.Component).ToArray(),selections.ToArray(),evaluations,states,results);
    }
}
