using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionalOwnershipTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Ownership: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message+": "+a+" / "+b);
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static MetadataRegionalOwnershipRequest Example(bool movedVoid=false)
    {
        var source=WorldRegionalContentTests.Portable(WorldRegionalContentTests.Example());var template=source.Regions[0];
        var recipe=template.Recipe with{Origin=null,Placement=template.Recipe.Placement! with{Request=template.Recipe.Placement.Request with{Boundary=null,Anchors=[]}}};
        var ids=new[]{"alpha","beta","gamma"};var profile=recipe.Placement!.Request.Demands[0].Profile;
        var regions=ids.Select(id=>new MetadataRegionalProgramRegion(id,"Complete "+id+" region","ownership:"+id,1,100,8,profile,"fixed",recipe,template.Catalog)).ToArray();
        var surface=source.Surface with{Exclusions=[new("protected-void","Keep source void excluded from play","ownership:void",movedVoid?Box(39,3,12,24):Box(44,28,12,24))]};
        var connections=Enumerable.Range(0,2).Select(i=>source.Connections[0] with{Id="journey-"+i,From=new(ids[i],"region-west/compounds-0","east"),
            To=new(ids[i+1],"region-west/compounds-0","west"),OwnerRegionId=ids[i],RoutingDomain=surface.Boundary!}).ToArray();
        return new(1,"derived-world","Complete region programs without authored ownership or root coordinates","ownership:world",surface,regions,connections,
            new(Interior:new(.1,65536),PartitionSearch:new([0,90,45])));
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var planner=new MetadataRegionalOwnershipPlanner();var input=Example();string original=Json(input);var definition=planner.Plan(input);
        Check(Json(input)==original,"planning leaves source templates untouched");
        Near(definition.Regions.Sum(r=>Math.Abs(r.Ownership.SignedArea())),8000,"complete outer ownership includes excluded void");
        foreach(var r in definition.Regions)
        {
            Near(Math.Abs(r.Ownership.SignedArea()),8000d/3,"equal weighted ownership");Check(Json(r.Ownership)==Json(r.GenerationDomain),"domain derives from ownership");
            var source=definition.OwnershipPlan!.Source.Regions.Single(p=>p.Id==r.Id);Check(source.Recipe.Origin is null&&source.Recipe.Placement!.Request.Boundary is null&&source.Recipe.Placement.Request.Anchors.Count==0,"saved source contains no fabricated coordinates");
            var anchor=definition.OwnershipPlan.Anchors.Single(a=>a.RegionId==r.Id).Plan.Regions.Single().Seed.Position;
            Check(r.Recipe.Origin!.SequenceEqual(new[]{anchor.X,anchor.Y})&&r.Recipe.Placement!.Request.Anchors.Single().Position==anchor,"origin and root anchor bind actual terrain-qualified interior");
            Check(!input.Surface.Exclusions[0].Boundary.ContainsPoint(anchor),"ownership does not turn excluded ground into a playable anchor");
        }
        Check(Json(definition)==Json(planner.Plan(input with{Regions=input.Regions.Reverse().ToArray(),Connections=input.Connections.Reverse().ToArray()})),"source order canonical");
        var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(definition);
        Check(world.Regions.Sum(r=>r.World.Nodes.Count)==15&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==9&&world.Connections.Count==2&&world.Connections.All(c=>c.Reverse is not null),"all complete programs and actual outward/return journeys retained");
        var reversed=definition.Regions.Reverse().Select(r=>engine.GenerateRegion(definition,r.Id)).ToArray();Check(Json(engine.Assemble(definition,reversed))==Json(world),"independent reverse-order requests equal whole generation");
        await engine.PublishAsync(world,Path.Combine(output,"world"));Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"saved world replays ownership derivation and every packet");
        var moved=planner.Plan(Example(true));var movedWorld=engine.Generate(moved);
        Check(Json(moved.Regions.Select(r=>r.Ownership))==Json(definition.Regions.Select(r=>r.Ownership)),"terrain exclusion changes no computational ownership mask");
        Check(Json(moved.Regions.Single(r=>r.Id=="beta").Recipe.Origin)!=Json(definition.Regions.Single(r=>r.Id=="beta").Recipe.Origin),"actual terrain exclusion revises the program anchor");
        Check(movedWorld.Regions.Sum(r=>r.World.Nodes.Count)==15,"terrain response retains complete content");
        await engine.PublishAsync(movedWorld,Path.Combine(output,"moved"));
        var weighted=planner.Plan(input with{Regions=input.Regions.Select(r=>r.Id=="beta"?r with{Weight=2}:r).ToArray()});
        foreach(var r in weighted.Regions)Near(Math.Abs(r.Ownership.SignedArea()),r.Id=="beta"?4000:2000,"weighted macro allocation");
        var weightedWorld=engine.Generate(weighted);Check(weightedWorld.Regions.Sum(r=>r.World.Nodes.Count)==15,"weight edit preserves all required nested programs");
        await engine.PublishAsync(weightedWorld,Path.Combine(output,"weighted"));
        await SessionAsync(output,definition,weighted);
        await BoundariesAsync(output,planner,input,definition,engine);
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(input));await File.WriteAllTextAsync(Path.Combine(output,"moved-request.json"),Json(Example(true)));
        await File.WriteAllTextAsync(Path.Combine(output,"weighted-request.json"),Json(weighted.OwnershipPlan!.Source));await File.WriteAllTextAsync(Path.Combine(output,"definition.json"),Json(definition));
        Console.WriteLine("PASS: derived full-domain ownership, terrain-qualified anchors without authored origins/domains, complete15-node/9-source world and return journeys, independent request equality, terrain/weight response, source replay and bounds.");
    }
}
