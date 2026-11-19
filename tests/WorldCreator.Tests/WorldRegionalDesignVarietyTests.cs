using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static class WorldRegionalDesignVarietyTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Reject(Action run,string code){try{run();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/semantic-root-request.json");
        var region=original.Template.Regions.Single(r=>r.Id=="arrival");var alternative=original.Slots.Single().Options.Single(o=>o.Id=="renamed-path");
        var wide=alternative.Catalog with{Archetypes=alternative.Catalog.Archetypes.Select(a=>a.Id!="compound"?a:a with{
            PhysicalFootprint=a.PhysicalFootprint! with{Polygon=new WorldPolygon([new(-7,-5),new(7,-5),new(7,5),new(-7,5)])},
            Access=a.Access! with{Ports=a.Access.Ports.Select(p=>p.Id switch{
                "east"=>p with{BoundaryPosition=new(7,0),ApproachPosition=new(8,0)},
                "west"=>p with{BoundaryPosition=new(-7,0),ApproachPosition=new(-8,0)},_=>p}).ToArray()}}).ToArray()};
        var options=new[]{new MetadataRegionalDesignOption("standard",0,region.Recipe,region.Catalog),
            new MetadataRegionalDesignOption("wide",0,alternative.Recipe,wide)};
        var request=original with{Slots=[new("arrival",options)]};
        string First(string seed)=>MetadataRegionalDesignGenerator.OrderDesignSlots(request.Slots,seed).Single().Options[0].Id;
        var seeds=Enumerable.Range(0,100).Select(i=>"design-"+i).GroupBy(First).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.Ordinal);
        Check(seeds.ContainsKey("standard")&&seeds.ContainsKey("wide"),"Explicit seeds cover two declared variants.");
        var reverse=request.Slots.Select(s=>s with{Options=s.Options.Reverse().ToArray()}).Reverse().ToArray();
        foreach(var seed in seeds.Values)
            Check(Json(MetadataRegionalDesignGenerator.OrderDesignSlots(request.Slots,seed))==Json(MetadataRegionalDesignGenerator.OrderDesignSlots(reverse,seed)),"Seeded ordering ignores input permutation.");
        var differing=options.Select((o,i)=>o with{Priority=i}).ToArray();
        foreach(var seed in seeds.Values)Check(MetadataRegionalDesignGenerator.OrderDesignSlots([new("arrival",differing)],seed)[0].Options[0].Id=="standard","Seed does not override explicit priority.");
        var slots=new[]{new MetadataRegionalDesignSlot("a",[options[0] with{Id="a0"},options[0] with{Id="a1"}]),
            new MetadataRegionalDesignSlot("b",[options[0] with{Id="b0"},options[0] with{Id="b1"},options[0] with{Id="b2"}]),
            new MetadataRegionalDesignSlot("c",[options[0] with{Id="c0"},options[0] with{Id="c1",Priority=1}])};
        var ordered=MetadataRegionalDesignGenerator.OrderDesignSlots(slots,null);
        var choices=MetadataRegionalDesignGenerator.EnumerateDesignChoices(ordered,null,default).Take(7).ToArray();
        Check(choices[0].Select(o=>o.Id).SequenceEqual(new[]{"a0","b0","c0"})&&
            choices.Take(3).Any(c=>c[0].Id=="a1")&&choices.Take(3).Any(c=>c[1].Id=="b1"),"Early attempts explore changes across slots.");
        Check(choices.Take(6).All(c=>c.Sum(o=>o.Priority)==0)&&choices[6].Sum(o=>o.Priority)==1,"Priority is minimized across the entire combination.");
        Reject(()=>new MetadataRegionalDesignGenerator().Generate(request with{SelectionSeed=" "}),"METADATA_REGIONAL_DESIGN_INVALID");
        using(var cts=new CancellationTokenSource()){cts.Cancel();try{MetadataRegionalDesignGenerator.EnumerateDesignChoices(ordered,"s",cts.Token).First();throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        var engine=new MetadataRegionalDesignGenerator();var results=new Dictionary<string,MetadataRegionalDesignResult>();
        foreach(var (variant,seed) in seeds.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            var result=engine.Generate(request with{SelectionSeed=seed});results.Add(variant,result);
            Check(result.Receipt.Attempts.Count==1&&result.Receipt.Attempts[0].Selections.Single().OptionId==variant,"Seed chooses a declared feasible program.");
            Check(result.World.Regions.Count==4&&result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&
                result.World.Connections.Count==3&&result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"All regions, physical sources and two-way connections remain.");
        }
        var standard=results["standard"].World.Regions.Single(r=>r.RegionId=="arrival").World;
        var varied=results["wide"].World.Regions.Single(r=>r.RegionId=="arrival").World;
        Check(Json(standard.AccessPlan!.Sites.Single(s=>s.NodeId=="region-west/compounds-0").Footprint)!=
            Json(varied.AccessPlan!.Sites.Single(s=>s.NodeId=="new-region-program/sites-0").Footprint),"Different seeds produce actually different physical source shapes.");
        var permuted=engine.Generate(request with{SelectionSeed=seeds["wide"],Slots=reverse});
        Check(Json(permuted.World)==Json(results["wide"].World)&&Json(permuted.Receipt.Attempts)==Json(results["wide"].Receipt.Attempts),"Full physical world and choices ignore option declaration order.");
        Directory.CreateDirectory(output);
        foreach(var (variant,seed) in seeds)await File.WriteAllTextAsync(Path.Combine(output,variant+"-request.json"),Json(request with{SelectionSeed=seed}));
        await engine.PublishAsync(results["wide"],Path.Combine(output,"wide-world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"wide-world")))==Json(results["wide"]),"Seed and complete world replay.");
        Console.WriteLine("PASS: seeded program variety, fair priority search, physical geometry differences, complete routes, cancellation and persisted replay.");
    }
}
