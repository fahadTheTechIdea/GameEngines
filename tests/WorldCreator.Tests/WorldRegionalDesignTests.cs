using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldRegionalDesignTests
{
    static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static ArgumentException Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return e;}throw new Exception("Expected "+code);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var region=template.Regions.Single(r=>r.Id=="arrival");
        var root=region.Catalog.Archetypes.Single(a=>a.Id==region.Recipe.RootArchetypeId);
        var wrong=region.Catalog with{Archetypes=region.Catalog.Archetypes.Select(a=>a.Id==root.Id?a with{Kind="different-role"}:a).ToArray()};
        string physical=region.Catalog.Archetypes.First(a=>a.Access is not null).Kind;
        var request=new MetadataRegionalDesignRequest(1,"example-design","Compose connected, purposeful game regions","test:design",template,
            [new("arrival",[new("wrong-role",0,region.Recipe,wrong),new("correct-role",1,region.Recipe,region.Catalog)])],
            [new("required-role","arrival",root.Kind,1,1),new("physical-member","arrival",physical,1,100,true)],2);
        var engine=new MetadataRegionalDesignGenerator();var before=Json(request);var result=engine.Generate(request);
        Check(Json(request)==before,"Source inputs remain unchanged.");
        Check(result.Receipt.Attempts.Count==2&&!result.Receipt.Attempts[0].Accepted&&result.Receipt.Attempts[0].Counts.Any(c=>!c.Satisfied)&&
            result.Receipt.Attempts[1].Accepted&&result.Receipt.Attempts[1].Selections.Single().OptionId=="correct-role","Chooses the first complete program satisfying declared requirements.");
        Check(result.World.Regions.Count==4&&result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12,"All required complete regions and physical sources retained.");
        Check(result.World.Connections.Count==3&&result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"Terrain-qualified network has full two-way paths.");
        var limited=Reject(()=>engine.Generate(request with{MaximumAttempts=1}),"METADATA_REGIONAL_DESIGN_ATTEMPT_LIMIT");
        Check(limited.Data["RegionalDesignAttempts"] is MetadataRegionalDesignAttempt[]{Length:1},"Attempt limit is not exhaustion and retains observed evidence.");
        var impossible=request with{Slots=[request.Slots[0] with{Options=[request.Slots[0].Options[0]]}]};
        var exhausted=Reject(()=>engine.Generate(impossible),"METADATA_REGIONAL_DESIGN_ALTERNATIVES_EXHAUSTED");
        Check(exhausted.Data["RegionalDesignAttempts"] is MetadataRegionalDesignAttempt[]{Length:1},"Exhaustion retains failed candidate.");
        Reject(()=>engine.Generate(request with{Template=template with{MaximumWork=1}}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>engine.Generate(request with{Requirements=[new("missing-region","absent","role",1,1)]}),"METADATA_REGIONAL_DESIGN_REQUIREMENT_INVALID");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{engine.Generate(request,cancelled.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await engine.PublishAsync(result,Path.Combine(output,"world"));
        var read=await engine.ReadAsync(Path.Combine(output,"world"));Check(Json(read)==Json(result),"Published selection and complete world replay.");
        var altered=result.Receipt with{Attempts=result.Receipt.Attempts.Select((a,i)=>i==0?a with{Accepted=true}:a).ToArray()};
        Reject(()=>engine.Verify(altered,result.World),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        Console.WriteLine("PASS: complete authored-program selection, terrain-qualified regional world, required physical content, attempt/exhaustion distinction, budget, cancellation and source replay.");
    }
}
