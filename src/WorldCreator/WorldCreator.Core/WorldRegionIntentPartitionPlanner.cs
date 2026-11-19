using WorldCreator.Core.Geometry;
using static WorldCreator.Core.Spatial.WorldRegionGeometry;

namespace WorldCreator.Core.Spatial;

public sealed record WorldRegionIntentPartition(WorldAreaPopulationPattern Pattern,int MaximumAssignments=64);
public sealed record WorldRegionIntentPartitionResult(int Component,IReadOnlyList<string> IntentIds,WorldAreaPopulationPlan AreaPlan);
public sealed record WorldRegionIntentPartitionAttempt(int Ordinal,IReadOnlyList<int> Components,bool Accepted,string? Rejection,
    IReadOnlyList<WorldRegionIntentPartitionResult> Partitions,IReadOnlyList<WorldAreaPartitionAttempt>? RejectedAreaAttempts);

/// <summary>Assigns required intents to suitable components, then partitions each
/// occupied component through the shared connected area kernel. No street/grid or
/// object placement is inferred here.</summary>
public sealed class WorldRegionIntentPartitionPlanner
{
    private sealed record ComponentSolution(List<WorldRegionIntentPartitionResult> Parts,List<WorldRegion> Regions,IReadOnlyList<WorldRegionIntentRelationResult>? Relations);
    internal WorldRegionIntentPlan Allocate(WorldEnvironmentQuery environment,WorldRegionRequest request,WorldSuitabilityProfile profile,
        IReadOnlyList<Component> components,HashSet<WorldPolygon> protectedCells,List<WorldRegion> regions,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var policy=request.IntentPartition!;
        if(policy.MaximumAssignments is <1 or >4096)throw new ArgumentException("WORLD_REGION_PARTITION_POLICY_INVALID");
        var pattern=policy.Pattern with{Interior=policy.Pattern.Interior??new(request.AnchorPrecision,request.MaximumAnchorCells)};
        var intents=request.Intents!;var witnesses=new List<WorldRegionIntentCandidate>();var options=new List<int>[intents.Count];
        for(int i=0;i<options.Length;i++)options[i]=[];
        int evaluations=0,states=0;var anchors=new Dictionary<int,WorldRegionInteriorAnchor>();
        var candidateAnchors=new Dictionary<(int Intent,int Component),WorldRegionInteriorAnchor>();
        for(int component=0;component<components.Count;component++)
        {
            var shape=components[component];WorldRegionInteriorAnchor? anchor=null;
            var fixedAnchors=new Dictionary<WorldPoint,WorldRegionInteriorAnchor>();
            for(int i=0;i<intents.Count;i++)
            {
                token.ThrowIfCancellationRequested();budget.Charge(shape.Cells.Count+1);
                if(witnesses.Count>=request.MaximumIntentCandidates)throw new ArgumentException("WORLD_REGION_INTENT_CANDIDATE_BUDGET");
                var intent=intents[i];WorldRegionInteriorAnchor? actual=null;
                string? reason=shape.Area<intent.MinimumArea?"minimum-area":protectedCells.Overlaps(shape.Cells)?"protected-ownership":null;
                if(reason is null)
                {
                    if(intent.RequiredPosition is {} position)
                    {
                        if(!fixedAnchors.TryGetValue(position,out actual))
                        {
                            if(evaluations>=request.MaximumAnchorCells)throw new ArgumentException("WORLD_REGION_ANCHOR_CELL_BUDGET");
                            var seed=new WorldRegionSeed("partition-component","Fixed component anchor",request.SourceRuleId,position,1e-8,profile);
                            actual=WorldRegionPlanner.AnchorAt(new(seed.Id,seed,shape.Cells,shape.BoundaryEdges,shape.Area),position,budget);
                            evaluations++;fixedAnchors.Add(position,actual);
                        }
                        if(actual.Clearance<=0)reason="required-position-outside";
                    }
                    else
                    {
                        if(anchor is null)
                        {
                            if(evaluations>=request.MaximumAnchorCells)throw new ArgumentException("WORLD_REGION_ANCHOR_CELL_BUDGET");
                            var seed=new WorldRegionSeed("partition-component","Suitable component interior",request.SourceRuleId,shape.Cells[0].Centroid(),1e-8,profile);
                            anchor=WorldRegionPlanner.FindInterior(new(seed.Id,seed,shape.Cells,shape.BoundaryEdges,shape.Area),
                                new(request.AnchorPrecision,request.MaximumAnchorCells-evaluations),budget);
                            evaluations+=anchor.Evaluations;
                        }
                        actual=anchor;
                    }
                    if(reason is null&&actual.Clearance<intent.MinimumClearance)
                    {
                        if(actual.UpperBound>=intent.MinimumClearance)throw new ArgumentException("WORLD_REGION_INTENT_CLEARANCE_PRECISION:"+intent.Id);
                        reason="minimum-clearance";
                    }
                    if(reason is null){options[i].Add(component);candidateAnchors.Add((i,component),actual);anchors.TryAdd(component,actual);}
                }
                witnesses.Add(new(intent.Id,component,shape.Area,actual,reason is null,reason));
            }
        }
        var attempts=new List<WorldRegionIntentPartitionAttempt>();var selected=new int[intents.Count];Array.Fill(selected,-1);
        var usedArea=new double[components.Count];List<WorldRegion>? accepted=null;List<WorldRegionIntentPartitionResult>? acceptedPlans=null;
        IReadOnlyList<WorldRegionIntentRelationResult>? acceptedRelations=null;
        bool Complete()
        {
            foreach(var excluded in request.ExcludedAssignments??[]){budget.Charge(intents.Count+1);if(excluded.SequenceEqual(selected))return false;}
            if(attempts.Count>=policy.MaximumAssignments)throw new ArgumentException("WORLD_REGION_PARTITION_ASSIGNMENT_BUDGET");
            var parts=new List<WorldRegionIntentPartitionResult>();var derived=new List<WorldRegion>();
            string? rejection=null;IReadOnlyList<WorldAreaPartitionAttempt>? rejectedAreas=null;
            try
            {
                var occupied=selected.Distinct().Order().ToArray();
                bool feedback=request.IntentRelations is {Count:>0};
                (WorldRegionEdge? Span,IReadOnlyList<WorldRegionEdge>? Segments) QualifiedSpan(WorldRegionIntentRelation relation,WorldRegion from,WorldRegion to)
                {
                    if(relation.MinimumSharedSpan is not {} minimum)return(null,null);
                    var segments=WorldLocalLayoutPlanner.SharedBoundary(from,to,budget);
                    var span=segments.OrderByDescending(e=>e.From.DistanceTo(e.To)).ThenBy(e=>e.From.X).ThenBy(e=>e.From.Y).FirstOrDefault();
                    if(span is null||span.From.DistanceTo(span.To)<minimum)
                        throw new WorldAreaQualificationException("region-relation:"+relation.Id,new ArgumentException("WORLD_REGION_PARTITION_RELATION:"+relation.Id));
                    return(span,segments);
                }
                void CheckCells(List<WorldRegion> current)
                {
                    if(regions.Sum(r=>(long)r.Cells.Count)+current.Sum(r=>(long)r.Cells.Count)>request.MaximumCells)
                        throw new ArgumentException("WORLD_REGION_PARTITION_CELL_BUDGET");
                }
                ComponentSolution Partition(int ordinal,List<WorldRegion> prefix)
                {
                    token.ThrowIfCancellationRequested();
                    if(ordinal==occupied.Length)
                    {
                        var shapes=regions.Concat(prefix).ToDictionary(r=>r.Id,StringComparer.Ordinal);
                        var positions=shapes.ToDictionary(p=>p.Key,p=>p.Value.Seed.Position,StringComparer.Ordinal);
                        var relations=new List<WorldRegionIntentRelationResult>();
                        foreach(var relation in request.IntentRelations??[])
                        {
                            budget.Charge();var from=positions[relation.FromId];var to=positions[relation.ToId];double distance=from.DistanceTo(to);
                            if(distance<relation.MinimumDistance||distance>relation.MaximumDistance)
                                throw new WorldAreaQualificationException("region-relation:"+relation.Id,new ArgumentException("WORLD_REGION_PARTITION_RELATION:"+relation.Id));
                            var boundary=QualifiedSpan(relation,shapes[relation.FromId],shapes[relation.ToId]);
                            relations.Add(new(relation,from,to,distance,boundary.Span,boundary.Segments));
                        }
                        return new([],prefix,request.IntentRelations is null?null:relations);
                    }
                    int component=occupied[ordinal];
                    var indices=Enumerable.Range(0,intents.Count).Where(i=>selected[i]==component).ToArray();
                    var shape=components[component];
                    string id="component-"+component.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var seed=new WorldRegionSeed(id,request.Purpose,request.SourceRuleId,anchors[component].Position,1e-8,profile);
                    var staged=new List<WorldPopulationStratum>();ComponentSolution? continuation=null;
                    List<WorldRegion> Append(IReadOnlyList<WorldPopulationStratum> strata)
                    {
                        var result=new List<WorldRegion>(prefix);
                        for(int n=0;n<strata.Count;n++)
                        {
                            var intent=intents[indices[n]];var stratum=strata[n];
                            var ownSeed=new WorldRegionSeed(intent.Id,intent.Purpose,intent.SourceRuleId,stratum.Position,intent.MinimumArea,intent.Profile??request.Profile);
                            result.Add(new(intent.Id,ownSeed,stratum.Cells,stratum.BoundaryEdges,stratum.Area));
                        }
                        return result;
                    }
                    void Qualify(int index,WorldPopulationStratum stratum)
                    {
                        // A retry owns only this prefix. Never let later strata or
                        // another component's rejected solution constrain a new cut.
                        if(staged.Count>index)staged.RemoveRange(index,staged.Count-index);
                        staged.Add(stratum);continuation=null;
                        var current=Append(staged);CheckCells(current);
                        var shapes=regions.Concat(current).ToDictionary(r=>r.Id,StringComparer.Ordinal);
                        var positions=shapes.ToDictionary(p=>p.Key,p=>p.Value.Seed.Position,StringComparer.Ordinal);
                        string added=intents[indices[index]].Id;
                        budget.Charge(positions.Count);
                        foreach(var relation in request.IntentRelations!)
                        {
                            budget.Charge();
                            if(relation.FromId!=added&&relation.ToId!=added||!positions.TryGetValue(relation.FromId,out var from)||!positions.TryGetValue(relation.ToId,out var to))continue;
                            double distance=from.DistanceTo(to);
                            if(distance<relation.MinimumDistance||distance>relation.MaximumDistance)
                                throw new WorldAreaQualificationException("region-relation:"+relation.Id,new ArgumentException("WORLD_REGION_PARTITION_RELATION:"+relation.Id));
                            QualifiedSpan(relation,shapes[relation.FromId],shapes[relation.ToId]);
                        }
                        if(index==indices.Length-1)
                        {
                            try{continuation=Partition(ordinal+1,current);}
                            catch(ArgumentException error)when(WorldPopulationGeometry.AreaSemanticFailure(error))
                            {
                                // A later component's geometric/relationship failure
                                // can reconsider this component's declared cuts. Hard
                                // work/attempt/cell limits and cancellation propagate.
                                throw new WorldAreaQualificationException("region-component:"+component,error);
                            }
                        }
                    }
                    var plan=WorldPopulationGeometry.AreaVerifiedRegion(new(id,seed,shape.Cells,shape.BoundaryEdges,shape.Area),environment,profile,pattern,indices.Length,budget,token,
                            relativeWeights:indices.Select(i=>intents[i].AllocationWeight??intents[i].MinimumArea).ToArray(),
                        requirements:indices.Select(i=>new WorldAreaStratumRequirement(intents[i].Id,intents[i].MinimumArea,intents[i].MinimumClearance,intents[i].RequiredPosition,
                            intents[i].PreferSuitableAnchor?intents[i].Profile??request.Profile:null)).ToArray(),
                        qualify:feedback?Qualify:null,resweepTerrain:request.Representation==WorldRegionRepresentation.Membership,
                        verifiedMembership:request.Representation==WorldRegionRepresentation.Membership);
                    if(!feedback){var current=Append(plan.Strata);CheckCells(current);continuation=Partition(ordinal+1,current);}
                    var result=continuation!;result.Parts.Insert(0,new(component,indices.Select(i=>intents[i].Id).ToArray(),plan));return result;
                }
                var solution=Partition(0,[]);parts=solution.Parts;derived=solution.Regions;
                accepted=derived;acceptedPlans=parts;acceptedRelations=solution.Relations;
            }
            catch(ArgumentException error)when(WorldPopulationGeometry.AreaSemanticFailure(error))
            {
                rejection=error.Message;rejectedAreas=error.Data["AreaPartitionAttempts"] as IReadOnlyList<WorldAreaPartitionAttempt>;
                for(Exception? cause=error;cause is not null;cause=cause.InnerException)
                    if(cause.Message.StartsWith("WORLD_REGION_PARTITION_RELATION:",StringComparison.Ordinal)){rejection=cause.Message;break;}
            }
            attempts.Add(new(attempts.Count+1,selected.ToArray(),rejection is null,rejection,parts.ToArray(),rejectedAreas));
            return rejection is null;
        }
        bool Search(int position)
        {
            token.ThrowIfCancellationRequested();budget.Charge();if(++states>request.MaximumIntentStates)throw new ArgumentException("WORLD_REGION_INTENT_STATE_BUDGET");
            if(position==intents.Count)return Complete();
            foreach(int component in options[position].OrderByDescending(c=>candidateAnchors[(position,c)].Clearance).ThenByDescending(c=>components[c].Area).ThenBy(c=>candidateAnchors[(position,c)].Position.X).ThenBy(c=>candidateAnchors[(position,c)].Position.Y).ThenBy(c=>c))
            {
                if(request.RequiredAssignment is {} required&&required[position]!=component)continue;
                budget.Charge();double prior=usedArea[component],next=prior+intents[position].MinimumArea;
                if(next>components[component].Area)continue;
                usedArea[component]=next;selected[position]=component;
                if(Search(position+1))return true;
                usedArea[component]=prior;selected[position]=-1;
            }
            return false;
        }
        if(!Search(0))
        {
            var failure=new WorldRegionIntentUnsatisfiedException();failure.Data["RegionPartitionAttempts"]=attempts.ToArray();
            failure.Data["RegionIntentCandidates"]=witnesses.ToArray();throw failure;
        }
        foreach(var region in accepted!)regions.Add(region);
        foreach(int component in selected.Distinct())foreach(var cell in components[component].Cells)protectedCells.Add(cell);
        var selections=intents.Select((intent,i)=>new WorldRegionIntentSelection(intent.Id,selected[i],accepted!.Single(r=>r.Id==intent.Id).Seed)).ToArray();
        return new(witnesses.OrderBy(w=>w.IntentId,StringComparer.Ordinal).ThenBy(w=>w.Component).ToArray(),selections,evaluations,states,acceptedRelations,
            acceptedPlans!.ToArray(),attempts.ToArray());
    }
}
