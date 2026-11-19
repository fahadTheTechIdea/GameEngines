using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalMixedCapacityTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Regional mixed capacity: "+reason);}
    private static WorldPolygon Box(double half)=>new([new(-half,-5),new(half,-5),new(half,5),new(-half,5)]);
    internal static MetadataRegionalOwnershipRequest Example()
    {
        var source=WorldRegionalOwnershipTests.DemandExample();
        return source with{Id="regional-mixed-capacity",Regions=source.Regions.Select(r=>{
            var original=r.Catalog.Archetypes.Single(a=>a.Id=="compound");
            var compact=original with{Capacities=[new("function","units",7,"Compact authored function","mixed:compact")]};
            var wide=compact with{Id="compound-wide",Capacities=[new("function","units",10,"Wide authored function","mixed:wide")],
                PhysicalFootprint=compact.PhysicalFootprint! with{Polygon=Box(9)},
                Access=compact.Access! with{Ports=compact.Access.Ports.Select(p=>p with{BoundaryPosition=new(p.Id=="east"?9:-9,0),ApproachPosition=new(p.Id=="east"?10:-10,0)}).ToArray()}};
            var catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a.Id=="compound"?compact:a.Id!="root"?a:a with{Parts=a.Parts.Select(p=>p.Id!="compounds"?p:p with{
                Capacity=new("function",false,7,"units","Required regional function","mixed:capacity"),
                Variation=new("region-options",r.Id=="beta"?"2":"0","Select authored source variant","mixed:variation",["compound","compound-wide"])}).ToArray()}).Append(wide).ToArray()};
            var program=r.Recipe.Placement!.Programs!.Single();
            return r with{Catalog=catalog,Recipe=r.Recipe with{Placement=r.Recipe.Placement with{Programs=[program with{MinimumParents=0},program with{Id="wide-modules",ParentArchetypeId="compound-wide",MinimumParents=0}]}}};
        }).ToArray()};
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var mixed=WorldMixedVariantCapacityTests.Example();
        var unequal=mixed.Catalog with{Archetypes=mixed.Catalog.Archetypes.Select(a=>a.Id!="camp-small"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{
            Polygon=new WorldPolygon(a.PhysicalFootprint!.Polygon.Vertices.Select(p=>new WorldPoint(p.X*.8,p.Y*.8)).ToArray())}}).ToArray()};
        var generator=new WorldMetadataGenerator();var analysis=generator.AnalyzeProgram(mixed.Recipe with{Origin=null},unequal);
        var measured=new MetadataPlacementPlanner().RegionSources(mixed.Recipe.Placement!,analysis.Nodes,"meters",analysis.FootprintDefinitions,analysis.StructureDefinitions,null,new(1000000,default),default);
        var envelopes=MetadataRegionPlanner.MeasureEnvelopes(measured.Select(m=>m.Source),new(1000000,default));
        Check(analysis.CapacityPlan!.Allocations.Single().Contributions.Count==2&&envelopes.Count==3,"all three individually selected sources contribute to regional demand");
        var physical=generator.Generate(mixed.Recipe,unequal);var actual=physical.PlacementPlan!.Footprints.ToDictionary(f=>f.NodeId);
        foreach(var m in measured)Check(Json(actual[m.Source.NodeId].Polygon)==Json(m.Source.Polygon),"unplaced demand and physical placement use each identical source polygon");
        Check(envelopes.Select(e=>Math.Round(e.EnvelopeArea,6)).Distinct().Count()==2,"different authored variant areas remain distinct");
        await File.WriteAllTextAsync(Path.Combine(output,"mixed-recipe.json"),Json(mixed.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"mixed-catalog.json"),Json(unequal));
        await File.WriteAllTextAsync(Path.Combine(output,"mixed-analysis.json"),Json(analysis));await File.WriteAllTextAsync(Path.Combine(output,"mixed-world.json"),Json(physical));
        var source=Example();await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));
        var planner=new MetadataRegionalOwnershipPlanner();var definition=planner.Plan(source);var demands=definition.OwnershipPlan!.ProgramDemands!;
        foreach(var demand in demands)
        {
            bool wide=demand.RegionId=="beta";double area=wide?180:120,capacity=wide?10:7;
            Check(demand.Analysis.Nodes.Count==5&&demand.Sources.Count==1&&Math.Abs(demand.EnvelopeArea-area)<1e-8&&Math.Abs(demand.RequiredArea-2*area)<1e-8,"regional demand uses the complete chosen variant envelope");
            var allocation=demand.Analysis.CapacityPlan!.Allocations.Single();Check(allocation.Count==1&&allocation.ProvidedQuantity==capacity&&allocation.Contributions.Single().ArchetypeId==(wide?"compound-wide":"compound"),"selected source capacity is exact before allocation");
            double expected=8000*(wide?360d:240d)/840d;
            Check(Math.Abs(Math.Abs(definition.Regions.Single(r=>r.Id==demand.RegionId).Ownership.SignedArea())-expected)<1e-6,"ownership responds to actual varied source demand");
        }
        var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(definition);
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==15&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==9&&world.Connections.Count==2&&world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"all nested members, physical sources and bidirectional journeys survive");
        await engine.PublishAsync(world,Path.Combine(output,"packet"));Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"packet/world-regions.json")))==Json(world),"full regional source replay");
        await File.WriteAllTextAsync(Path.Combine(output,"summary.json"),Json(new{Regions=3,Nodes=15,PhysicalSources=9,BidirectionalConnections=2,EnvelopeAreas=new[]{120,180,120},RequiredAreas=new[]{240,360,240}}));
        Console.WriteLine("PASS: regional ownership measures actual varied capacity sources, retains complete physical programs and routes, and replays the saved world.");
    }
}
