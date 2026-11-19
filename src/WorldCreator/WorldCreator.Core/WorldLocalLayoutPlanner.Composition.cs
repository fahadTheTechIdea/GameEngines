using WorldCreator.Core.Geometry;
using static WorldCreator.Core.Spatial.WorldRegionGeometry;

namespace WorldCreator.Core.Spatial;

public sealed partial class WorldLocalLayoutPlanner
{
    private sealed record LayoutComposition(List<WorldLocalAllocation> Allocations,List<WorldLocalLayoutArea> Areas,List<WorldLocalGeneratedArea> GeneratedAreas);

    private static LayoutComposition ComposeLayouts(IReadOnlyList<WorldLocalLayout> layouts,WorldEnvironmentQuery environment,
        WorldRegionPlan regions,WorldLocalLayoutDefinition definition,WorldSpatialWorkBudget budget,CancellationToken token)
    {
        void CheckInterfaces(IReadOnlyList<WorldLocalAllocation> selected,string added)
        {
            foreach(var requirement in definition.Interfaces)
            {
                budget.Charge(selected.Count+1);
                if(requirement.FromId!=added&&requirement.ToId!=added||!selected.Any(a=>a.Id==requirement.FromId)||!selected.Any(a=>a.Id==requirement.ToId))continue;
                Interface(selected,requirement,budget);
            }
        }
        LayoutComposition Compose(int ordinal,List<WorldLocalAllocation> prefix,int prefixCells)
        {
            token.ThrowIfCancellationRequested();
            if(ordinal==layouts.Count)return new(prefix,[],[]);
            var layout=layouts[ordinal];var region=layout.ParentAllocationId is {} parentId?prefix.Single(a=>a.Id==parentId).Shape:
                regions.Regions.Single(r=>r.Id==layout.RegionId);
            var profile=WorldRegionPlanner.ProfileFor(regions,layout.RegionId);double allocated=0;LayoutComposition result;
            WorldLocalLayoutArea Measure(double area)
            {
                double residual=region.Area-area,tolerance=Math.Max(1,region.Area)*1e-10;
                if(residual< -tolerance||layout.Strategy!=WorldLocalLayoutStrategy.OrientedGrid&&Math.Abs(residual)>tolerance)
                    throw new ArgumentException("WORLD_LAYOUT_AREA_ACCOUNTING_INVALID");
                if(residual>tolerance&&!WorldEnvironmentQuery.Text(layout.ResidualPurpose))throw new ArgumentException("WORLD_LAYOUT_RESIDUAL_PURPOSE_REQUIRED:"+layout.Id);
                return new(layout.Id,layout.RegionId,region.Area,area,Math.Max(0,residual),layout.ResidualPurpose,layout.ParentAllocationId);
            }
            if(layout.Strategy==WorldLocalLayoutStrategy.AreaBalanced)
            {
                var intents=layout.ProgramIntents!;var staged=new List<WorldPopulationStratum>();LayoutComposition? continuation=null;
                List<WorldLocalAllocation> Append(IReadOnlyList<WorldPopulationStratum> strata)
                {
                    var selected=new List<WorldLocalAllocation>(prefix);int cells=prefixCells;
                    for(int i=0;i<strata.Count;i++)
                    {
                        var intent=intents[i];var stratum=strata[i];
                        if(stratum.Area<intent.MinimumArea)throw new ArgumentException("WORLD_REGION_MINIMUM_AREA:"+intent.Id);
                        if((cells+=stratum.Cells.Count)>definition.MaximumCells)throw new ArgumentException("WORLD_LAYOUT_CELL_BUDGET");
                        var program=new WorldLocalProgram(intent.Id,intent.Purpose,intent.SourceRuleId,stratum.Position,intent.MinimumArea);
                        var seed=new WorldRegionSeed(intent.Id,intent.Purpose,intent.SourceRuleId,stratum.Position,intent.MinimumArea,profile);
                        selected.Add(new(intent.Id,layout.Id,layout.RegionId,program,new(intent.Id,seed,stratum.Cells,stratum.BoundaryEdges,stratum.Area)));
                    }
                    return selected;
                }
                void Qualify(int index,WorldPopulationStratum stratum)
                {
                    if(staged.Count>index)staged.RemoveRange(index,staged.Count-index);
                    staged.Add(stratum);continuation=null;
                    try
                    {
                        var selected=Append(staged);CheckInterfaces(selected,intents[index].Id);
                        if(index==intents.Count-1)
                            continuation=Compose(ordinal+1,selected,prefixCells+staged.Sum(s=>s.Cells.Count));
                    }
                    catch(ArgumentException error)when(layout.AreaPattern!.PartitionSearch is not null&&LocalConstraintFailure(error))
                    {
                        string identity=error.Message.StartsWith("WORLD_LAYOUT_INTERFACE_SPAN_UNSATISFIED:",StringComparison.Ordinal)?
                            "local-interface:"+error.Message["WORLD_LAYOUT_INTERFACE_SPAN_UNSATISFIED:".Length..]:"local-layout:"+layout.Id;
                        throw new WorldAreaQualificationException(identity,error);
                    }
                }
                bool feedback=definition.Interfaces.Count>0||ordinal+1<layouts.Count;
                var plan=WorldPopulationGeometry.AreaVerifiedRegion(region,environment,profile,layout.AreaPattern!,intents.Count,budget,token,
                    relativeWeights:intents.Select(i=>i.AllocationWeight??i.MinimumArea).ToArray(),
                    requirements:layout.AreaPattern!.PartitionSearch is null?null:intents.Select(i=>new WorldAreaStratumRequirement(i.Id,i.MinimumArea,0)).ToArray(),
                    qualify:feedback?Qualify:null,resweepTerrain:regions.Request.Representation==WorldRegionRepresentation.Membership,
                    verifiedMembership:regions.Request.Representation==WorldRegionRepresentation.Membership);
                var selected=Append(plan.Strata);allocated=plan.Strata.Sum(s=>s.Area);
                result=feedback?continuation!:Compose(ordinal+1,selected,prefixCells+plan.Strata.Sum(s=>s.Cells.Count));
                result.GeneratedAreas.Insert(0,new(layout.Id,plan));
            }
            else
            {
                var selected=new List<WorldLocalAllocation>(prefix);int count=prefixCells;
                var partitionPaths=layout.Partition is null?null:PartitionPaths(layout,budget);
                foreach(var program in layout.Programs)
                {
                    if(!region.Cells.Any(c=>c.ContainsPoint(program.Anchor)))throw new ArgumentException("WORLD_LAYOUT_ANCHOR_OUTSIDE_PARENT:"+program.Id);
                    var cells=new List<WorldPolygon>();
                    foreach(var source in region.Cells)
                    {
                        IReadOnlyList<WorldPoint> polygon=source.Vertices;
                        if(layout.Strategy==WorldLocalLayoutStrategy.PowerDiagram)
                        {
                            foreach(var other in layout.Programs.Where(p=>p.Id!=program.Id))
                            {
                                double x=other.Anchor.X-program.Anchor.X,y=other.Anchor.Y-program.Anchor.Y;
                                double limit=x*x+y*y+program.Influence-other.Influence;
                                polygon=Clip(polygon,p=>limit-2*(x*(p.X-program.Anchor.X)+y*(p.Y-program.Anchor.Y)),budget);
                            }
                        }
                        else if(layout.Strategy==WorldLocalLayoutStrategy.BinaryPartition)
                        {
                            foreach(var (cut,positive) in partitionPaths![program.Id])
                            {
                                double angle=cut.NormalDegrees*Math.PI/180,nx=Math.Cos(angle),ny=Math.Sin(angle),sign=positive?1:-1;
                                polygon=Clip(polygon,p=>sign*(nx*(p.X-cut.Origin.X)+ny*(p.Y-cut.Origin.Y)),budget);
                            }
                        }
                        else
                        {
                            var grid=layout.Grid!;double x=program.Column!.Value*grid.CellWidth,y=program.Row!.Value*grid.CellHeight;
                            polygon=Clip(polygon,p=>Local(grid,p).X-x,budget);polygon=Clip(polygon,p=>x+grid.CellWidth-Local(grid,p).X,budget);
                            polygon=Clip(polygon,p=>Local(grid,p).Y-y,budget);polygon=Clip(polygon,p=>y+grid.CellHeight-Local(grid,p).Y,budget);
                        }
                        double area=Area(polygon);if(area==0)continue;
                        if(area<=1e-8)throw new ArgumentException("WORLD_LAYOUT_GEOMETRY_RESOLUTION");
                        if(++count>definition.MaximumCells)throw new ArgumentException("WORLD_LAYOUT_CELL_BUDGET");
                        var cell=new WorldPolygon(Array.AsReadOnly(polygon.ToArray()));cell.Validate();cells.Add(cell);
                    }
                    if(cells.Count==0)throw new ArgumentException("WORLD_LAYOUT_PROGRAM_EMPTY:"+program.Id);
                    var seed=new WorldRegionSeed(program.Id,program.Purpose,program.SourceRuleId,program.Anchor,program.MinimumArea);
                    var shape=Assemble(environment,profile,cells,[seed],budget,token,true,program.AllowDisconnected).Single();
                    allocated+=shape.Area;selected.Add(new(program.Id,layout.Id,layout.RegionId,program,shape));CheckInterfaces(selected,program.Id);
                }
                // Source and numerical faults remain hard. Only a caller's declared
                // area search may reconsider a later geometric constraint failure.
                Measure(allocated);result=Compose(ordinal+1,selected,count);
            }
            result.Areas.Insert(0,Measure(allocated));return result;
        }
        return Compose(0,[],0);
    }
    private static bool LocalConstraintFailure(ArgumentException error)=>WorldPopulationGeometry.AreaSemanticFailure(error)||
        new[]{"WORLD_LAYOUT_INTERFACE_SPAN_UNSATISFIED:","WORLD_LAYOUT_ANCHOR_OUTSIDE_PARENT:","WORLD_LAYOUT_PROGRAM_EMPTY:",
            "WORLD_REGION_MINIMUM_AREA:","WORLD_REGION_SEED_COMPONENT_AMBIGUOUS:","WORLD_LAYOUT_DISCONNECTED_ALLOCATION"}
        .Any(code=>error.Message.StartsWith(code,StringComparison.Ordinal));

    internal static IReadOnlyList<WorldRegionEdge> SharedBoundary(WorldRegion a,WorldRegion b,WorldSpatialWorkBudget budget)
    {
        var segments=new List<WorldRegionEdge>();
        foreach(var first in a.BoundaryEdges)foreach(var second in b.BoundaryEdges)
        {
            budget.Charge();if(Shared(first,second) is {} overlap)
            {
                if(segments.Count>=32768)throw new ArgumentException("WORLD_LAYOUT_INTERFACE_SEGMENT_BUDGET");segments.Add(Canonical(overlap));
            }
        }
        return Merge(segments,budget);
    }

    private static WorldLocalInterface Interface(IReadOnlyList<WorldLocalAllocation> allocations,WorldLocalInterfaceRequirement requirement,WorldSpatialWorkBudget budget)
    {
        var a=allocations.Single(x=>x.Id==requirement.FromId);var b=allocations.Single(x=>x.Id==requirement.ToId);
        var merged=SharedBoundary(a.Shape,b.Shape,budget);var selected=merged.OrderByDescending(e=>e.From.DistanceTo(e.To)).ThenBy(e=>e.From.X).ThenBy(e=>e.From.Y).FirstOrDefault();
        if(selected is null||selected.From.DistanceTo(selected.To)<requirement.MinimumSpan)throw new ArgumentException("WORLD_LAYOUT_INTERFACE_SPAN_UNSATISFIED:"+requirement.Id);
        return new(requirement.Id,requirement,merged,selected,new((selected.From.X+selected.To.X)/2,(selected.From.Y+selected.To.Y)/2));
    }
}
