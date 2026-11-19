using System.Collections.ObjectModel;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

public sealed record WorldSiteDemand(string Id,string Purpose,string SourceRuleId,WorldPolygon? Footprint,WorldSuitabilityProfile Profile,WorldPolygon? Reservation=null,string? RegionId=null,string? AllocationId=null);
public sealed record WorldPlacementAnchor(string Id,WorldPoint Position);
public sealed record WorldPlacementRelation(string Id,string FromId,string ToId,string Purpose,string SourceRuleId,
    double Minimum,double Maximum,double Ideal,double Weight);
public enum WorldPlacementExecution { Search, Prescribed }
public sealed record WorldPlacementPose(string DemandId,WorldPoint Position);
public sealed record WorldPlacementNogood(string Id,string Purpose,string SourceRuleId,IReadOnlyList<WorldPlacementPose> Assignments,string? ConsumerRecipeSha256=null);
public sealed record WorldPlacementSeparationWitness(string FirstDemandId,string SecondDemandId,WorldPolygon FirstEnvelope,WorldPolygon SecondEnvelope,double MinimumSeparation);
public sealed record WorldPlacementRequest(string Id,string Purpose,string SourceRuleId,WorldPolygon? Boundary,
    IReadOnlyList<WorldSiteDemand> Demands,IReadOnlyList<WorldPlacementAnchor> Anchors,IReadOnlyList<WorldPlacementRelation> Relations,
    double MinimumSeparation=0,int MaximumCandidates=2048,int MaximumSearchStates=100000,long MaximumWork=20000000,bool AllowBoundaryContact=false,
    WorldPlacementGrowth? Growth=null,WorldPlacementExecution Execution=WorldPlacementExecution.Search,IReadOnlyList<WorldPlacementNogood>? Nogoods=null);
public sealed record WorldSitePlacement(string DemandId,WorldPoint Position,string CandidateSource,
    WorldPointSuitabilityResult Point,WorldFootprintSuitabilityResult Footprint,WorldFootprintSuitabilityResult? Reservation=null);
public sealed record WorldPlacementDiagnostic(string DemandId,int Candidates,int Eligible,IReadOnlyDictionary<string,int> Rejections);
public enum WorldPlacementStatus { Found,NoFeasibleAssignment }
public sealed record WorldPlacementPlan(WorldPlacementRequest Request,WorldPlacementStatus Status,IReadOnlyList<WorldSitePlacement> Sites,
    IReadOnlyList<WorldPlacementDiagnostic> Diagnostics,long SearchStates,long Work,IReadOnlyDictionary<string,long> Conflicts,
    WorldPlacementSeparationWitness? SeparationWitness=null);
internal sealed record WorldPlacementConstraintRejection(string Reason,IReadOnlyList<string>? Dependencies=null);

/// <summary>Terrain-derived finite candidate search. No random scattering, count reduction or rule relaxation.</summary>
public sealed partial class WorldSitePlacementPlanner
{
    internal static (int Demands,int Anchors,int Relations) Limits(WorldPlacementExecution execution,bool connectedGrowth=false)=>execution switch
    {
        // Graph contacts generate candidates on demand. They do not construct
        // the unrestricted demand-by-terrain candidate product.
        WorldPlacementExecution.Search when connectedGrowth=>(128,256,1024),
        WorldPlacementExecution.Search=>(16,32,128),
        WorldPlacementExecution.Prescribed=>(1024,2048,8192),
        _=>throw new ArgumentException("WORLD_PLACEMENT_EXECUTION_INVALID")
    };
    public WorldPlacementPlan Plan(WorldEnvironmentQuery environment,WorldPlacementRequest request,CancellationToken token=default,WorldRegionPlan? regions=null,WorldLocalLayoutPlan? localLayouts=null)
        =>Plan(environment,request,null,null,token,regions,localLayouts);

    internal WorldPlacementPlan Plan(WorldEnvironmentQuery environment,WorldPlacementRequest request,
        WorldSpatialWorkBudget? sharedBudget,Func<IReadOnlyList<WorldSitePlacement>,bool>? assignmentConstraint,CancellationToken token,WorldRegionPlan? regions=null,WorldLocalLayoutPlan? localLayouts=null,Func<WorldSitePlacement,string?>? candidateConstraint=null)
    {
        foreach(var plan in Enumerate(environment,request,sharedBudget,token,regions,localLayouts,candidateConstraint))
            if(plan.Status!=WorldPlacementStatus.Found||assignmentConstraint is null||assignmentConstraint(plan.Sites))return plan;
        throw new InvalidOperationException("WORLD_PLACEMENT_TERMINAL_RESULT_REQUIRED");
    }

    internal IEnumerable<WorldPlacementPlan> Enumerate(WorldEnvironmentQuery environment,WorldPlacementRequest request,
        WorldSpatialWorkBudget? sharedBudget,CancellationToken token,WorldRegionPlan? regions=null,WorldLocalLayoutPlan? localLayouts=null,
        Func<WorldSitePlacement,string?>? candidateConstraint=null,
        Func<WorldSitePlacement,IReadOnlyDictionary<string,WorldSitePlacement>,WorldPlacementConstraintRejection?>? partialAssignmentConstraint=null,
        WorldPlacementEvaluationMemo? evaluationMemo=null,string? memoWorldId=null,string? memoStageId=null)
    {
        ArgumentNullException.ThrowIfNull(environment);token.ThrowIfCancellationRequested();
        bool Text(string? x)=>WorldEnvironmentQuery.Text(x);bool Id(string? x)=>WorldEnvironmentQuery.Id(x);
        WorldPolygon Polygon(WorldPolygon? p)
        {
            if(p?.Vertices is null||p.Vertices.Count is <3 or >64||p.Vertices.Any(v=>!double.IsFinite(v.X)||!double.IsFinite(v.Y)||Math.Abs(v.X)>1e7||Math.Abs(v.Y)>1e7))
                throw new ArgumentException("WORLD_PLACEMENT_POLYGON_INVALID");
            var copy=new WorldPolygon(Array.AsReadOnly(p.Vertices.ToArray()));copy.Validate();return copy;
        }
        var limits=request is null?default:Limits(request.Execution,request.Growth is not null);
        if(request is null||!Id(request.Id)||!Text(request.Purpose)||!Text(request.SourceRuleId)||request.Demands is null||request.Demands.Count<1||request.Demands.Count>limits.Demands||
            request.Anchors is null||request.Anchors.Count>limits.Anchors||request.Relations is null||request.Relations.Count>limits.Relations||
            !double.IsFinite(request.MinimumSeparation)||request.MinimumSeparation<0||request.MinimumSeparation>1e7||
            request.MaximumCandidates is <1 or >8192||request.MaximumSearchStates is <1 or >1000000||request.MaximumWork is <1 or >200000000)
            throw new ArgumentException("WORLD_PLACEMENT_REQUEST_INVALID");
        bool prescribed=request.Execution==WorldPlacementExecution.Prescribed;
        if(prescribed&&request.Growth is not null)throw new ArgumentException("WORLD_PLACEMENT_PRESCRIBED_GROWTH_CONFLICT");
        var budget=new WorldSpatialWorkBudget(request.MaximumWork,token,sharedBudget);
        // A public caller cannot substitute a forged or stale generated region.
        if(regions is not null)
        {
            var expected=new WorldRegionPlanner().Plan(environment,regions.Request,budget,token);
            if(System.Text.Json.JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=System.Text.Json.JsonSerializer.Serialize(regions,WorldJson.CreateOptions()))
                throw new ArgumentException("WORLD_PLACEMENT_REGION_PLAN_MISMATCH");
        }
        if(localLayouts is not null)
        {
            if(regions is null)throw new ArgumentException("WORLD_PLACEMENT_LAYOUT_REGIONS_REQUIRED");
            var expected=new WorldLocalLayoutPlanner().Plan(environment,regions,localLayouts.Definition,budget,token);
            if(System.Text.Json.JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=System.Text.Json.JsonSerializer.Serialize(localLayouts,WorldJson.CreateOptions()))
                throw new ArgumentException("WORLD_PLACEMENT_LAYOUT_PLAN_MISMATCH");
        }
        var ids=new HashSet<string>(StringComparer.Ordinal);var demands=new List<WorldSiteDemand>();
        foreach(var d in request.Demands.OrderBy(d=>d?.Id,StringComparer.Ordinal))
        {
            if(d is null||!Id(d.Id)||!ids.Add(d.Id)||!Text(d.Purpose)||!Text(d.SourceRuleId))throw new ArgumentException("WORLD_PLACEMENT_DEMAND_INVALID");
            if(d.RegionId is not null&&(regions is null||!regions.Regions.Any(r=>r.Id==d.RegionId)))throw new ArgumentException("WORLD_PLACEMENT_REGION_REQUIRED:"+d.Id);
            if(d.AllocationId is not null&&(d.RegionId is null||localLayouts is null||!localLayouts.Allocations.Any(a=>a.Id==d.AllocationId&&a.RegionId==d.RegionId)))
                throw new ArgumentException("WORLD_PLACEMENT_ALLOCATION_REQUIRED:"+d.Id);
            var shape=Polygon(d.Footprint);
            if(!shape.ContainsPoint(new(0,0)))throw new ArgumentException("WORLD_PLACEMENT_ANCHOR_OUTSIDE_FOOTPRINT");
            var reservation=d.Reservation is {} reserved?Polygon(reserved):null;
            if(reservation is not null&&!reservation.ContainsPolygon(shape))throw new ArgumentException("WORLD_PLACEMENT_RESERVATION_CONTAINMENT");
            demands.Add(d with{Footprint=shape,Reservation=reservation,Profile=new WorldPointSuitability(environment,d.Profile).Profile});
        }
        var anchors=new SortedDictionary<string,WorldPoint>(StringComparer.Ordinal);
        foreach(var a in request.Anchors)
        {
            if(a is null||!Id(a.Id)||!ids.Add(a.Id)||!double.IsFinite(a.Position.X)||!double.IsFinite(a.Position.Y)||Math.Abs(a.Position.X)>1e7||Math.Abs(a.Position.Y)>1e7)
                throw new ArgumentException("WORLD_PLACEMENT_ANCHOR_INVALID");
            anchors.Add(a.Id,a.Position);
        }
        var demandIds=demands.Select(d=>d.Id).ToHashSet(StringComparer.Ordinal);var relationIds=new HashSet<string>(StringComparer.Ordinal);
        if(request.Nogoods is {Count:0 or >128})throw new ArgumentException("WORLD_PLACEMENT_NOGOOD_INVALID");
        var nogoodIds=new HashSet<string>(StringComparer.Ordinal);
        foreach(var nogood in request.Nogoods??[])
        {
            if(nogood is null||!Id(nogood.Id)||!nogoodIds.Add(nogood.Id)||!Text(nogood.Purpose)||!Text(nogood.SourceRuleId)||
                nogood.ConsumerRecipeSha256 is {} hash&&(hash.Length!=64||hash.Any(c=>!char.IsAsciiHexDigitLower(c)))||
                nogood.Assignments is not {Count:>0 and <=16}||nogood.Assignments.Select(a=>a?.DemandId).Distinct().Count()!=nogood.Assignments.Count||
                nogood.Assignments.Any(a=>a is null||!demandIds.Contains(a.DemandId)||!double.IsFinite(a.Position.X)||!double.IsFinite(a.Position.Y)||Math.Abs(a.Position.X)>1e7||Math.Abs(a.Position.Y)>1e7))
                throw new ArgumentException("WORLD_PLACEMENT_NOGOOD_INVALID");
        }
        bool Forbidden(WorldSitePlacement site,IReadOnlyDictionary<string,WorldPoint> known)
        {
            foreach(var nogood in request.Nogoods??[])
            {
                bool match=true;
                foreach(var assignment in nogood.Assignments)
                {
                    budget.Charge();
                    if(assignment.DemandId==site.DemandId?assignment.Position!=site.Position:
                        !known.TryGetValue(assignment.DemandId,out var position)||position!=assignment.Position){match=false;break;}
                }
                if(match)return true;
            }
            return false;
        }
        foreach(var r in request.Relations)
            if(r is null||!Id(r.Id)||!relationIds.Add(r.Id)||!demandIds.Contains(r.FromId)||!ids.Contains(r.ToId)||r.FromId==r.ToId||!Text(r.Purpose)||!Text(r.SourceRuleId)||
                !double.IsFinite(r.Minimum)||!double.IsFinite(r.Maximum)||!double.IsFinite(r.Ideal)||!double.IsFinite(r.Weight)||r.Minimum<0||r.Maximum<r.Minimum||r.Maximum>3e7||r.Ideal<r.Minimum||r.Ideal>r.Maximum||r.Weight<0||r.Weight>1e9)
                throw new ArgumentException("WORLD_PLACEMENT_RELATION_INVALID");
        foreach(var d in demands)if(!prescribed&&request.Growth is null&&d.Profile.Preferences.Count==0&&!request.Relations.Any(r=>r.Weight>0&&(r.FromId==d.Id||r.ToId==d.Id)))
            throw new ArgumentException("WORLD_PLACEMENT_PURPOSE_SCORE_REQUIRED:"+d.Id);
        var boundary=Polygon(request.Boundary??environment.Surface.Definition.Boundary);
        request=request with{Boundary=boundary,Demands=demands.AsReadOnly(),
            Anchors=Array.AsReadOnly(anchors.Select(a=>new WorldPlacementAnchor(a.Key,a.Value)).ToArray()),
            Relations=Array.AsReadOnly(request.Relations.OrderBy(r=>r.Id,StringComparer.Ordinal).ToArray()),
            Nogoods=request.Nogoods is null?null:Array.AsReadOnly(request.Nogoods.OrderBy(n=>n.Id,StringComparer.Ordinal).Select(n=>n with{
                Assignments=Array.AsReadOnly(n.Assignments.OrderBy(a=>a.DemandId,StringComparer.Ordinal).ToArray())}).ToArray())};
        var incident=demands.ToDictionary(d=>d.Id,_=>new List<WorldPlacementRelation>(),StringComparer.Ordinal);
        foreach(var relation in request.Relations)
        {
            budget.Charge();incident[relation.FromId].Add(relation);
            if(incident.TryGetValue(relation.ToId,out var target)){budget.Charge();target.Add(relation);}
        }
        var fixedAnchors=new Dictionary<string,(WorldPoint Position,string Id)>(StringComparer.Ordinal);
        if(prescribed||request.Growth is not null)
        {
            if(prescribed&&demands.Count>request.MaximumCandidates)throw new ArgumentException("WORLD_PLACEMENT_CANDIDATE_BUDGET");
            foreach(var demand in demands)
            {
                budget.Charge(incident[demand.Id].Count);
                var pins=incident[demand.Id].Where(r=>r.FromId==demand.Id&&r.Minimum==0&&r.Maximum==0&&r.Weight>0&&anchors.ContainsKey(r.ToId)).ToArray();
                if(pins.Length==0)
                {if(prescribed)throw new ArgumentException("WORLD_PLACEMENT_PRESCRIBED_ANCHOR_REQUIRED:"+demand.Id);continue;}
                var pin=pins[0];var position=anchors[pin.ToId];
                if(pins.Any(r=>anchors[r.ToId]!=position))throw new ArgumentException((prescribed?"WORLD_PLACEMENT_PRESCRIBED_ANCHOR_CONFLICT:":"WORLD_PLACEMENT_GROWTH_ANCHOR_CONFLICT:")+demand.Id);
                fixedAnchors.Add(demand.Id,(position,pin.ToId));
            }
        }
        var growth=request.Growth is {} guide?new GrowthGuide(guide,demands,anchors,fixedAnchors,boundary,budget):null;
        request=request with{Growth=growth?.Definition};
        if(growth is null&&!prescribed)budget.Charge(request.Relations.Count);
        bool hasAnchoredRelations=growth is null&&!prescribed&&request.Relations.Any(r=>r.Maximum>0&&anchors.ContainsKey(r.ToId));
        bool hasProvisionalRelations=growth is null&&!prescribed&&request.Relations.Any(r=>demandIds.Contains(r.ToId));
        bool hasRelationEdges=hasAnchoredRelations||hasProvisionalRelations;
        var anchoredConstraints=demands.ToDictionary(d=>d.Id,d=>request.Relations
            .Where(r=>r.FromId==d.Id&&anchors.ContainsKey(r.ToId)).ToArray(),StringComparer.Ordinal);
        bool allAnchored=anchoredConstraints.Values.All(r=>r.Length>0);
        bool AnchorEligible(WorldPoint point,IReadOnlyList<WorldPlacementRelation> relations)
        {
            foreach(var relation in relations)
            {
                budget.Charge();
                if(!RelationDistanceAccepts(point,anchors[relation.ToId],relation.Minimum,relation.Maximum))return false;
            }
            return true;
        }
        var candidates=new Dictionary<WorldPoint,string>();
        var featureVertices=new Dictionary<WorldPoint,string>();
        var featureEdges=new Dictionary<(WorldPoint From,WorldPoint To),string>();
        void FeatureEdge(WorldPoint a,WorldPoint b,string source)
        {
            if(!hasRelationEdges)return;
            budget.Charge();var edge=a.X<b.X||a.X==b.X&&a.Y<=b.Y?(a,b):(b,a);featureEdges.TryAdd(edge,source);
        }
        void Features(WorldPolygon polygon,string source)
        {
            foreach(var point in polygon.Vertices){budget.Charge();featureVertices.TryAdd(point,source);}
            for(int i=0;i<polygon.Vertices.Count;i++)
            {
                FeatureEdge(polygon.Vertices[i],polygon.Vertices[(i+1)%polygon.Vertices.Count],source);
            }
        }
        void Candidate(WorldPoint p,string source)
        {
            budget.Charge();
            // Hard fixed-anchor constraints cannot be repaired by another
            // assignment. Do not retain a terrain-derived candidate that no
            // demand can use. An unconstrained demand disables this pruning;
            // all retained points still receive complete footprint validation.
            if(allAnchored&&!anchoredConstraints.Values.Any(r=>AnchorEligible(p,r)))return;
            if(!candidates.TryAdd(p,source))return;
            if(candidates.Count>request.MaximumCandidates)throw new ArgumentException("WORLD_PLACEMENT_CANDIDATE_BUDGET");
        }
        if(growth is null&&!prescribed)new WorldSurfaceAreaQuery(environment.Surface).VisitRegions(boundary,(sample,vertices)=>
        {
            if(sample.Status!=WorldSurfaceStatus.Sampled)return true;
            // A convex arrangement face's vertex mean is interior. Edges and points also supply
            // exact terrain/overlay feature candidates, retaining source identity.
            Candidate(new(vertices.Average(p=>p.X),vertices.Average(p=>p.Y)),sample.SourceId+":representative");
            foreach(var p in vertices)Candidate(p,sample.SourceId+":vertex");
            return true;
        },budget,token);
        if(growth is null&&!prescribed)foreach(var anchor in anchors)Candidate(anchor.Value,"anchor:"+anchor.Key);
        if(growth is null&&!prescribed)
        {
            // Contact features are semantic boundaries, not tessellation edges.
            // Refining a heightfield must not multiply this candidate family.
            Features(boundary,"placement-boundary");
            if(environment.Surface.Definition.Boundary is {} surfaceBoundary)Features(surfaceBoundary,"surface-boundary");
            foreach(var patch in environment.Surface.Definition.Patches)Features(patch.Boundary,"terrain:"+patch.Id);
            foreach(var exclusion in environment.Surface.Exclusions)Features(exclusion.Boundary,"exclusion:"+exclusion.Id);
        }
        if(growth is null&&!prescribed&&regions is not null)
            foreach(var region in regions.Regions.Where(r=>demands.Any(d=>d.RegionId==r.Id)))
            {
                Candidate(region.Seed.Position,"region:"+region.Id+":seed");
                foreach(var cell in region.Cells)
                {
                    Candidate(new(cell.Vertices.Average(p=>p.X),cell.Vertices.Average(p=>p.Y)),"region:"+region.Id+":interior");
                    foreach(var vertex in cell.Vertices)Candidate(vertex,"region:"+region.Id+":boundary");
                }
                foreach(var edge in region.BoundaryEdges)
                {budget.Charge(2);featureVertices.TryAdd(edge.From,"region:"+region.Id+":boundary");featureVertices.TryAdd(edge.To,"region:"+region.Id+":boundary");FeatureEdge(edge.From,edge.To,"region:"+region.Id+":boundary");}
            }
        if(growth is null&&!prescribed&&localLayouts is not null)
            foreach(var allocation in localLayouts.Allocations.Where(a=>demands.Any(d=>d.AllocationId==a.Id)))
            {
                Candidate(allocation.Program.Anchor,"allocation:"+allocation.Id+":anchor");
                foreach(var cell in allocation.Shape.Cells)
                {
                    Candidate(new(cell.Vertices.Average(p=>p.X),cell.Vertices.Average(p=>p.Y)),"allocation:"+allocation.Id+":interior");
                    foreach(var vertex in cell.Vertices)Candidate(vertex,"allocation:"+allocation.Id+":boundary");
                }
                foreach(var edge in allocation.Shape.BoundaryEdges)
                {budget.Charge(2);featureVertices.TryAdd(edge.From,"allocation:"+allocation.Id+":boundary");featureVertices.TryAdd(edge.To,"allocation:"+allocation.Id+":boundary");FeatureEdge(edge.From,edge.To,"allocation:"+allocation.Id+":boundary");}
            }
        if(growth is null&&!prescribed)
        {
            // Representatives alone can miss every separated source-sized pair in a
            // suitable region. Align exact envelope vertices with actual terrain /
            // region features. These are purposeful configuration-space contacts;
            // the ordinary full-footprint, reservation and access owners still decide
            // suitability. No source resizing, jitter or vacant-cell scan is used.
            var envelopes=new List<WorldPolygon>();
            foreach(var demand in demands)
            {
                var envelope=demand.Reservation??demand.Footprint!;
                budget.Charge(envelopes.Sum(e=>(long)e.Vertices.Count)+envelope.Vertices.Count);
                if(envelopes.Any(e=>e.Vertices.SequenceEqual(envelope.Vertices)))continue;
                envelopes.Add(envelope);
                foreach(var feature in featureVertices.OrderBy(f=>f.Key.X).ThenBy(f=>f.Key.Y))
                    for(int vertex=0;vertex<envelope.Vertices.Count;vertex++)
                    {
                        var local=envelope.Vertices[vertex];
                        var point=feature.Key.Offset(-local.X,-local.Y);
                        if(Math.Abs(point.X)>1e7||Math.Abs(point.Y)>1e7){budget.Charge();continue;}
                        Candidate(point,feature.Value+":source-contact:"+demand.Id+":"+vertex);
                    }
            }
        }
        if(hasAnchoredRelations)RelationCandidates(request,anchors,featureVertices,featureEdges,budget,Candidate);
        var options=new Dictionary<string,List<WorldSitePlacement>>(StringComparer.Ordinal);var diagnostics=new List<WorldPlacementDiagnostic>();
        int retainedOptions=0,growthCandidateAttempts=0;
        long evaluationCost=1+environment.OrderedFields.Sum(f=>f.Terms.Count+1)+environment.Surface.Definition.Patches.Sum(p=>p.Boundary.Vertices.Count)+
            environment.Surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count)+(environment.Surface.Definition.Boundary?.Vertices.Count??0);
        string? evaluationEnvironment=evaluationMemo is null?null:WorldCreator.Core.Metadata.MetadataExecutionCache.Hash(new{
            environment.Definition,Surface=environment.Surface.Definition,environment.Surface.Exclusions});
        List<WorldSitePlacement> Evaluate(WorldSiteDemand demand,IReadOnlyDictionary<WorldPoint,string> inputCandidates)
        {
            var eligible=new List<WorldSitePlacement>();var rejected=new SortedDictionary<string,int>(StringComparer.Ordinal);
            void Reject(IEnumerable<string> reasons){foreach(string reason in reasons)rejected[reason]=rejected.GetValueOrDefault(reason)+1;}
            var region=demand.AllocationId is {} allocationId?localLayouts!.Allocations.Single(a=>a.Id==allocationId).Shape:
                demand.RegionId is {} regionId?regions!.Regions.Single(r=>r.Id==regionId):null;
            string? rootRegion=demand.AllocationId is {} selectedAllocation?localLayouts!.Allocations.Single(a=>a.Id==selectedAllocation).RegionId:demand.RegionId;
            var regionQuery=region is not null?new WorldFootprintSuitability(environment,WorldRegionPlanner.ProfileFor(regions!,rootRegion!)):null;
            var pointQuery=new WorldPointSuitability(environment,demand.Profile);var areaQuery=new WorldFootprintSuitability(environment,demand.Profile);
            var memo=evaluationMemo?.Open(memoWorldId!,memoStageId!,demand.Id,new{
                EnvironmentSha256=evaluationEnvironment,Boundary=boundary,Demand=demand,Region=region,RegionProfile=regionQuery?.Profile},token);
            // Fixed anchor constraints cannot be repaired by assigning another demand.
            // Reject them before whole-polygon evaluation, keeping the same feasible
            // candidates and search order without paying geometry costs for impossible sites.
            var anchoredRelations=anchoredConstraints[demand.Id];
            var ordered=growth is null?inputCandidates.OrderBy(c=>c.Key.X).ThenBy(c=>c.Key.Y).ToArray():
                inputCandidates.OrderBy(c=>c.Value,StringComparer.Ordinal).ThenBy(c=>c.Key.X).ThenBy(c=>c.Key.Y).ToArray();
            foreach(var candidate in ordered)
            {
                if(!AnchorEligible(candidate.Key,anchoredRelations)){Reject(["anchor-relationship"]);continue;}
                WorldPlacementCandidateEvaluation Qualify()
                {
                    budget.Charge(evaluationCost);var point=pointQuery.Evaluate(candidate.Key,token);
                    if(!point.Eligible)return new(null,point.Rejections);
                    var shape=new WorldPolygon(Array.AsReadOnly(demand.Footprint!.Vertices.Select(p=>p.Offset(candidate.Key.X,candidate.Key.Y)).ToArray()));
                    budget.Charge((long)boundary.Vertices.Count*shape.Vertices.Count*4);
                    if(!boundary.ContainsPolygon(shape))return new(null,["region-boundary"]);
                    if(shape.Vertices.Any(p=>Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))return new(null,["coordinate-range"]);
                    long remaining=request.MaximumWork-budget.Used;if(remaining<1)throw new ArgumentException("WORLD_PLACEMENT_WORK_BUDGET");
                    var area=areaQuery.Evaluate(shape,remaining,4096,budget,token);
                    if(!area.Eligible)return new(null,area.Rejections);
                    WorldFootprintSuitabilityResult? reservationArea=null;
                    if(demand.Reservation is {} localReservation)
                    {
                        var reservation=new WorldPolygon(Array.AsReadOnly(localReservation.Vertices.Select(p=>p.Offset(candidate.Key.X,candidate.Key.Y)).ToArray()));
                        budget.Charge((long)boundary.Vertices.Count*reservation.Vertices.Count*4);
                        if(!boundary.ContainsPolygon(reservation))return new(null,["reservation-boundary"]);
                        if(reservation.Vertices.Any(p=>Math.Abs(p.X)>1e7||Math.Abs(p.Y)>1e7))return new(null,["reservation-coordinate-range"]);
                        remaining=request.MaximumWork-budget.Used;if(remaining<1)throw new ArgumentException("WORLD_PLACEMENT_WORK_BUDGET");
                        reservationArea=areaQuery.Evaluate(reservation,remaining,4096,budget,token);
                        if(!reservationArea.Eligible)return new(null,reservationArea.Rejections.Select(r=>"reservation:"+r).ToArray());
                    }
                    if(region is not null)
                    {
                        var envelope=reservationArea?.Footprint??shape;
                        if(!WorldRegionGeometry.Contains(region,envelope,budget))return new(null,["generated-region-boundary"]);
                        remaining=request.MaximumWork-budget.Used;
                        if(remaining<1)throw new ArgumentException("WORLD_PLACEMENT_WORK_BUDGET");
                        if(!regionQuery!.Evaluate(envelope,remaining,4096,budget,token).Eligible)return new(null,["generated-region-profile"]);
                    }
                    return new(new WorldSitePlacement(demand.Id,candidate.Key,candidate.Value,point,area,reservationArea),[]);
                }
                var evaluated=memo is null?Qualify():memo.Run(candidate.Key,candidate.Value,Qualify,budget,token);
                if(evaluated.Site is not {} placed){Reject(evaluated.Rejections);continue;}
                if(Forbidden(placed,anchors)){Reject(["nogood"]);continue;}
                if(!Relations(placed,anchors)){Reject(["anchor-relationship"]);continue;}
                if(candidateConstraint?.Invoke(placed) is {} rejection){Reject([rejection]);continue;}
                if(++retainedOptions>4096)throw new ArgumentException("WORLD_PLACEMENT_OPTION_BUDGET");
                eligible.Add(placed);
            }
            diagnostics.Add(new(demand.Id,inputCandidates.Count,eligible.Count,new ReadOnlyDictionary<string,int>(rejected)));
            return eligible;
        }
        if(growth is null)foreach(var demand in demands)
            options.Add(demand.Id,Evaluate(demand,prescribed?new Dictionary<WorldPoint,string>{{fixedAnchors[demand.Id].Position,"anchor:"+fixedAnchors[demand.Id].Id}}:candidates));
        var provisional=hasProvisionalRelations?new ProvisionalRelationGuide(request,anchors,featureVertices,featureEdges,candidates,budget):null;
        var selected=new Dictionary<string,WorldSitePlacement>(StringComparer.Ordinal);long states=0;
        WorldPlacementEnvelopeIndex? prescribedIndex=null;
        var conflicts=new SortedDictionary<string,long>(StringComparer.Ordinal){{"relationship",0},{"footprint-separation",0},{"dependent-allocation",0},{"nogood",0}};
        WorldPlacementSeparationWitness? separationWitness=null;
        bool ConflictsWithSelected(WorldSitePlacement site,ISet<string>? dependencies=null)
        {
            var others=prescribedIndex is null?selected.Values.AsEnumerable():
                prescribedIndex.Query(Envelope(site),request.MinimumSeparation,selected.ContainsKey).Select(id=>selected[id]);
            foreach(var other in others)
                if(!Separated(site,other))
                {
                    dependencies?.Add(other.DemandId);
                    separationWitness??=new(site.DemandId,other.DemandId,site.Reservation?.Footprint??site.Footprint.Footprint,
                        other.Reservation?.Footprint??other.Footprint.Footprint,request.MinimumSeparation);
                    return true;
                }
            return false;
        }
        bool PartialAssignmentAccepts(WorldSitePlacement site,ISet<string>? dependencies=null)
        {
            if(partialAssignmentConstraint?.Invoke(site,selected) is not {} rejection)return true;
            if(string.IsNullOrWhiteSpace(rejection.Reason))throw new ArgumentException("WORLD_PLACEMENT_CONSTRAINT_REJECTION_INVALID");
            if(rejection.Dependencies is {} causes)
            {
                budget.Charge(causes.Count);
                if(causes.Any(id=>id is null||!selected.ContainsKey(id)))throw new ArgumentException("WORLD_PLACEMENT_CONSTRAINT_REJECTION_INVALID");
                dependencies?.UnionWith(causes);
            }
            else{budget.Charge(selected.Count);dependencies?.UnionWith(selected.Keys);}
            conflicts[rejection.Reason]=conflicts.GetValueOrDefault(rejection.Reason)+1;return false;
        }
        bool Compatible(WorldSitePlacement site,IReadOnlyDictionary<string,WorldPoint> known,ISet<string>? dependencies=null)
        {
            if(Forbidden(site,known)){dependencies?.UnionWith(selected.Keys);conflicts["nogood"]++;return false;}
            if(!Relations(site,known,dependencies)){conflicts["relationship"]++;return false;}
            if(ConflictsWithSelected(site,dependencies)){conflicts["footprint-separation"]++;return false;}
            if(!PartialAssignmentAccepts(site,dependencies))return false;
            return true;
        }
        bool Relations(WorldSitePlacement site,IReadOnlyDictionary<string,WorldPoint> known,ISet<string>? dependencies=null)
        {
            if(growth is not null&&!growth.Compatible(site,known,dependencies))return false;
            foreach(var r in incident[site.DemandId])
            {
                budget.Charge();string? other=r.FromId==site.DemandId?r.ToId:r.ToId==site.DemandId?r.FromId:null;
                if(other is null||!known.TryGetValue(other,out var p))continue;
                if(!RelationDistanceAccepts(site.Position,p,r.Minimum,r.Maximum))
                {if(demandIds.Contains(other))dependencies?.Add(other);return false;}
            }
            return true;
        }
        double Score(WorldSitePlacement site,IReadOnlyDictionary<string,WorldPoint> known)
        {
            double score=site.Point.Score!.Value;
            foreach(var r in incident[site.DemandId])
            {
                budget.Charge();string? other=r.FromId==site.DemandId?r.ToId:r.ToId==site.DemandId?r.FromId:null;
                if(other is null||!known.TryGetValue(other,out var p))continue;
                score+=r.Weight*(1-Math.Abs(site.Position.DistanceTo(p)-r.Ideal)/Math.Max(1,r.Maximum-r.Minimum));
            }
            return score;
        }
        var bounds=new Dictionary<WorldPolygon,(double MinX,double MinY,double MaxX,double MaxY)>(ReferenceEqualityComparer.Instance);
        (double MinX,double MinY,double MaxX,double MaxY) Bounds(WorldPolygon polygon)
        {
            if(bounds.TryGetValue(polygon,out var found))return found;
            budget.Charge(4L*polygon.Vertices.Count);
            return bounds[polygon]=(polygon.Vertices.Min(p=>p.X),polygon.Vertices.Min(p=>p.Y),polygon.Vertices.Max(p=>p.X),polygon.Vertices.Max(p=>p.Y));
        }
        WorldPlacementEnvelopeIndex.Box Envelope(WorldSitePlacement site)
        {
            var b=Bounds(site.Reservation?.Footprint??site.Footprint.Footprint);budget.Charge();
            // Include the anchor as well: the exact predicate checks anchor
            // distance first, including boundary-tolerant origin containment.
            return new(Math.Min(b.MinX,site.Position.X),Math.Min(b.MinY,site.Position.Y),Math.Max(b.MaxX,site.Position.X),Math.Max(b.MaxY,site.Position.Y));
        }
        bool Separated(WorldSitePlacement first,WorldSitePlacement second)
        {
            budget.Charge();
            // Each anchor lies inside its footprint, so anchor distance is an upper bound on
            // polygon separation. Reject impossible spacing before the edge-pair work.
            if(first.Position.DistanceTo(second.Position)<request.MinimumSeparation)return false;
            var a=first.Reservation?.Footprint??first.Footprint.Footprint;var b=second.Reservation?.Footprint??second.Footprint.Footprint;
            // This lower bound can accept only definitely separated bounding boxes. Near,
            // touching and overlapping boxes still reach the original exact polygon owner.
            var ba=Bounds(a);var bb=Bounds(b);budget.Charge();
            double dx=Math.Max(0,Math.Max(ba.MinX-bb.MaxX,bb.MinX-ba.MaxX)),dy=Math.Max(0,Math.Max(ba.MinY-bb.MaxY,bb.MinY-ba.MaxY));
            if(Math.Sqrt(dx*dx+dy*dy)>request.MinimumSeparation)return true;
            if(request.AllowBoundaryContact&&request.MinimumSeparation==0)
            {
                // Explicitly joined floors/sites may share boundary points or edges, never area.
                // Triangulate both shapes so the existing convex clipper also handles concavity.
                budget.Charge((long)a.Vertices.Count*a.Vertices.Count*a.Vertices.Count+(long)b.Vertices.Count*b.Vertices.Count*b.Vertices.Count);
                var firstPieces=LandscapePolygonTriangulator.Triangulate(a);var secondPieces=LandscapePolygonTriangulator.Triangulate(b);
                double overlapArea=0;
                foreach(var firstPiece in firstPieces)foreach(var secondPiece in secondPieces)
                {
                    budget.Charge(64);
                    overlapArea+=TerrainCoverageTopology.IntersectionArea(firstPiece,secondPiece);
                    if(overlapArea>1e-8)return false;
                }
                return true;
            }
            budget.Charge((long)a.Vertices.Count*b.Vertices.Count*4);
            if(a.Vertices.Any(b.ContainsPoint)||b.Vertices.Any(a.ContainsPoint))return false;
            foreach(var edge in a.Vertices.Select((p,i)=>(A:p,B:a.Vertices[(i+1)%a.Vertices.Count])))
                foreach(var other in b.Vertices.Select((p,i)=>(A:p,B:b.Vertices[(i+1)%b.Vertices.Count])))
                    if(PlanarIntersections.Intersect(edge.A,edge.B,other.A,other.B).Kind!=IntersectionKind.None)return false;
            return a.Vertices.All(p=>b.DistanceToPoint(p)>=request.MinimumSeparation)&&b.Vertices.All(p=>a.DistanceToPoint(p)>=request.MinimumSeparation);
        }
        IEnumerable<bool> Search()
        {
            if(demands.Count>1&&request.MinimumSeparation>0)
            {
                // Every accepted envelope vertex is contained by this boundary.
                // Even the maximum possible inter-polygon distance cannot exceed
                // its bounding-box diagonal (including the containment owner's tolerance).
                var b=Bounds(boundary);double dx=b.MaxX-b.MinX+2*WorldPolygon.BoundaryTolerance,dy=b.MaxY-b.MinY+2*WorldPolygon.BoundaryTolerance;
                double diagonal=Math.Sqrt(dx*dx+dy*dy);
                double error=4*(Ulp(b.MinX)+Ulp(b.MinY)+Ulp(b.MaxX)+Ulp(b.MaxY)+Ulp(diagonal)+Ulp(request.MinimumSeparation));
                if(request.MinimumSeparation>diagonal+error){conflicts["footprint-separation"]++;yield break;}
            }
            // A suspended dependent stage must not retain one CLR frame per
            // assigned demand. Keep depth-first search state on the heap instead.
            var frames=new (string Id,Dictionary<string,WorldPoint> Known,IEnumerator<WorldSitePlacement> Choices,HashSet<string> Dependencies)[demands.Count];
            int depth=0;bool entering=true;
            try
            {
                while(true)
                {
                    if(entering)
                    {
                        budget.Charge();if(++states>request.MaximumSearchStates)throw new ArgumentException("WORLD_PLACEMENT_SEARCH_BUDGET");
                        if(depth==demands.Count)
                        {
                            yield return true;
                            conflicts["dependent-allocation"]++;depth--;
                            frames[depth].Dependencies.UnionWith(selected.Keys.Where(id=>id!=frames[depth].Id));
                            selected.Remove(frames[depth].Id);
                        }
                        else
                        {
                            var known=new Dictionary<string,WorldPoint>(anchors,StringComparer.Ordinal);foreach(var s in selected.Values)known.Add(s.DemandId,s.Position);
                            var next=provisional?.Next(selected,options,known,Evaluate,(site,positions)=>Compatible(site,positions));
                            string id=growth is not null?growth.Next(selected):next?.Id??
                                demands.Where(d=>!selected.ContainsKey(d.Id)).OrderBy(d=>options[d.Id].Count).ThenBy(d=>d.Id,StringComparer.Ordinal).First().Id;
                            IReadOnlyList<WorldSitePlacement> frameOptions;
                            if(growth is not null)
                            {
                                var input=growth.Candidates(id,selected);growthCandidateAttempts+=input.Count;
                                if(growthCandidateAttempts>request.MaximumCandidates)
                                {
                                    var error=new ArgumentException("WORLD_PLACEMENT_GROWTH_CANDIDATE_BUDGET");
                                    error.Data["WorldPlacementSearch"]=new{RequestId=request.Id,DemandId=id,CandidateAttempts=growthCandidateAttempts,SearchStates=states,
                                        Selected=selected.Values.Select(s=>new WorldPlacementPose(s.DemandId,s.Position)).ToArray(),Conflicts=conflicts,
                                        Diagnostics=Snapshot(false).Diagnostics};
                                    throw error;
                                }
                                options[id]=Evaluate(demands.Single(d=>d.Id==id),input);
                            }
                            frameOptions=next?.Choices??options[id];
                            var ranked=frameOptions.Select((s,index)=>(Site:s,Score:Score(s,known),Index:index)).OrderByDescending(s=>s.Score);
                            frames[depth]=(id,known,(growth is null?ranked.ThenBy(s=>s.Site.Position.X).ThenBy(s=>s.Site.Position.Y):ranked.ThenBy(s=>s.Index))
                                .Select(s=>s.Site).GetEnumerator(),growth?.Dependencies(id,selected)??new(StringComparer.Ordinal));
                        }
                        entering=false;
                    }
                    var frame=frames[depth];
                    if(!frame.Choices.MoveNext())
                    {
                        frame.Choices.Dispose();frames[depth]=default;
                        if(depth==0)yield break;
                        int target=depth-1;
                        if(growth is not null)
                        {
                            // All candidates have a proof of rejection. Changing
                            // a prefix assignment outside their union cannot
                            // repair this domain; jump to the deepest actual cause.
                            budget.Charge(depth+frame.Dependencies.Count);
                            while(target>=0&&!frame.Dependencies.Contains(frames[target].Id))target--;
                            if(target<0){selected.Clear();yield break;}
                            if(target<depth-1)conflicts["dependency-backjump"]=conflicts.GetValueOrDefault("dependency-backjump")+1;
                            for(int skipped=depth-1;skipped>target;skipped--)
                            {
                                selected.Remove(frames[skipped].Id);frames[skipped].Choices.Dispose();frames[skipped]=default;
                            }
                            frame.Dependencies.Remove(frames[target].Id);
                            frames[target].Dependencies.UnionWith(frame.Dependencies);
                        }
                        depth=target;selected.Remove(frames[depth].Id);continue;
                    }
                    var site=frame.Choices.Current;budget.Charge();
                    if(!Compatible(site,frame.Known,frame.Dependencies))continue;
                    selected.Add(frame.Id,site);depth++;entering=true;
                }
            }
            finally{foreach(var frame in frames)frame.Choices?.Dispose();}
        }
        IEnumerable<bool> Prescribed()
        {
            budget.Charge();states=1;
            var known=new Dictionary<string,WorldPoint>(anchors,StringComparer.Ordinal);
            foreach(var demand in demands)
            {
                if(options[demand.Id].Count!=1){selected.Clear();yield break;}
                var site=options[demand.Id][0];
                if(Forbidden(site,known)){conflicts["nogood"]++;selected.Clear();yield break;}
                if(!Relations(site,known)){conflicts["relationship"]++;selected.Clear();yield break;}
                if(ConflictsWithSelected(site)){conflicts["footprint-separation"]++;selected.Clear();yield break;}
                if(!PartialAssignmentAccepts(site)){selected.Clear();yield break;}
                selected.Add(demand.Id,site);known.Add(demand.Id,site.Position);
            }
            yield return true;
            conflicts["dependent-allocation"]++;selected.Clear();
        }
        WorldPlacementPlan Snapshot(bool found)
        {
            var savedDiagnostics=diagnostics;
            if(growth is not null||provisional is not null)
                savedDiagnostics=demands.Select(d=>
                {
                    var attempts=diagnostics.Where(a=>a.DemandId==d.Id).ToArray();
                    var rejections=new SortedDictionary<string,int>(StringComparer.Ordinal);
                    foreach(var attempt in attempts)foreach(var reason in attempt.Rejections)
                        rejections[reason.Key]=rejections.GetValueOrDefault(reason.Key)+reason.Value;
                    return new WorldPlacementDiagnostic(d.Id,attempts.Sum(a=>a.Candidates),attempts.Sum(a=>a.Eligible),new ReadOnlyDictionary<string,int>(rejections));
                }).ToList();
            return new(request,found?WorldPlacementStatus.Found:WorldPlacementStatus.NoFeasibleAssignment,
                Array.AsReadOnly(selected.Values.OrderBy(s=>s.DemandId,StringComparer.Ordinal).ToArray()),Array.AsReadOnly(savedDiagnostics.ToArray()),states,budget.Used,
                new ReadOnlyDictionary<string,long>(new SortedDictionary<string,long>(conflicts,StringComparer.Ordinal)),found?null:separationWitness);
        }
        if(prescribed&&demands.All(d=>options[d.Id].Count==1))
        {
            budget.Charge(demands.Count);
            prescribedIndex=new(demands.Select(d=>(d.Id,Envelope(options[d.Id][0]))).ToArray(),budget);
        }
        foreach(bool found in prescribed?Prescribed():Search())yield return Snapshot(found);
        yield return Snapshot(false);
    }
}
