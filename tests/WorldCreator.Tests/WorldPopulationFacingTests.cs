using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static class WorldPopulationFacingTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool v,string message){if(!v)throw new Exception("Population facing: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static WorldMetadataCatalog Directional(WorldMetadataCatalog catalog,bool futureOnly=false)=>catalog with{Archetypes=[..catalog.Archetypes.Select(a=>a with{
        Parts=a.Parts.Select(p=>p.ArchetypeId=="marker"&&(!futureOnly||p.Id=="markers")?p with{ArchetypeId="directional-marker"}:p).ToArray()}),catalog.Archetypes.Single(a=>a.Id=="marker") with{Id="directional-marker",
        PhysicalFootprint=new("meters","Exact asymmetric directional source","facing:source",Box(-.3,-.1,.6,.2),Box(-.35,-.15,.7,.3)),
        Access=new(MetadataOccupancyKind.Solid,[new("front","Positive X entrance","facing:front",new(.3,0),new(.8,0),.15)])}]};
    private static WorldMetadataRecipe Change(WorldMetadataRecipe recipe,Func<MetadataPlacementBinding,MetadataPlacementBinding> change)=>recipe with{Placement=recipe.Placement! with{
        Bindings=recipe.Placement.Bindings.Select(change).ToArray(),Programs=recipe.Placement.Programs?.Select(p=>p with{Bindings=p.Bindings.Select(b=>
            b.Population is null?b:b with{Population=b.Population with{Facing=MetadataPopulationFacing.Outside}}).ToArray()}).ToArray()}};
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Authored(bool clockwise=false,double offset=0)
    {
        var e=WorldPopulationTests.Example();return(Change(e.Recipe,b=>b.DemandId=="marker"?b with{RotationDegrees=offset,Population=b.Population! with{Facing=MetadataPopulationFacing.Along}}:
            b.DemandId=="border"?b with{Population=b.Population! with{Facing=MetadataPopulationFacing.Inside,Boundary=clockwise?b.Population.Boundary! with{
                Boundary=new(b.Population.Boundary!.Boundary.Vertices.Reverse().ToArray())}:b.Population.Boundary}}:b),Directional(e.Catalog));
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Boundary(string name)
    {
        var e=name is "nested" or "translated"?WorldGeneratedBoundaryTests.Nested(2,name=="translated"?3:0):
            name.StartsWith("reservation",StringComparison.Ordinal)?WorldGeneratedBoundaryTests.Root(MetadataBoundarySource.Reservation):
            WorldGeneratedBoundaryTests.Root(name=="split"?MetadataBoundarySource.Allocation:MetadataBoundarySource.Region,name.StartsWith("hole",StringComparison.Ordinal),name=="split");
        return(Change(e.Recipe,b=>b.Population?.GeneratedBoundary is not {} p?b:b with{Population=b.Population with{
            Facing=name is "reservation-tangent" or "hole-tangent"?MetadataPopulationFacing.Along:p.Side==WorldBoundarySide.Inside?MetadataPopulationFacing.Inside:MetadataPopulationFacing.Outside}}),Directional(e.Catalog));
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Route(bool reverse=false)
    {
        var e=WorldRoutePopulationTests.Example();return(Change(e.Recipe,b=>b.DemandId!="marker"?b:b with{Population=b.Population! with{
            Facing=MetadataPopulationFacing.Along,Route=b.Population.Route! with{Reverse=reverse,NormalOffset=reverse?2:-2}}}),Directional(e.Catalog,true));
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Facing evidence exists");Directory.CreateDirectory(output);Geometry();
        var generator=new WorldMetadataGenerator();var worlds=new Dictionary<string,WorldMetadataDocument>();var facts=new List<object>();
        var variants=new[]{("authored",Authored()),("offset",Authored(offset:90)),("clockwise",Authored(true)),
            ("nested",Boundary("nested")),("translated",Boundary("translated")),("reservation-tangent",Boundary("reservation-tangent")),("region",Boundary("region")),
            ("hole-tangent",Boundary("hole-tangent")),("split",Boundary("split")),("route",Route()),("reverse-route",Route(true))};
        foreach(var (name,e) in variants)
        {
            var world=generator.Generate(e.Recipe,e.Catalog);worlds.Add(name,world);var plan=world.PlacementPlan!;var traces=plan.PopulationOrientations!;
            int count=name is "nested" or "translated"?8:name is "authored" or "offset" or "clockwise"?8:4;
            Check(traces.Sum(t=>t.NodeIds.Count)==count,"all required headings retained");
            var bindings=plan.Definition.Bindings.Concat((plan.Definition.NestedStages??[]).SelectMany(s=>s.Bindings)).ToDictionary(b=>b.NodeId);
            var local=plan.Footprints.Concat(plan.NestedStages.SelectMany(s=>s.Footprints)).ToDictionary(f=>f.NodeId);
            foreach(var t in traces)
            for(int i=0;i<t.NodeIds.Count;i++)
            {
                string id=t.NodeIds[i];double rotation=t.Rotations[i],r=rotation*Math.PI/180,c=Math.Cos(r),s=Math.Sin(r);
                var n=world.Nodes.Single(n=>n.Id==id);var site=world.AccessPlan!.Sites.Single(s=>s.NodeId==id);var source=e.Catalog.Archetypes.Single(a=>a.Id==n.ArchetypeId).PhysicalFootprint!;
                WorldPoint Rotate(WorldPoint p)=>new(p.X*c-p.Y*s,p.X*s+p.Y*c);
                Check(bindings[id].RotationDegrees==rotation,"effective binding owns exact per-member rotation");
                for(int k=0;k<source.Polygon.Vertices.Count;k++)Check(local[id].Polygon.Vertices[k].DistanceTo(Rotate(source.Polygon.Vertices[k]))<1e-10,"exact asymmetric source polygon rotates");
                for(int k=0;k<source.Reservation!.Vertices.Count;k++)Check(local[id].Reservation!.Vertices[k].DistanceTo(Rotate(source.Reservation.Vertices[k]))<1e-10,"clearance rotates with source");
                var front=site.Ports.Single(p=>p.Id=="front");var expected=Rotate(new(.3,0));var approach=Rotate(new(.8,0));
                Check(front.BoundaryPosition.DistanceTo(new(n.Position![0]+expected.X,n.Position[1]+expected.Y))<1e-10&&
                    front.ApproachPosition.DistanceTo(new(n.Position[0]+approach.X,n.Position[1]+approach.Y))<1e-10,"native access geometry follows facing");
            }
            if(plan.Definition.SourceWorld is {} sourceWorld)
            foreach(var site in sourceWorld.AccessPlan!.Sites)Check(Json(site)==Json(world.AccessPlan!.Sites.Single(s=>s.NodeId==site.NodeId)),"accepted physical source stays exact");
            var changed=traces[0] with{Rotations=traces[0].Rotations.Select((r,i)=>i==0?r+1:r).ToArray()};
            Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=plan with{PopulationOrientations=[changed,..traces.Skip(1)]}}),"PLAN_MISMATCH");
            Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=plan with{PopulationOrientations=null}}),"PLAN_MISMATCH");
            string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            var saved=await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"world"));
            Check(Json(saved)==Json(world),"actual file generation/publication equals API");
            facts.Add(new{Name=name,Nodes=world.Nodes.Count,Physical=world.AccessPlan!.Sites.Count,FacingMembers=count,Traces=traces,Work=plan.Work});
            Console.WriteLine("Qualified population facing: "+name+"; "+count+" members");
        }
        Check(Json(worlds["nested"].PlacementPlan!.PopulationOrientations)==Json(worlds["translated"].PlacementPlan!.PopulationOrientations),"translation cannot change headings");
        var a=Authored();var offsets=worlds["authored"].PlacementPlan!.PopulationOrientations!;
        var impossible=Boundary("reservation");Reject(()=>generator.Generate(impossible.Recipe,impossible.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        string refused=Path.Combine(output,"negative-reservation");Directory.CreateDirectory(refused);
        await File.WriteAllTextAsync(Path.Combine(refused,"recipe.json"),Json(impossible.Recipe));await File.WriteAllTextAsync(Path.Combine(refused,"catalog.json"),Json(impossible.Catalog));
        var blocked=Boundary("hole");Reject(()=>generator.Generate(blocked.Recipe,blocked.Catalog),"METADATA_ACCESS_APPROACH_BLOCKED");
        string blockedFolder=Path.Combine(output,"negative-hole");Directory.CreateDirectory(blockedFolder);
        await File.WriteAllTextAsync(Path.Combine(blockedFolder,"recipe.json"),Json(blocked.Recipe));await File.WriteAllTextAsync(Path.Combine(blockedFolder,"catalog.json"),Json(blocked.Catalog));
        Check(offsets.Single(t=>t.DemandId=="marker").TangentDegrees.SequenceEqual(new double[]{0,0,0,90}),"exact bend uses incoming segment");
        foreach(var facing in Enum.GetValues<MetadataPopulationFacing>())
        {
            var input=Change(a.Recipe,b=>b.DemandId=="border"?b with{Population=b.Population! with{Facing=facing}}:b);
            var world=generator.Generate(input,a.Catalog);var t=world.PlacementPlan!.PopulationOrientations!.Single(t=>t.DemandId=="border");
            double offset=facing switch{MetadataPopulationFacing.Along=>0,MetadataPopulationFacing.Against=>180,MetadataPopulationFacing.Left=>90,MetadataPopulationFacing.Right=>-90,MetadataPopulationFacing.Inside=>90,_=>270};
            for(int i=0;i<t.Rotations.Count;i++)Check(Math.Cos((t.Rotations[i]-t.TangentDegrees[i]-offset)*Math.PI/180)>1-1e-12,"all facing modes follow exact tangent");
        }
        Reject(()=>generator.Generate(Change(a.Recipe,b=>b.DemandId=="marker"?b with{Population=b.Population! with{Facing=MetadataPopulationFacing.Inside}}:b),a.Catalog),"METADATA_POPULATION_FACING_INVALID");
        Reject(()=>generator.Generate(Change(a.Recipe,b=>b.DemandId=="resource"?b with{Population=b.Population! with{Facing=MetadataPopulationFacing.Along}}:b),a.Catalog),"METADATA_POPULATION_FACING_INVALID");
        Reject(()=>generator.Generate(Change(a.Recipe,b=>b.DemandId=="marker"?b with{RotationDegrees=361}:b),a.Catalog),"METADATA_PLACEMENT_ROTATION_INVALID");
        Reject(()=>generator.Generate(Change(a.Recipe,b=>b.DemandId=="marker"?b with{Population=b.Population! with{Facing=(MetadataPopulationFacing)99}}:b),a.Catalog),"METADATA_POPULATION_FACING_INVALID");
        Reject(()=>generator.Generate(Change(a.Recipe,b=>b.DemandId=="marker"?b with{Population=b.Population! with{Path=null,Region=new("home")}}:b),a.Catalog),"METADATA_POPULATION_FACING_INVALID");
        var routeInput=Route();Reject(()=>generator.Generate(Change(routeInput.Recipe,b=>b.DemandId=="gate"?b with{Population=new("Cannot reorient accepted source","facing:source-protection",
            Path:new(new([new(5,10),new(45,10)]),40),Facing:MetadataPopulationFacing.Along)}:b),routeInput.Catalog),"METADATA_POPULATION_SOURCE_FACING_CONFLICT");
        var bad=Change(a.Recipe,b=>b.DemandId=="marker"?b with{Population=b.Population! with{Path=b.Population.Path! with{Spacing=100}}}:b);
        var session=new MetadataGenerationSession(generator,a.Recipe,a.Catalog);var before=session.Snapshot;
        var edited=session.Apply(new("face-edit","Change source-facing offset","facing:edit",before.World.RecipeSha256,before.World.CatalogSha256,Authored(offset:90).Recipe,a.Catalog));
        Check(Json(edited.World)==Json(worlds["offset"]),"facing edit equals clean generation");
        try{session.Apply(new("bad","Refuse unavailable positions","facing:bad",edited.World.RecipeSha256,edited.World.CatalogSha256,bad,a.Catalog));throw new Exception("Expected refusal");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(edited),"failed edit preserves accepted state");
        using(var cancelled=new CancellationTokenSource())
        {cancelled.Cancel();try{session.Apply(new("cancel","Retain accepted state","facing:cancel",edited.World.RecipeSha256,edited.World.CatalogSha256,a.Recipe,a.Catalog),cancelled.Token);throw new Exception("Expected cancellation");}catch(OperationCanceledException){}Check(Json(session.Snapshot)==Json(edited),"cancelled edit preserves accepted state");}
        await session.PublishAsync(Path.Combine(output,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session/generation-session.json"));
        Check(Json(restored.Snapshot)==Json(edited),"complete facing checkpoint/history replays");
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(facts));
        Console.WriteLine("PASS: exact geometry-derived population facing, asymmetric sources/reservations/ports, parent frames, route direction, holes, full replay/tamper and atomic edit history.");
    }
    private static void Geometry()
    {
        var pattern=new WorldPathPattern(new([new(0,0),new(2,0),new(2,3)]),1,NormalOffset:.5);
        var poses=WorldPopulationGeometry.PathPoses(pattern,6);Check(poses.Select(p=>p.TangentDegrees).SequenceEqual(new double[]{0,0,0,90,90,90}),"exact incoming tangent and endpoint");
        Check(Json(poses.Select(p=>p.Position))==Json(WorldPopulationGeometry.Path(pattern,6)),"shared sampling never moves positions");
        Reject(()=>WorldPopulationGeometry.PathPoses(pattern,7),"WORLD_PATTERN_CAPACITY_EXCEEDED");
        Reject(()=>WorldPopulationGeometry.BoundaryPoses(new(Box(0,0,2,2),0,2),5),"WORLD_BOUNDARY_WRAP_FORBIDDEN");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{WorldPopulationGeometry.PathPoses(pattern,6,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
    }
}
