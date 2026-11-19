using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalScopedRequirementsTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Scoped regional requirement: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var region=template.Regions.Single(r=>r.Id=="arrival");
        var wrong=region.Catalog with{Archetypes=region.Catalog.Archetypes.Select(a=>a.Id=="module"?a with{Kind="different-module"}:
            a.Id=="compound"?a with{ChildRequirements=a.ChildRequirements.Select(c=>c.Kind=="module"?c with{Kind="different-module"}:c).ToArray()}:a).ToArray()};
        var requirement=new MetadataRegionalDesignRequirement("complete-compounds","arrival","module",2,2,true,new("compound",1,1,true,"compound"),"module");
        var request=new MetadataRegionalDesignRequest(1,"scoped-design","Select complete game structures","test:scope",template,
            [new("arrival",[new("wrong-parts",0,region.Recipe,wrong),new("required-parts",1,region.Recipe,region.Catalog)])],[requirement],2);
        var engine=new MetadataRegionalDesignGenerator();var result=engine.Generate(request);
        Check(result.Receipt.Attempts.Count==2&&!result.Receipt.Attempts[0].Accepted&&result.Receipt.Attempts[1].Accepted&&
            result.Receipt.Attempts[1].Selections.Single().OptionId=="required-parts","incomplete compound rejected and complete alternative selected");
        var accepted=result.Receipt.Attempts[1];var proof=accepted.Counts.Single();
        Check(proof.Scopes is {Count:1}&&proof.Scopes[0].Count==2&&proof.Scopes[0].Satisfied&&accepted.RequirementWork>0,"actual per-parent physical count and work recorded");
        Check(result.World.Regions.Count==4&&result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&
            result.World.Connections.Count==3&&result.World.Connections.All(c=>c.Reverse?.Path is not null),"all four physical programs and two-way journeys retained");
        var source=result.World.Regions.Single(r=>r.RegionId=="arrival").World;var prototype=source.Nodes[0];
        WorldMetadataNode N(string id,string kind,string? parent)=>prototype with{Id=id,Kind=kind,ParentId=parent};
        var synthetic=source with{Nodes=[N("p1","complex",null),N("p2","complex",null),N("a","part","p1"),N("b","part","p1"),
            N("c","part","p2"),N("sub","subassembly","p2"),N("d","part","sub")]};
        var scoped=new MetadataRegionalDesignRequirement("parts","arrival","part",2,2,false,new("complex",2,2,true));
        WorldSpatialWorkBudget Budget()=>new(1000,default);
        var direct=MetadataRegionalDesignGenerator.EvaluateRequirement(scoped,synthetic,Budget(),default);
        Check(!direct.Satisfied&&direct.Scopes is {Count:2}&&direct.Scopes[0].Count==2&&direct.Scopes[1].Count==1,"global total cannot hide an incomplete parent");
        var deep=MetadataRegionalDesignGenerator.EvaluateRequirement(scoped with{Scope=scoped.Scope! with{DirectChildrenOnly=false}},synthetic,Budget(),default);
        Check(deep.Satisfied&&deep.Count==4&&deep.Scopes!.All(s=>s.Count==2),"descendant scope counts nested substructures");
        var absent=MetadataRegionalDesignGenerator.EvaluateRequirement(scoped with{Scope=new("missing",1,2)},synthetic,Budget(),default);
        Check(!absent.Satisfied&&absent.Scopes is {Count:0},"missing parents cannot pass vacuously");
        var multipleInput=WorldRegionDemandTests.Example();var generator=new WorldMetadataGenerator();
        var multiple=generator.Generate(multipleInput.Recipe,multipleInput.Catalog);
        var three=new MetadataRegionalDesignRequirement("all-compounds","arrival","module",2,2,true,new("compound",3,3,true),"module");
        var threeResult=MetadataRegionalDesignGenerator.EvaluateRequirement(three,multiple,new(5000000,default),default);
        Check(threeResult.Satisfied&&threeResult.Scopes is {Count:3}&&threeResult.Scopes.All(s=>s.Count==2)&&
            multiple.Nodes.Count==11&&multiple.AccessPlan!.Sites.Count==9&&multiple.RoutePlan!.Routes.Count==3&&
            multiple.RoutePlan.Routes.All(r=>r.Forward.Path is not null&&r.Reverse?.Path is not null)&&multiple.NetworkPlan!.Networks.Single().Plan.Edges.Count==2,
            "three complete physical structures preserve every nested module and required route");
        var wide=three with{Scope=three.Scope! with{MinimumParents=1,MaximumParents=1,ParentArchetypeId="wide"}};
        var wideResult=MetadataRegionalDesignGenerator.EvaluateRequirement(wide,multiple,new(5000000,default),default);
        Check(wideResult.Satisfied&&wideResult.Scopes is {Count:1}&&multiple.Nodes.Single(n=>n.Id==wideResult.Scopes[0].ParentNodeId).ArchetypeId=="wide",
            "parent archetype selects one complete program among parents with the same kind");
        var missingPart=MetadataRegionalDesignGenerator.EvaluateRequirement(wide with{NodeArchetypeId="absent"},multiple,new(5000000,default),default);
        Check(!missingPart.Satisfied&&missingPart.Scopes is {Count:1}&&missingPart.Scopes[0].Count==0,"part archetype filter never substitutes another part kind");
        Reject(()=>MetadataRegionalDesignGenerator.EvaluateRequirement(scoped,synthetic,new(1,default,failureCode:"METADATA_REGIONAL_DESIGN_REQUIREMENT_WORK_BUDGET"),default),"METADATA_REGIONAL_DESIGN_REQUIREMENT_WORK_BUDGET");
        Reject(()=>engine.Generate(request with{MaximumRequirementWork=1}),"METADATA_REGIONAL_DESIGN_REQUIREMENT_WORK_BUDGET");
        Reject(()=>engine.Generate(request with{Requirements=[requirement with{Scope=new("compound",0,1)}]}),"METADATA_REGIONAL_DESIGN_SCOPE_INVALID");
        Reject(()=>engine.Generate(request with{Requirements=[requirement with{NodeArchetypeId="invalid id"}]}),"METADATA_REGIONAL_DESIGN_REQUIREMENT_INVALID");
        Reject(()=>engine.Generate(request with{Requirements=[requirement with{Scope=requirement.Scope! with{ParentArchetypeId="invalid id"}}]}),"METADATA_REGIONAL_DESIGN_SCOPE_INVALID");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await engine.PublishAsync(result,Path.Combine(output,"world"));
        await generator.PublishAsync(multipleInput.Recipe,multipleInput.Catalog,multiple,Path.Combine(output,"multiple"),null,CancellationToken.None);
        Check(Json(await generator.ReadPublishedAsync(Path.Combine(output,"multiple")))==Json(multiple),"three-parent physical world replays");
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(result),"complete selected program and scoped witness replay");
        var forged=result.Receipt with{Attempts=result.Receipt.Attempts.Select((a,i)=>i==1?a with{Counts=[a.Counts[0] with{Count=3}]}:a).ToArray()};
        Reject(()=>engine.Verify(forged,result.World),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        Console.WriteLine("PASS: scoped parent/child requirements select complete physical regional programs and preserve all required journeys.");
    }
}
