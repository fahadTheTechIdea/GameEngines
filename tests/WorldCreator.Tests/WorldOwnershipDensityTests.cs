using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldOwnershipDensityTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool condition,string message){if(!condition)throw new Exception("Density ownership: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<=Math.Max(1,Math.Abs(b))*1e-8,message+": "+a+" / "+b);
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing density refusal: "+code);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static MetadataRegionalOwnershipRequest Example(string kind="affine",bool larger=false,int sites=2,int plants=4)
    {
        var input=WorldPortablePopulationTests.Example(larger:larger,sites:sites,plants:plants);
        WorldEnvironmentalField density=new("demand","Game-authored spatial demand","density:field","relative",1,[new(WorldFieldInput.X,1d/336)]);
        WorldEnvironmentDefinition environment=new(1,"ownership-demand",[density]);
        if(kind=="reversed")density=density with{Offset=2,Terms=[new(WorldFieldInput.X,-1d/336)]};
        if(kind=="clamped")density=density with{Maximum=1.5};
        if(kind is "elevation" or "piecewise")density=density with{Terms=[new(WorldFieldInput.Elevation,.2)]};
        environment=environment with{Fields=[density]};
        if(kind=="derived")environment=environment with{Fields=[density with{Id="base",Maximum=1.5},density with{Offset=0,Terms=[new(WorldFieldInput.Field,1,"base")]}]};
        if(kind=="piecewise")input=input with{Surface=input.Surface with{Patches=[..input.Surface.Patches,
            new("raised-east","ground","Actual continuous higher-gradient terrain","density:terrain",Box(168,0,168,224),1,new(168,0),.341,.004,0)]}};
        if(kind is "terrain" or "sampled")
        {
            input=input with{Surface=input.Surface with{Patches=[],Terrain=new(1,"generated-terrain","ground","Original plane through declared terrain recipe","density:terrain","meters",new(0,0),336,224,2,2,.005,.002,0,[])}};
            if(kind=="terrain")environment=environment with{Fields=[density with{Terms=[new(WorldFieldInput.Elevation,.2)]}]};
            else environment=environment with{Fields=[density with{Offset=0,Terms=[new(WorldFieldInput.SampledField,1,"authored-demand")]}],
                SampledFields=[new(1,"authored-demand","Explicit game-authored scalar demand","density:sampled","relative",new(0,0),336,224,2,2,[1,2,1,2])]};
        }
        return input with{Pattern=input.Pattern with{DensityFieldId="demand"},OwnershipEnvironment=environment};
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");Directory.CreateDirectory(output);
        var planner=new MetadataRegionalOwnershipPlanner();var input=Example();string before=Json(input);
        var definition=planner.Plan(input);Check(Json(input)==before,"input immutable");Measure(definition,75264*1.5);
        Check(Json(definition)==Json(planner.Plan(input with{Regions=input.Regions.Reverse().ToArray(),Connections=input.Connections.Reverse().ToArray()})),"canonical declaration order");
        var uniform=planner.Plan(WorldPortablePopulationTests.Example());
        Check(Json(definition.Regions.Select(r=>r.Ownership))!=Json(uniform.Regions.Select(r=>r.Ownership)),"density changes actual ownership, not just a receipt");
        // Original source terrain is z=.005+.002*x. The east overlay is
        // continuous at168m and adds .002*max(0,x-168) to that plane.
        foreach(var (kind,average) in new[]{("reversed",1.5),("clamped",1.375),("derived",1.375),("elevation",1.0682),("piecewise",1.085),("terrain",1.0682),("sampled",1.5)})
        {
            var planned=planner.Plan(Example(kind));Measure(planned,75264*average);
            await File.WriteAllTextAsync(Path.Combine(output,kind+"-definition.json"),Json(planned));
        }
        var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(definition);Complete(world,2,4,2900);
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(input));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"saved native replay");
        var packets=definition.Regions.Reverse().Select(r=>engine.GenerateRegion(definition,r.Id)).ToArray();
        Check(Json(engine.Assemble(definition,packets))==Json(world),"independent reverse-order region generation");
        foreach(var (name,request,sites,plants,area) in new[]{("reversed",Example("reversed"),2,4,2900d),("piecewise",Example("piecewise"),2,4,2900d),("source-size",Example(larger:true),2,4,3200d),("more",Example(sites:3,plants:6),3,6,2900d)})
        {
            var changed=planner.Plan(request);var actual=engine.Generate(changed);Complete(actual,sites,plants,area);
            Check(Json(actual.Regions.Select(r=>r.World.AccessPlan!.Sites))!=Json(world.Regions.Select(r=>r.World.AccessPlan!.Sites)),"variant changes actual complete physical content: "+name);
            await engine.PublishAsync(actual,Path.Combine(output,name));await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(request));
        }
        await Lifecycle(output,definition);Refusals(planner,input,definition);
        Console.WriteLine("PASS: actual-terrain density ownership, analytical affine/clamped/dependent/piecewise mass, complete48/87-node programs, original sources/open ground/journeys, independent generation/replay/history and hard refusals.");
    }
    private static void Measure(MetadataRegionalWorldDefinition definition,double mass)
    {
        var p=definition.OwnershipPlan!;Near(p.Partition.Area,75264,"full outer area includes original void");Near(p.Partition.Mass,mass,"analytical integrated density");
        var weights=p.Partition.RelativeWeights!;double total=weights.Sum();
        for(int i=0;i<p.Partition.Strata.Count;i++)
        {
            var s=p.Partition.Strata[i];Near(s.Mass,mass*weights[i]/total,"weighted mass share");Near(s.Area,Math.Abs(definition.Regions[i].Ownership.SignedArea()),"owned geometric area");
            Check(s.Area>=p.ProgramDemands![i].RequiredArea,"original source capacity stays geometric");
        }
    }
    private static void Complete(MetadataRegionalWorldDocument world,int sites,int plants,double compoundArea)
    {
        int per=2+sites*(plants+3);Check(world.Regions.Sum(r=>r.World.Nodes.Count)==3*per&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==3*(per-1),"no missing complete members");
        Near(Math.Abs(world.Definition.Surface.Exclusions.Single().Boundary.SignedArea()),560,"original protected void retained");
        Near(world.Regions.Sum(r=>r.World.GroundCoverPlan!.ExcludedArea),560,"actual regional cover accounts for every excluded square meter");
        Check(world.Connections.Count==2&&world.Connections.All(r=>r.Reverse is not null&&r.Definition.Profile.Radius==.2),"original actor and regional round trips");
        foreach(var packet in world.Regions)
        {
            var w=packet.World;var source=w.PlacementPlan!.Definition.SourceWorld!;
            Check(Json(w.RoutePlan!.Routes)==Json(source.RoutePlan!.Routes)&&w.RoutePlan.Routes.Sum(r=>1+(r.Reverse is null?0:1))==2*(sites-1),"every original directed local path retained");
            Check(w.PlacementPlan.PopulationDomains!.Count==2*sites&&w.PlacementPlan.SiteFeatures!.Count==4*sites,"all source-derived surroundings and content domains");
            var demand=world.Definition.OwnershipPlan!.ProgramDemands!.Single(d=>d.RegionId==packet.RegionId);Near(demand.EnvelopeArea,compoundArea,"actual complete envelope");
            Check(w.GroundCoverPlan!.UnresolvedArea==0&&new MetadataPhysicalAccountingPlanner().Measure(w).GroundUses!.Single().MinimumSatisfied,"required85% open ground and complete material scope");
            Check(Json(w.Surface)==Json(source.Surface)&&source.Nodes.All(n=>Json(n)==Json(w.Nodes.Single(m=>m.Id==n.Id))),"actual source terrain/geometry untouched");
        }
    }
}
