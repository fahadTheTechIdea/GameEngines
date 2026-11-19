using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Geometry;

public sealed record WorldAreaPopulationPattern(string? DensityFieldId=null,double AxisRotationDegrees=0,int MaximumCells=4096,int MaximumDepth=32,
    WorldRegionInteriorSearch? Interior=null,WorldAreaPartitionSearch? PartitionSearch=null);
public sealed record WorldAreaPartitionSearch(IReadOnlyList<double> AxisOffsetsDegrees,int MaximumAttempts=256,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? Seed=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] double Bend=0,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<double>? AlternateBends=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] double BendOffset=0,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<double>? AlternateBendOffsets=null);
public sealed record WorldAreaPartitionChoice(int FirstMember,int Members,double AxisOffsetDegrees,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? Bend=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? BendOffset=null);
public enum WorldAreaPartitionStatus { Rejected, Superseded, Selected }
public sealed record WorldAreaPartitionAttempt(int Ordinal,int FirstMember,int Members,int Depth,double AxisOffsetDegrees,
    double AngleDegrees,WorldPoint Origin,double Cut,double TargetMass,double LeftMass,double RightMass,
    WorldAreaPartitionStatus Status,string? Rejection,long Work,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? Bend=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? BendOffset=null);
public sealed record WorldAreaPopulationDomain(WorldRegionPlan Regions,string RegionId,WorldLocalLayoutPlan? Layouts=null,string? AllocationId=null);
public sealed record WorldPopulationStratum(string Id,WorldPoint Position,double Area,double Mass,
    IReadOnlyList<WorldPolygon> Cells,IReadOnlyList<WorldRegionEdge> BoundaryEdges,
    WorldPoint? MassCentroid=null,WorldRegionInteriorAnchor? Interior=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] double? AnchorPreferenceScore=null,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? AnchorPreferenceRank=null);
public sealed record WorldAreaPopulationPlan(int SchemaVersion,WorldAreaPopulationPattern Pattern,string RegionId,double Area,double Mass,
    IReadOnlyList<WorldPopulationStratum> Strata,long Work,IReadOnlyList<double>? RelativeWeights=null,
    IReadOnlyList<WorldAreaPartitionAttempt>? PartitionAttempts=null);
public sealed record WorldAreaPopulationMeasure(string RegionId,string? DensityFieldId,double Area,double Mass,long Work);
internal sealed record WorldAreaStratumRequirement(string Id,double MinimumArea,double MinimumClearance,WorldPoint? RequiredPosition=null,WorldSuitabilityProfile? PreferredProfile=null);
internal sealed class WorldAreaQualificationException(string id,ArgumentException failure)
    :ArgumentException("WORLD_AREA_QUALIFICATION_REJECTED:"+id,failure)
{
    internal ArgumentException Failure {get;}=failure;
}

public static partial class WorldPopulationGeometry
{
    private sealed record MassPiece(IReadOnlyList<WorldPoint> Vertices,WorldPoint Origin,WorldEnvironmentRegions.Affine Density);
    private sealed record AreaMoments(double Area,double Mass,WorldPoint Centroid,double XX,double XY,double YY);
    public static WorldAreaPopulationPlan Area(WorldAreaPopulationDomain domain,WorldEnvironmentQuery environment,
        WorldAreaPopulationPattern pattern,int count,long maximumWork=5000000,CancellationToken token=default)
    {
        if(maximumWork is <1 or >200000000)throw new ArgumentException("WORLD_AREA_WORK_INVALID");
        return Area(domain,environment,pattern,count,new WorldSpatialWorkBudget(maximumWork,token),token);
    }
    internal static WorldAreaPopulationPlan Area(WorldAreaPopulationDomain domain,WorldEnvironmentQuery environment,
        WorldAreaPopulationPattern pattern,int count,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(count is <1 or >1024)throw new ArgumentException("WORLD_AREA_PATTERN_INVALID");
        return AreaCore(domain,environment,pattern,count,budget,token);
    }
    /// <summary>Measures the replayed region without choosing a representative point or requiring positive density.</summary>
    public static WorldAreaPopulationMeasure MeasureArea(WorldAreaPopulationDomain domain,WorldEnvironmentQuery environment,
        string? densityFieldId=null,int maximumCells=4096,long maximumWork=5000000,CancellationToken token=default)
    {
        if(maximumWork is <1 or >200000000)throw new ArgumentException("WORLD_AREA_WORK_INVALID");
        return MeasureArea(domain,environment,densityFieldId,maximumCells,new WorldSpatialWorkBudget(maximumWork,token),token);
    }
    internal static WorldAreaPopulationMeasure MeasureArea(WorldAreaPopulationDomain domain,WorldEnvironmentQuery environment,
        string? densityFieldId,int maximumCells,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        var measured=AreaCore(domain,environment,new(densityFieldId,MaximumCells:maximumCells),0,budget,token);
        return new(measured.RegionId,densityFieldId,measured.Area,measured.Mass,measured.Work);
    }
    private static WorldAreaPopulationPlan AreaCore(WorldAreaPopulationDomain domain,WorldEnvironmentQuery environment,
        WorldAreaPopulationPattern pattern,int count,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(environment);long started=budget.Used;
        if(domain?.Regions is null)throw new ArgumentException("WORLD_AREA_PATTERN_INVALID");
        ValidateAreaPattern(environment,pattern,count);
        var expected=new WorldRegionPlanner().Plan(environment,domain.Regions.Request,budget,token);
        if(System.Text.Json.JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=System.Text.Json.JsonSerializer.Serialize(domain.Regions,WorldJson.CreateOptions()))
            throw new ArgumentException("WORLD_AREA_REGION_PLAN_MISMATCH");
        var region=domain.Regions.Regions.SingleOrDefault(r=>r.Id==domain.RegionId)??throw new ArgumentException("WORLD_AREA_REGION_REQUIRED");
        if(domain.AllocationId is {} allocation)
        {
            if(domain.Layouts is null)throw new ArgumentException("WORLD_AREA_ALLOCATION_REQUIRED");
            var replay=new WorldLocalLayoutPlanner().Plan(environment,domain.Regions,domain.Layouts.Definition,budget,token);
            if(System.Text.Json.JsonSerializer.Serialize(replay,WorldJson.CreateOptions())!=System.Text.Json.JsonSerializer.Serialize(domain.Layouts,WorldJson.CreateOptions()))
                throw new ArgumentException("WORLD_AREA_LAYOUT_PLAN_MISMATCH");
            region=domain.Layouts.Allocations.SingleOrDefault(a=>a.Id==allocation&&a.RegionId==domain.RegionId)?.Shape??throw new ArgumentException("WORLD_AREA_ALLOCATION_REQUIRED");
        }
        else if(domain.Layouts is not null)throw new ArgumentException("WORLD_AREA_LAYOUT_UNUSED");
        var normalized=WorldRegionPlanner.ProfileFor(domain.Regions,domain.RegionId);
        return AreaVerifiedRegion(region,environment,normalized,pattern,count,budget,token,started,
            resweepTerrain:domain.Regions.Request.Representation==WorldRegionRepresentation.Membership,
            verifiedMembership:domain.Regions.Request.Representation==WorldRegionRepresentation.Membership);
    }

    internal static void ValidateAreaPattern(WorldEnvironmentQuery environment,WorldAreaPopulationPattern pattern,int count)
    {
        if(pattern is null||count is <0 or >1024||pattern.MaximumCells is <1 or >16384||pattern.MaximumDepth is <1 or >32||
            !double.IsFinite(pattern.AxisRotationDegrees)||Math.Abs(pattern.AxisRotationDegrees)>180||
            pattern.DensityFieldId is {} field&&!environment.Definition.Fields.Any(f=>f.Id==field))throw new ArgumentException("WORLD_AREA_PATTERN_INVALID");
        if(pattern.Interior is {} search)WorldRegionPlanner.ValidateInteriorSearch(search);
        if(pattern.PartitionSearch is {} partition)
        {
            if(partition.AxisOffsetsDegrees is not {Count:>=1 and <=16}||partition.MaximumAttempts is <1 or >4096||
                partition.AxisOffsetsDegrees.Any(a=>!double.IsFinite(a)||Math.Abs(a)>180)||
                partition.Seed is {} seed&&(!WorldEnvironmentQuery.Text(seed)||seed.Length>256)||
                !double.IsFinite(partition.Bend)||Math.Abs(partition.Bend)>1||
                !double.IsFinite(partition.BendOffset)||Math.Abs(partition.BendOffset)>1e7||
                partition.BendOffset!=0&&partition.Bend==0&&(partition.AlternateBends is null||partition.AlternateBends.All(b=>b==0))||
                partition.AlternateBendOffsets is {} positions&&(positions.Count is <1 or >8||
                    positions.Any(p=>!double.IsFinite(p)||Math.Abs(p)>1e7||p==partition.BendOffset)||positions.Distinct().Count()!=positions.Count||
                    partition.Bend==0&&(partition.AlternateBends is null||partition.AlternateBends.All(b=>b==0)))||
                partition.AlternateBends is {} bends&&(bends.Count is <1 or >8||bends.Any(b=>!double.IsFinite(b)||Math.Abs(b)>1||b==partition.Bend)||bends.Distinct().Count()!=bends.Count))
                throw new ArgumentException("WORLD_AREA_PARTITION_SEARCH_INVALID");
            for(int i=0;i<partition.AxisOffsetsDegrees.Count;i++)for(int j=0;j<i;j++)
                if(Math.Abs(Math.IEEERemainder(partition.AxisOffsetsDegrees[i]-partition.AxisOffsetsDegrees[j],180))<1e-9)
                    throw new ArgumentException("WORLD_AREA_PARTITION_SEARCH_INVALID");
        }
    }

    internal static bool AreaSemanticFailure(ArgumentException error)=>error is WorldAreaQualificationException||new[]{"WORLD_LAYOUT_DISCONNECTED_ALLOCATION","WORLD_AREA_CENTROID_OUTSIDE:",
        "WORLD_AREA_CENTROID_ZERO_DENSITY:","WORLD_AREA_INTERIOR_CLEARANCE_UNSATISFIED:","WORLD_REGION_SEED_COMPONENT_AMBIGUOUS:",
        "WORLD_AREA_REQUIRED_POSITION_OUTSIDE:","WORLD_AREA_REQUIRED_POSITION_ZERO_DENSITY:",
        "WORLD_AREA_PARTITION_UNRESOLVED:","WORLD_AREA_STRATUM_MINIMUM_AREA:"}.Any(code=>error.Message.StartsWith(code,StringComparison.Ordinal));

    // Only callers which have replayed their region/parent plan may enter here. The same
    // integrator and connected-stratum checks serve population and local program spaces.
    internal static WorldAreaPopulationPlan AreaVerifiedRegion(WorldRegion region,WorldEnvironmentQuery environment,WorldSuitabilityProfile normalized,
        WorldAreaPopulationPattern pattern,int count,WorldSpatialWorkBudget budget,CancellationToken token,long? workStarted=null,IReadOnlyList<double>? relativeWeights=null,
        IReadOnlyList<WorldAreaStratumRequirement>? requirements=null,Action<int,WorldPopulationStratum>? qualify=null,
        IReadOnlyList<WorldAreaPartitionChoice>? requiredPartitions=null,bool resweepTerrain=false,bool verifiedMembership=false)
    {
        token.ThrowIfCancellationRequested();
        if(pattern is null)throw new ArgumentException("WORLD_AREA_PATTERN_INVALID");
        if(pattern.PartitionSearch is {} declared&&declared.AxisOffsetsDegrees is not null)
            pattern=pattern with{PartitionSearch=declared with{AxisOffsetsDegrees=Array.AsReadOnly(declared.AxisOffsetsDegrees.ToArray()),
                AlternateBends=declared.AlternateBends is null?null:Array.AsReadOnly(declared.AlternateBends.ToArray()),
                AlternateBendOffsets=declared.AlternateBendOffsets is null?null:Array.AsReadOnly(declared.AlternateBendOffsets.ToArray())}};
        ValidateAreaPattern(environment,pattern,count);long started=workStarted??budget.Used;
        if(requiredPartitions is not null)
        {
            var keys=new List<(int First,int Members)>();
            void Keys(int first,int members){if(members<2)return;keys.Add((first,members));int left=members/2;Keys(first,left);Keys(first+left,members-left);}
            Keys(0,count);
            if(pattern.PartitionSearch is null||requiredPartitions.Count!=keys.Count||requiredPartitions.Any(c=>c is null)||
                requiredPartitions.Select(c=>(c.FirstMember,c.Members)).Distinct().Count()!=keys.Count||
                requiredPartitions.Any(c=>!keys.Contains((c.FirstMember,c.Members))||!pattern.PartitionSearch.AxisOffsetsDegrees.Contains(c.AxisOffsetDegrees)||
                    (pattern.PartitionSearch.AlternateBends is null?c.Bend is not null:
                        c.Bend is null||c.Bend!=pattern.PartitionSearch.Bend&&!pattern.PartitionSearch.AlternateBends.Contains(c.Bend.Value))||
                    (pattern.PartitionSearch.AlternateBendOffsets is null?c.BendOffset is not null:
                        c.BendOffset is null||c.BendOffset!=pattern.PartitionSearch.BendOffset&&!pattern.PartitionSearch.AlternateBendOffsets.Contains(c.BendOffset.Value)||
                        (c.Bend??pattern.PartitionSearch.Bend)==0&&c.BendOffset!=pattern.PartitionSearch.BendOffset)))
                throw new ArgumentException("WORLD_AREA_PARTITION_BINDING_INVALID");
            requiredPartitions=Array.AsReadOnly(requiredPartitions.ToArray());
        }
        if(relativeWeights is not null&&(count<1||relativeWeights.Count!=count||relativeWeights.Any(w=>!double.IsFinite(w)||w<=0||w>4e14)))
            throw new ArgumentException("WORLD_AREA_WEIGHTS_INVALID");
        relativeWeights=relativeWeights is null?null:Array.AsReadOnly(relativeWeights.ToArray());
        if(requirements is not null&&(requirements.Count!=count||requirements.Any(r=>r is null||!WorldEnvironmentQuery.Id(r.Id)||
            !double.IsFinite(r.MinimumArea)||r.MinimumArea<=0||r.MinimumArea>4e14||!double.IsFinite(r.MinimumClearance)||r.MinimumClearance<0||r.MinimumClearance>1e7||
            r.RequiredPosition is {} position&&!WorldHeightFieldQuery.Point(position))))
            throw new ArgumentException("WORLD_AREA_STRATUM_REQUIREMENTS_INVALID");
        if(requirements?.Any(r=>r.MinimumClearance>0)==true&&pattern.Interior is null)throw new ArgumentException("WORLD_AREA_STRATUM_REQUIREMENTS_INVALID");
        double Weight(int index)=>relativeWeights?[index]??1;
        double totalWeight=Enumerable.Range(0,count).Sum(Weight);
        var centroidQuery=new WorldPointSuitability(environment,normalized);
        long centroidCost=1+environment.OrderedFields.Sum(f=>f.Terms.Count+1)+normalized.Requirements.Count+normalized.Preferences.Count+
            environment.Surface.Definition.Patches.Sum(p=>p.Boundary.Vertices.Count)+environment.Surface.Exclusions.Sum(e=>e.Boundary.Vertices.Count)+(environment.Surface.Definition.Boundary?.Vertices.Count??0);
        var cells=region.Cells.Select(c=>
        {
            if(c?.Vertices is not {Count:>=3 and <=64}||c.Vertices.Any(p=>!Finite(p.X)||!Finite(p.Y)))throw new ArgumentException("WORLD_AREA_CELL_INVALID");
            budget.Charge(8L*c.Vertices.Count*c.Vertices.Count);c.Validate();
            if(!ConvexParcelGeometry.IsConvex(c)||Math.Abs(WorldRegionGeometry.Area(c.Vertices))<=1e-8)throw new ArgumentException("WORLD_AREA_CELL_INVALID");
            return c.SignedArea()>0?c:new WorldPolygon(c.Vertices.Reverse().ToArray());
        }).ToArray();
        var bounds=cells.Select(c=>(MinX:c.Vertices.Min(p=>p.X),MaxX:c.Vertices.Max(p=>p.X),
            MinY:c.Vertices.Min(p=>p.Y),MaxY:c.Vertices.Max(p=>p.Y))).ToArray();
        budget.Charge(cells.Sum(c=>(long)c.Vertices.Count)*4);
        for(int i=0;i<cells.Length;i++)for(int j=i+1;j<cells.Length;j++)
        {
            budget.Charge();var a=bounds[i];var b=bounds[j];
            if(a.MaxX<=b.MinX||b.MaxX<=a.MinX||a.MaxY<=b.MinY||b.MaxY<=a.MinY)continue;
            budget.Charge(4L*cells[i].Vertices.Count*cells[j].Vertices.Count);
            if(TerrainCoverageTopology.IntersectionArea(cells[i],cells[j])>1e-8)throw new ArgumentException("WORLD_AREA_INPUT_OVERLAP");
        }
        double sourceArea=cells.Sum(c=>WorldRegionGeometry.Area(c.Vertices));
        if(!double.IsFinite(region.Area)||Math.Abs(sourceArea-region.Area)>Math.Max(1,sourceArea)*1e-8)throw new ArgumentException("WORLD_AREA_SOURCE_ACCOUNTING");
        var atoms=new List<MassPiece>();
        IReadOnlyList<WorldEnvironmentalField>? coordinateFields=null;
        if(resweepTerrain&&verifiedMembership&&pattern.DensityFieldId is {} densityId)
            coordinateFields=WorldEnvironmentRegions.CoordinateFields(environment,densityId,budget);
        void AddPieces(WorldSurfaceSample sample,IReadOnlyList<WorldPoint> vertices)
        {
            if(sample.Status!=WorldSurfaceStatus.Sampled)throw new ArgumentException("WORLD_AREA_SURFACE_MISSING");
            foreach(var piece in WorldEnvironmentRegions.Evaluate(environment,sample,vertices,4096,budget,coordinateFields))
            {
                if(Math.Abs(WorldRegionGeometry.Area(piece.Vertices))==0)continue;
                var density=pattern.DensityFieldId is {} id?piece.Fields[id]:new WorldEnvironmentRegions.Affine(1,0,0);
                budget.Charge(piece.Vertices.Count);
                if(piece.Vertices.Any(p=>WorldEnvironmentRegions.Value(density,sample.Position,p)<0))throw new ArgumentException("WORLD_AREA_NEGATIVE_DENSITY");
                if(atoms.Count>=pattern.MaximumCells)throw new ArgumentException("WORLD_AREA_CELL_BUDGET");
                atoms.Add(new(piece.Vertices,sample.Position,density));
            }
        }
        // A replayed membership region proves coverage and eligibility for its
        // whole shape. Uniform mass depends only on that shape, not on the terrain
        // planes inside it. Arbitrary masks do not have this proof and must sweep.
        bool uniformMembership=resweepTerrain&&verifiedMembership&&pattern.DensityFieldId is null;
        var surfaceArea=resweepTerrain&&!uniformMembership&&coordinateFields is null?new WorldSurfaceAreaQuery(environment.Surface):null;
        foreach(var cell in cells)
        {
            // Membership cells can cross source terrain planes. Re-sweep those
            // cells before evaluating density; terrain-face cells already carry
            // the exact decomposition and must not be recut at coincident seams.
            if(uniformMembership)
            {
                budget.Charge(cell.Vertices.Count);
                if(atoms.Count>=pattern.MaximumCells)throw new ArgumentException("WORLD_AREA_CELL_BUDGET");
                atoms.Add(new(cell.Vertices,cell.Centroid(),new WorldEnvironmentRegions.Affine(1,0,0)));
            }
            else if(surfaceArea is not null)surfaceArea.VisitRegions(cell,(sample,vertices)=>{AddPieces(sample,vertices);return true;},budget,token);
            else AddPieces(environment.Surface.Sample(cell.Centroid(),token),cell.Vertices);
        }
        var whole=Measure(atoms,budget);
        if(whole.Mass<0||count>0&&whole.Mass==0||!double.IsFinite(whole.Mass))throw new ArgumentException("WORLD_AREA_ZERO_MASS");
        if(Math.Abs(whole.Area-sourceArea)>Math.Max(1,sourceArea)*1e-8)throw new ArgumentException("WORLD_AREA_SOURCE_ACCOUNTING");
        if(count==0)return new(1,pattern,region.Id,whole.Area,whole.Mass,[],budget.Used-started,
            PartitionAttempts:pattern.PartitionSearch is null?null:Array.Empty<WorldAreaPartitionAttempt>());
        var strata=new List<WorldPopulationStratum>();int retained=0;long qualificationWork=0;
        var partitionAttempts=new List<WorldAreaPartitionAttempt>();
        bool jointAnchorSearch=qualify is not null&&requirements?.Any(r=>r.PreferredProfile is not null)==true;
        void Partition(IReadOnlyList<MassPiece> pieces,int members,int depth,int first,Action continuation)
        {
            long continuationWork=0;
            void Continue()
            {
                long before=budget.Used,qualificationBefore=qualificationWork;
                try{continuation();}
                finally{continuationWork+=budget.Used-before-(qualificationWork-qualificationBefore);}
            }
            token.ThrowIfCancellationRequested();var facts=Measure(pieces,budget);
            if(members==1)
            {
                string id=region.Id+"/area-"+strata.Count.ToString("D5",System.Globalization.CultureInfo.InvariantCulture);
                var requirement=requirements?[first];
                if(requirement is not null)
                {
                    budget.Charge();
                    if(facts.Area<requirement.MinimumArea)throw new ArgumentException("WORLD_AREA_STRATUM_MINIMUM_AREA:"+requirement.Id);
                }
                var polygons=pieces.Select(p=>new WorldPolygon(p.Vertices)).ToArray();
                WorldRegionInteriorAnchor? interior=null;WorldRegion? connected=null;
                IReadOnlyList<(WorldRegionInteriorAnchor Anchor,double? Score)>? preferredAnchors=null;
                var position=requirement?.RequiredPosition??facts.Centroid;
                if(pattern.Interior is not null||requirement?.RequiredPosition is not null)
                {
                    var search=pattern.Interior??new WorldRegionInteriorSearch();
                    if(requirement is not null)search=search with{MinimumClearance=Math.Max(search.MinimumClearance,requirement.MinimumClearance)};
                    var components=WorldRegionGeometry.Components(environment,normalized,polygons,budget,token);
                    if(components.Count!=1)throw new ArgumentException("WORLD_LAYOUT_DISCONNECTED_ALLOCATION");
                    var shape=components[0];
                    var initial=new WorldRegionSeed(id,region.Seed.Purpose,region.Seed.SourceRuleId,polygons[0].Centroid(),1e-8,normalized);
                    connected=new(id,initial,shape.Cells,shape.BoundaryEdges,shape.Area);
                    interior=requirement?.RequiredPosition is not null?WorldRegionPlanner.AnchorAt(connected,position,budget):WorldRegionPlanner.FindInterior(connected,search,budget);
                    if(requirement?.RequiredPosition is not null&&interior.Clearance<=0)
                        throw new ArgumentException("WORLD_AREA_REQUIRED_POSITION_OUTSIDE:"+requirement.Id);
                    if(interior.Clearance<search.MinimumClearance)throw new ArgumentException("WORLD_AREA_INTERIOR_CLEARANCE_UNSATISFIED:"+id);
                    if(requirement?.PreferredProfile is {} preference)
                    {
                        int evaluations=interior.Evaluations;
                        preferredAnchors=WorldRegionPlanner.PreferredAnchors(connected,interior,environment,preference,search.MinimumClearance,
                            pattern.DensityFieldId,search.MaximumCells,ref evaluations,budget,token);
                        preferredAnchors=preferredAnchors.Select(c=>(c.Anchor with{Evaluations=evaluations},c.Score)).ToArray();
                        interior=preferredAnchors[0].Anchor;
                    }
                    position=interior.Position;
                }
                int options=preferredAnchors?.Count??1;
                int savedStrata=strata.Count,savedRetained=retained,savedAttempts=partitionAttempts.Count;
                for(int rank=0;rank<options;rank++)
                {
                    token.ThrowIfCancellationRequested();
                    if(preferredAnchors is not null){interior=preferredAnchors[rank].Anchor;position=interior.Position;}
                    if(facts.Mass<=0||!pieces.Any(p=>new WorldPolygon(p.Vertices).ContainsPoint(position)))
                        throw new ArgumentException((requirement?.RequiredPosition is null?"WORLD_AREA_CENTROID_OUTSIDE:"+id:"WORLD_AREA_REQUIRED_POSITION_OUTSIDE:"+requirement.Id));
                    budget.Charge(centroidCost);var point=centroidQuery.Evaluate(position,token);
                    if(!point.Eligible)throw new ArgumentException((requirement?.RequiredPosition is null?"WORLD_AREA_CENTROID_OUTSIDE:"+id:"WORLD_AREA_REQUIRED_POSITION_OUTSIDE:"+requirement.Id));
                    if(pattern.DensityFieldId is {} field&&point.Observation.Values[field]<=0)
                        throw new ArgumentException((requirement?.RequiredPosition is null?"WORLD_AREA_CENTROID_ZERO_DENSITY:"+id:"WORLD_AREA_REQUIRED_POSITION_ZERO_DENSITY:"+requirement.Id));
                    var seed=new WorldRegionSeed(id,region.Seed.Purpose,region.Seed.SourceRuleId,position,1e-8,normalized);
                    connected??=WorldRegionGeometry.Assemble(environment,normalized,polygons,[seed],budget,token,requireAllCells:true).Single();
                    retained=savedRetained+polygons.Length;
                    if(retained>pattern.MaximumCells)throw new ArgumentException("WORLD_AREA_CELL_BUDGET");
                    var stratum=new WorldPopulationStratum(id,position,connected.Area,facts.Mass,connected.Cells,connected.BoundaryEdges,
                        interior is null?null:facts.Centroid,interior,preferredAnchors?[rank].Score,preferredAnchors is null?null:rank);
                    strata.Add(stratum);
                    try
                    {
                        if(qualify is not null)
                        {
                            long before=budget.Used;
                            try{qualify(first,stratum);}finally{qualificationWork+=budget.Used-before;}
                        }
                        Continue();
                    }
                    catch(ArgumentException error)when(rank+1<options&&AreaSemanticFailure(error))
                    {
                        strata.RemoveRange(savedStrata,strata.Count-savedStrata);retained=savedRetained;
                        for(int i=savedAttempts;i<partitionAttempts.Count;i++)if(partitionAttempts[i].Status==WorldAreaPartitionStatus.Selected)
                            partitionAttempts[i]=partitionAttempts[i] with{Status=WorldAreaPartitionStatus.Superseded,Rejection="WORLD_AREA_ANCHOR_RECONSIDERED"};
                        continue;
                    }
                    return;
                }
                throw new InvalidOperationException("WORLD_AREA_ANCHOR_OPTIONS_EXHAUSTED");
            }
            if(depth>=pattern.MaximumDepth)throw new ArgumentException("WORLD_AREA_DEPTH_BUDGET");
            int leftCount=members/2;double target=facts.Mass*leftCount/members;
            if(relativeWeights is not null)
            {
                budget.Charge(members);double groupWeight=Enumerable.Range(first,members).Sum(Weight),leftWeight=Enumerable.Range(first,leftCount).Sum(Weight);
                target=facts.Mass*(leftWeight/groupWeight);
            }
            double baseAngle=.5*Math.Atan2(2*facts.XY,facts.XX-facts.YY)+pattern.AxisRotationDegrees*Math.PI/180;
            string? rejection=null;ArgumentException? lastFailure=null;
            IReadOnlyList<double> offsets=requiredPartitions is null?pattern.PartitionSearch?.AxisOffsetsDegrees??new[]{0d}:
                new[]{requiredPartitions.Single(c=>c.FirstMember==first&&c.Members==members).AxisOffsetDegrees};
            if(requiredPartitions is null&&pattern.PartitionSearch?.Seed is {} partitionSeed&&offsets.Count>1)
            {
                // Stable semantic ordering, independent of runtime hash codes and
                // unaffected by other partition branches or failed attempts.
                budget.Charge(offsets.Count*(1L+(long)Math.Ceiling(Math.Log2(offsets.Count+1d))));
                offsets=offsets.Select((offset,index)=>(offset,index,rank:Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{partitionSeed,first,members,depth,offset})))))
                    .OrderBy(x=>x.rank,StringComparer.Ordinal).ThenBy(x=>x.index).Select(x=>x.offset).ToArray();
            }
            // A pinned semantic member can belong on either side of an axis.
            // Search both directed normals within the declared attempt/work cap;
            // unpinned requests retain their original canonical sign and order.
            bool directed=pattern.PartitionSearch is not null&&requirements is not null&&Enumerable.Range(first,members).Any(i=>requirements[i].RequiredPosition is not null);
            if(directed)budget.Charge(members);
            var policy=pattern.PartitionSearch;bool alternateBends=policy?.AlternateBends is not null;
            bool alternatePositions=policy?.AlternateBendOffsets is not null;
            IReadOnlyList<double> bends=requiredPartitions is not null&&alternateBends?
                new[]{requiredPartitions.Single(c=>c.FirstMember==first&&c.Members==members).Bend!.Value}:
                alternateBends?new[]{policy!.Bend}.Concat(policy.AlternateBends!).ToArray():new[]{policy?.Bend??0};
            IReadOnlyList<double> positions=requiredPartitions is not null&&alternatePositions?
                new[]{requiredPartitions.Single(c=>c.FirstMember==first&&c.Members==members).BendOffset!.Value}:
                alternatePositions?new[]{policy!.BendOffset}.Concat(policy.AlternateBendOffsets!).ToArray():new[]{policy?.BendOffset??0};
            IEnumerable<(double Offset,double Bend,double Position)> configurations=offsets.SelectMany(offset=>bends.SelectMany(bend=>
                positions.Where(position=>bend!=0||position==(policy?.BendOffset??0)).Select(position=>(offset,bend,position))));
            if(requiredPartitions is null&&(alternateBends||alternatePositions)&&policy!.Seed is {} bendSeed)
            {
                var choices=configurations.ToArray();
                budget.Charge(choices.Length*(1L+(long)Math.Ceiling(Math.Log2(choices.Length+1d))));
                configurations=choices.Select((c,index)=>(Choice:c,Index:index,Rank:Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(alternatePositions?
                        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{bendSeed,first,members,depth,c.Offset,c.Bend,c.Position}):
                        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{bendSeed,first,members,depth,c.Offset,c.Bend})))))
                    .OrderBy(x=>x.Rank,StringComparer.Ordinal).ThenBy(x=>x.Index).Select(x=>x.Choice).ToArray();
            }
            foreach(var choice in configurations.SelectMany(c=>directed?new[]{(c.Offset,c.Bend,c.Position,Reverse:false),(c.Offset,c.Bend,c.Position,Reverse:true)}:new[]{(c.Offset,c.Bend,c.Position,Reverse:false)}))
            {
                double offset=choice.Offset;
                if(pattern.PartitionSearch is {} search&&partitionAttempts.Count>=search.MaximumAttempts)
                    throw new ArgumentException("WORLD_AREA_PARTITION_ATTEMPT_BUDGET");
                if(pattern.PartitionSearch is not null)budget.Charge();long before=budget.Used,beforeQualification=qualificationWork,beforeContinuation=continuationWork;int savedCount=strata.Count,savedRetained=retained,slot=partitionAttempts.Count;
                if(pattern.PartitionSearch is not null)partitionAttempts.Add(new(slot+1,first,members,depth,offset,(baseAngle+offset*Math.PI/180)*180/Math.PI,
                    facts.Centroid,0,target,0,0,WorldAreaPartitionStatus.Rejected,null,0,alternateBends?choice.Bend:null,alternatePositions?choice.Position:null));
                try
                {
                    double angle=baseAngle+offset*Math.PI/180;
                    // Exact quarter turns have exact axis vectors. Evaluating
                    // cos(pi/2) instead creates an artificial sliver at a later
                    // perpendicular cut. Do not round non-cardinal angles.
                    double quarter=angle/(Math.PI/2);
                    (double dx,double dy)=quarter==Math.Truncate(quarter)?(((int)quarter%4+4)%4) switch{
                        0=>(1d,0d),1=>(0d,1d),2=>(-1d,0d),_=>(0d,-1d)}:(Math.Cos(angle),Math.Sin(angle));
                    // Canonical axis sign orders semantic leaves; it does not impose streets or cells.
                    if(dx<0||Math.Abs(dx)<1e-14&&dy<0){dx=-dx;dy=-dy;}
                    if(choice.Reverse){dx=-dx;dy=-dy;}
                    var origin=facts.Centroid;
                    double bend=choice.Bend,bendOffset=choice.Position;
                    double Tangent(WorldPoint p)=>(p.X-origin.X)*-dy+(p.Y-origin.Y)*dx-bendOffset;
                    double Coordinate(WorldPoint p)=>(p.X-origin.X)*dx+(p.Y-origin.Y)*dy-bend*Math.Abs(Tangent(p));
                    // Preserve an exact horizontal/vertical source seam while
                    // solving the intersection with either the tangent or cut.
                    // Only use the solution when it stays on that source segment
                    // and differs from interpolation by arithmetic roundoff.
                    WorldPoint ProjectIntersection(IReadOnlyList<WorldPoint> source,WorldPoint p,double nx,double ny,double value)
                    {
                        if(source.Contains(p))return p;
                        double tolerance=64*2.220446049250313e-16*
                            (Math.Abs(p.X)+Math.Abs(p.Y)+Math.Abs(origin.X)+Math.Abs(origin.Y)+Math.Abs(value)+1);
                        for(int edge=0;edge<source.Count;edge++)
                        {
                            var a=source[edge];var b=source[(edge+1)%source.Count];
                            if(a.X==b.X&&p.X==a.X&&ny!=0&&p.Y>=Math.Min(a.Y,b.Y)&&p.Y<=Math.Max(a.Y,b.Y))
                            {
                                double y=origin.Y+(value-(p.X-origin.X)*nx)/ny;
                                if(double.IsFinite(y)&&Math.Abs(y-p.Y)<=tolerance&&y>=Math.Min(a.Y,b.Y)&&y<=Math.Max(a.Y,b.Y))return new(p.X,y);
                            }
                            if(a.Y==b.Y&&p.Y==a.Y&&nx!=0&&p.X>=Math.Min(a.X,b.X)&&p.X<=Math.Max(a.X,b.X))
                            {
                                double x=origin.X+(value-(p.Y-origin.Y)*ny)/nx;
                                if(double.IsFinite(x)&&Math.Abs(x-p.X)<=tolerance&&x>=Math.Min(a.X,b.X)&&x<=Math.Max(a.X,b.X))return new(x,p.Y);
                            }
                        }
                        return Math.Abs(nx)>=Math.Abs(ny)?new WorldPoint(origin.X+(value-(p.Y-origin.Y)*ny)/nx,p.Y):
                            new WorldPoint(p.X,origin.Y+(value-(p.X-origin.X)*nx)/ny);
                    }
                    IReadOnlyList<MassPiece> partitionPieces=pieces;
                    if(bend!=0)
                    {
                        var split=new List<MassPiece>();
                        foreach(var piece in pieces)
                        {
                            double TangentStable(WorldPoint p)
                            {
                                double value=Tangent(p),roundoff=Math.Min(1e-8,64*2.220446049250313e-16*(Math.Abs(p.X)+Math.Abs(p.Y)+Math.Abs(origin.X)+Math.Abs(origin.Y)+Math.Abs(bendOffset)+1));
                                return Math.Abs(value)<=roundoff?0:value;
                            }
                            budget.Charge(piece.Vertices.Count);
                            double min=piece.Vertices.Min(TangentStable),max=piece.Vertices.Max(TangentStable);
                            if(min>=0||max<=0){split.Add(piece);continue;}
                            foreach(int side in new[]{-1,1})
                            {
                                var vertices=WorldRegionGeometry.Clip(piece.Vertices,p=>side*TangentStable(p),budget);
                                double tx=-dy,ty=dx;
                                budget.Charge(vertices.Count*(1L+piece.Vertices.Count));
                                vertices=vertices.Select(p=>ProjectIntersection(piece.Vertices,p,tx,ty,bendOffset)).ToArray();
                                double area=WorldRegionGeometry.Area(vertices);if(area==0)continue;
                                if(area<=1e-8)throw new ArgumentException("WORLD_AREA_GEOMETRY_RESOLUTION");
                                if(split.Count>=pattern.MaximumCells)throw new ArgumentException("WORLD_AREA_CELL_BUDGET");
                                split.Add(piece with{Vertices=vertices});
                            }
                        }
                        if(split.Count>pattern.MaximumCells)throw new ArgumentException("WORLD_AREA_CELL_BUDGET");
                        partitionPieces=split.AsReadOnly();
                    }
                    budget.Charge(partitionPieces.Sum(p=>p.Vertices.Count)*2L);
                    double lo=partitionPieces.Min(p=>p.Vertices.Min(Coordinate)),hi=partitionPieces.Max(p=>p.Vertices.Max(Coordinate));
                    double cut=0;
                    // Trial cuts need only mass, not centroids and second moments.
                    // Cache each complete piece's projection interval and mass;
                    // integrate raw clipped triangles only where the cut crosses it.
                    var projected=partitionPieces.Select(piece=>{
                        budget.Charge(piece.Vertices.Count*2L);
                        return(Piece:piece,Low:piece.Vertices.Min(Coordinate),High:piece.Vertices.Max(Coordinate),Mass:MeasureMass(piece,budget));
                    }).ToArray();
                    double LeftMass(double value)
                    {
                        double mass=0;
                        foreach(var item in projected)
                        {
                            budget.Charge(2);
                            if(item.High<=value){mass+=item.Mass;continue;}
                            if(item.Low>=value)continue;
                            var vertices=WorldRegionGeometry.ClipForMeasure(item.Piece.Vertices,p=>value-Coordinate(p),budget);
                            mass+=MeasureMass(item.Piece with{Vertices=vertices},budget);
                        }
                        return mass;
                    }
                    IReadOnlyList<MassPiece> Clip(double value,bool left)
                    {
                        var output=new List<MassPiece>();
                        foreach(var piece in partitionPieces)
                        {
                            double side=bend==0?0:Math.Sign(Tangent(new WorldPolygon(piece.Vertices).Centroid()));
                            double nx=dx+bend*side*dy,ny=dy-bend*side*dx;
                            double projectedValue=value-bend*side*bendOffset;
                            double Signed(WorldPoint p)
                            {
                                double coordinate=Coordinate(p),signed=left?value-coordinate:coordinate-value;
                                double roundoff=Math.Min(1e-8,64*2.220446049250313e-16*(Math.Abs(p.X)+Math.Abs(p.Y)+Math.Abs(origin.X)+Math.Abs(origin.Y)+Math.Abs(value)+1));
                                return Math.Abs(signed)<=roundoff?0:signed;
                            }
                            var vertices=WorldRegionGeometry.Clip(piece.Vertices,Signed,budget);
                            // Evaluate new intersections on the solved cut plane. Edge
                            // interpolation alone may put its two sides one ULP apart.
                            // Original source vertices retain their exact coordinates.
                            budget.Charge(vertices.Count*(1L+piece.Vertices.Count));
                            vertices=vertices.Select(p=>ProjectIntersection(piece.Vertices,p,nx,ny,projectedValue)).ToArray();
                            double area=WorldRegionGeometry.Area(vertices);if(area==0)continue;
                            if(area<=1e-8)throw new ArgumentException("WORLD_AREA_GEOMETRY_RESOLUTION");
                            if(output.Count>=pattern.MaximumCells)throw new ArgumentException("WORLD_AREA_CELL_BUDGET");output.Add(piece with{Vertices=vertices});
                        }
                        return output.AsReadOnly();
                    }
                    // Refine to representable precision before accepting a mass tolerance.
                    // An early approximate cut can leave artificial slivers where a later
                    // terrain clip meets an exactly representable source boundary.
                    double bestCut=lo,bestError=double.PositiveInfinity;
                    for(int iteration=0;iteration<64;iteration++)
                    {
                        budget.Charge();cut=lo+(hi-lo)/2;double mass=LeftMass(cut);
                        double error=Math.Abs(mass-target);
                        if(error<bestError){bestCut=cut;bestError=error;}
                        if(mass==target||cut==lo||cut==hi)break;
                        if(mass<target)lo=cut;else hi=cut;
                    }
                    if(bestError>facts.Mass*1e-10)throw new ArgumentException("WORLD_AREA_MASS_RESOLUTION");
                    cut=bestCut;
                    // Prefer an existing geometric event only when it preserves the solved mass.
                    // This stabilizes coincident seams without rounding onto a placement lattice.
                    budget.Charge(partitionPieces.Sum(p=>p.Vertices.Count));
                    double nearest=partitionPieces.SelectMany(p=>p.Vertices).Select(Coordinate).MinBy(v=>Math.Abs(v-cut));
                    if(Math.Abs(LeftMass(nearest)-target)<=facts.Mass*1e-10)cut=nearest;
                    var left=Clip(cut,true);var right=Clip(cut,false);
                    var lm=Measure(left,budget);var rm=Measure(right,budget);
                    if(Math.Abs(lm.Mass-target)>facts.Mass*1e-8||Math.Abs(lm.Mass+rm.Mass-facts.Mass)>facts.Mass*1e-8||
                        Math.Abs(lm.Area+rm.Area-facts.Area)>Math.Max(1,facts.Area)*1e-8)throw new ArgumentException("WORLD_AREA_MASS_ACCOUNTING");
                    if(pattern.PartitionSearch is not null)
                    {
                        partitionAttempts[slot]=partitionAttempts[slot] with{AngleDegrees=Math.Atan2(dy,dx)*180/Math.PI,Origin=origin,Cut=cut,LeftMass=lm.Mass,RightMass=rm.Mass};
                        foreach(var child in new[]{left,right})
                            if(WorldRegionGeometry.Components(environment,normalized,child.Select(p=>new WorldPolygon(p.Vertices)).ToArray(),budget,token).Count!=1)
                                throw new ArgumentException("WORLD_LAYOUT_DISCONNECTED_ALLOCATION");
                    }
                    if(jointAnchorSearch)Partition(left,leftCount,depth+1,first,()=>Partition(right,members-leftCount,depth+1,first+leftCount,Continue));
                    else{Partition(left,leftCount,depth+1,first,()=>{});Partition(right,members-leftCount,depth+1,first+leftCount,()=>{});Continue();}
                    if(pattern.PartitionSearch is not null)partitionAttempts[slot]=partitionAttempts[slot] with{Status=WorldAreaPartitionStatus.Selected,Rejection=null,Work=budget.Used-before-(qualificationWork-beforeQualification)-(continuationWork-beforeContinuation)};
                    return;
                }
                catch(ArgumentException error)when(pattern.PartitionSearch is not null&&AreaSemanticFailure(error))
                {
                    rejection=error.Message;lastFailure=error;strata.RemoveRange(savedCount,strata.Count-savedCount);retained=savedRetained;
                    partitionAttempts[slot]=partitionAttempts[slot] with{Status=WorldAreaPartitionStatus.Rejected,Rejection=rejection,Work=budget.Used-before-(qualificationWork-beforeQualification)-(continuationWork-beforeContinuation)};
                    for(int i=slot+1;i<partitionAttempts.Count;i++)if(partitionAttempts[i].Status==WorldAreaPartitionStatus.Selected)
                        partitionAttempts[i]=partitionAttempts[i] with{Status=WorldAreaPartitionStatus.Superseded,Rejection="WORLD_AREA_ANCESTOR_RECONSIDERED"};
                }
            }
            var failure=new ArgumentException("WORLD_AREA_PARTITION_UNRESOLVED:"+region.Id+"/"+first+":"+rejection,lastFailure);
            failure.Data["AreaPartitionAttempts"]=Array.AsReadOnly(partitionAttempts.ToArray());
            failure.Data["AreaRetainedStrata"]=strata.Count;failure.Data["AreaRetainedCells"]=retained;
            throw failure;
        }
        Partition(atoms,count,0,0,()=>{});
        if(strata.Count!=count||strata.Where((s,i)=>Math.Abs(s.Mass-whole.Mass*(Weight(i)/totalWeight))>whole.Mass*(Weight(i)/totalWeight)*1e-7).Any()||
            Math.Abs(strata.Sum(s=>s.Mass)-whole.Mass)>whole.Mass*1e-8||Math.Abs(strata.Sum(s=>s.Area)-whole.Area)>Math.Max(1,whole.Area)*1e-8)
            throw new ArgumentException("WORLD_AREA_MASS_ACCOUNTING");
        return new(1,pattern,region.Id,whole.Area,whole.Mass,strata.AsReadOnly(),budget.Used-started-qualificationWork,relativeWeights,pattern.PartitionSearch is null?null:partitionAttempts.AsReadOnly());
    }

    private static double MeasureMass(MassPiece piece,WorldSpatialWorkBudget budget)
    {
        double mass=0;
        for(int i=1;i+1<piece.Vertices.Count;i++)
        {
            budget.Charge(12);var a=piece.Vertices[0];var b=piece.Vertices[i];var c=piece.Vertices[i+1];
            double area=((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X))/2;if(area==0)continue;
            double density=WorldEnvironmentRegions.Value(piece.Density,piece.Origin,a)+WorldEnvironmentRegions.Value(piece.Density,piece.Origin,b)+
                WorldEnvironmentRegions.Value(piece.Density,piece.Origin,c);
            mass+=area*density/3;
        }
        return mass;
    }
    private static AreaMoments Measure(IReadOnlyList<MassPiece> pieces,WorldSpatialWorkBudget budget)
    {
        if(pieces.Count==0)return new(0,0,new(0,0),0,0,0);
        budget.Charge(pieces.Sum(p=>p.Vertices.Count)*4L);
        var points=pieces.SelectMany(p=>p.Vertices).ToArray();
        var origin=new WorldPoint(points.Min(p=>p.X)+(points.Max(p=>p.X)-points.Min(p=>p.X))/2,points.Min(p=>p.Y)+(points.Max(p=>p.Y)-points.Min(p=>p.Y))/2);
        double area=0,mass=0,mx=0,my=0,gx=0,gy=0,xx=0,xy=0,yy=0;
        foreach(var piece in pieces)for(int i=1;i+1<piece.Vertices.Count;i++)
        {
            budget.Charge(32);var a=piece.Vertices[0];var b=piece.Vertices[i];var c=piece.Vertices[i+1];
            double ar=((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X))/2;if(ar==0)continue;
            // Signed fan integration remains valid through collinear seam roundoff.
            // Do not discard a tiny negative fan term from an otherwise positive
            // clipped polygon: its signed moment cancels the matching contribution.
            double ax=a.X-origin.X,bx=b.X-origin.X,cx=c.X-origin.X,ay=a.Y-origin.Y,by=b.Y-origin.Y,cy=c.Y-origin.Y;
            double ra=WorldEnvironmentRegions.Value(piece.Density,piece.Origin,a),rb=WorldEnvironmentRegions.Value(piece.Density,piece.Origin,b),rc=WorldEnvironmentRegions.Value(piece.Density,piece.Origin,c);
            double sx=ax+bx+cx,sy=ay+by+cy,sr=ra+rb+rc;
            area+=ar;mass+=ar*sr/3;mx+=ar*(sx*sr+ax*ra+bx*rb+cx*rc)/12;my+=ar*(sy*sr+ay*ra+by*rb+cy*rc)/12;
            gx+=ar*sx/3;gy+=ar*sy/3;xx+=ar*(sx*sx+ax*ax+bx*bx+cx*cx)/12;xy+=ar*(sx*sy+ax*ay+bx*by+cx*cy)/12;yy+=ar*(sy*sy+ay*ay+by*by+cy*cy)/12;
        }
        if(area<=0)return new(0,0,new(0,0),0,0,0);
        var centroid=mass>0?origin.Offset(mx/mass,my/mass):origin;
        return new(area,mass,centroid,xx/area-gx/area*(gx/area),xy/area-gx/area*(gy/area),yy/area-gy/area*(gy/area));
    }
}
