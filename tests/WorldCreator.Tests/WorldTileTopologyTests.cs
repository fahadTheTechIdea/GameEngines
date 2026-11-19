using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;
using WorldCreator.Godot;

internal static class WorldTileTopologyTests
{
    static void Check(bool value,string message){if(!value)throw new Exception("Tile topology: "+message);}
    static async Task Reject(Func<Task> action,string code)
    {try{await action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    public static async Task RunAsync(string sourceFile,string newDirectory)
    {
        if(Directory.Exists(newDirectory)||File.Exists(newDirectory))throw new IOException("Test output exists");
        string folder=Path.GetFullPath(newDirectory);Directory.CreateDirectory(folder);
        var world=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(sourceFile);
        string before=WorldTileTopologyPlanner.Digest(world);
        var request=new WorldTileTopologyRequest(1,before,new(0,0),.5,.5,.36);
        var owner=new WorldTileTopologyPlanner();var plan=owner.Plan(world,request);
        Check(plan.Channels.Count==8&&plan.Channels.Sum(c=>c.Cells.Count)>35000,"complete surface, passage and approach masks at half-meter resolution");
        Check(plan.Channels.Count(c=>c.Source.Kind==WorldTileChannelKind.Access)==2&&plan.Channels.Count(c=>c.Source.Kind==WorldTileChannelKind.Passage)==4,"one channel per passage and each real entrance");
        Check(plan.Channels.Where(c=>c.Source.Kind is WorldTileChannelKind.Passage or WorldTileChannelKind.Access).All(c=>c.Components==1),"every required individual passage/approach mask is face-connected");
        Check(plan.ErrorBandMeters==Math.Sqrt(.5)/2&&before==WorldTileTopologyPlanner.Digest(world),"declared error band; original world unchanged");
        owner.Validate(world,plan);
        await Reject(()=>Task.Run(()=>owner.Validate(world,plan with{Status="Approved"})),"PLAN_REPLAY_MISMATCH");
        await Reject(()=>Task.Run(()=>owner.Plan(world,request with{WorldSha256=new string('0',64)})),"REQUEST_INVALID_OR_STALE");
        await Reject(()=>Task.Run(()=>owner.Plan(world,request with{MaximumErrorBandMeters=.1})),"RESOLUTION_EXCEEDS_ERROR_BAND");
        await Reject(()=>Task.Run(()=>owner.Plan(world,request with{CellWidthMeters=4,CellHeightMeters=4,MaximumErrorBandMeters=3})),"TARGET_HAS_NO_CELLS");
        await Reject(()=>Task.Run(()=>owner.Plan(world,request with{MaximumCells=1})),"CANDIDATE_LIMIT");
        await Reject(()=>Task.Run(()=>owner.Plan(world,request with{MaximumWork=1})),"WORK_BUDGET");
        var cts=new CancellationTokenSource();cts.Cancel();
        try{owner.Plan(world,request,token:cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        var groups=plan.Channels.Where(c=>c.Source.Kind==WorldTileChannelKind.Surface).Select((c,i)=>new GodotTerrainTopologyGroup("surface-"+i,[c.Source.Id]))
            .Append(new("passages",plan.Channels.Where(c=>c.Source.Kind!=WorldTileChannelKind.Surface).Select(c=>c.Source.Id).ToArray())).ToArray();
        var spec=new GodotTerrainTopologySpecification(1,WorldTileTopologyPlanner.Digest(plan),groups);
        var exporter=new GodotStructureLayerExporter();
        var packet=await exporter.ExportTopologyProbeAsync(world,plan,spec,Path.Combine(folder,"native"));
        Check(packet.Groups.Count==3&&packet.Groups.Single(g=>g.Id=="passages").ChannelIds.Count==6,"explicit material group merges street/drive/entrance interfaces without deleting source ownership");
        await Reject(()=>exporter.ExportTopologyProbeAsync(world,plan,spec with{Groups=groups.Skip(1).ToArray()},Path.Combine(folder,"refused-coverage")),"CHANNEL_COVERAGE");
        await Reject(()=>exporter.ExportTopologyProbeAsync(world,plan,spec with{Groups=groups.Append(groups[0] with{Id="duplicate"}).ToArray()},Path.Combine(folder,"refused-duplicate")),"CHANNEL_COVERAGE");
        var distant=owner.Plan(world,request with{Origin=new(-20000,0)});
        await Reject(()=>exporter.ExportTopologyProbeAsync(world,distant,spec with{PlanSha256=WorldTileTopologyPlanner.Digest(distant)},Path.Combine(folder,"refused-coordinate")),"SIGNED_COORDINATE_LIMIT");
        try{await exporter.ExportTopologyProbeAsync(world,plan,spec,Path.Combine(folder,"native"));throw new Exception("Overwrite accepted");}catch(IOException){}
        Check(!Directory.Exists(Path.Combine(folder,"refused-coordinate"))&&!Directory.Exists(Path.Combine(folder,"refused-coverage")),"failed exports leave no partial project");
        await File.WriteAllTextAsync(Path.Combine(folder,"world.json"),JsonSerializer.Serialize(world,WorldJson.CreateOptions(true)));
        await File.WriteAllTextAsync(Path.Combine(folder,"request.json"),JsonSerializer.Serialize(request,WorldJson.CreateOptions(true)));
        await File.WriteAllTextAsync(Path.Combine(folder,"plan.json"),JsonSerializer.Serialize(plan,WorldJson.CreateOptions(true)));
        await File.WriteAllTextAsync(Path.Combine(folder,"specification.json"),JsonSerializer.Serialize(spec,WorldJson.CreateOptions(true)));
        Console.WriteLine($"PASS: {plan.Channels.Count} exact-source masks/{plan.Channels.Sum(c=>c.Cells.Count)} selected presentation cells; explicit {plan.ErrorBandMeters:R}m error band, connected passages/entrances, replay/budget/resolution/coverage/signed-coordinate/output refusal. Native probe exported; execute it separately before native claims.");
    }
}
