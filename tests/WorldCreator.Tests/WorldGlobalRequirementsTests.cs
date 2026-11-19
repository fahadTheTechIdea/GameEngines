using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldGlobalRequirementsTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string reason){if(!value)throw new Exception("World requirements: "+reason);}
    static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var arrival=template.Regions.Single(r=>r.Id=="arrival");
        var wrong=arrival.Catalog with{Archetypes=arrival.Catalog.Archetypes.Select(a=>a.Id=="module"?a with{Kind="different-module"}:
            a.Id=="compound"?a with{ChildRequirements=a.ChildRequirements.Select(c=>c.Kind=="module"?c with{Kind="different-module"}:c).ToArray()}:a).ToArray()};
        var total=new MetadataWorldDesignRequirement("all-modules","module",8,8,true,NodeArchetypeId:"module");
        var each=new MetadataWorldDesignRequirement("each-compound","module",2,2,true,new("compound",4,4,true,"compound"),"module");
        var request=new MetadataRegionalDesignRequest(1,"world-requirements","Select complete world content","test:world-requirements",template,
            [new("arrival",[new("missing-parts",0,arrival.Recipe,wrong),new("complete",1,arrival.Recipe,arrival.Catalog)])],[],2,
            WorldRequirements:[total,each]);
        var engine=new MetadataRegionalDesignGenerator();var result=engine.Generate(request);
        Check(result.Receipt.Attempts.Count==2&&!result.Receipt.Attempts[0].Accepted&&result.Receipt.Attempts[1].Accepted,"global constraints reject incomplete choice");
        var rejected=result.Receipt.Attempts[0].WorldCounts!;var accepted=result.Receipt.Attempts[1].WorldCounts!;
        Check(rejected.Single(c=>c.RequirementId=="all-modules").Count==6&&!rejected.Single(c=>c.RequirementId=="all-modules").Satisfied,"total spans regions");
        Check(!rejected.Single(c=>c.RequirementId=="each-compound").Satisfied&&rejected.Single(c=>c.RequirementId=="each-compound").Regions.Single(r=>r.RegionId=="arrival").Scopes!.Single().Count==0,"incomplete parent attributed to its region");
        Check(accepted.All(c=>c.Satisfied)&&accepted.Single(c=>c.RequirementId=="each-compound").Regions.Sum(r=>r.Scopes!.Count)==4&&
            accepted.Single(c=>c.RequirementId=="each-compound").Regions.All(r=>r.Scopes!.Single().Count==2),"each of four regional parents has two physical parts");
        Check(result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Connections.Count==3&&
            result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"complete nodes and two-way regional paths retained");
        var absent=each with{Scope=each.Scope! with{ParentKind="absent",MinimumParents=1,MaximumParents=4}};
        Check(!MetadataRegionalDesignGenerator.EvaluateWorldRequirements([absent],result.World,new(1000000,default),default).Single().Satisfied,"absent parents cannot satisfy requirement");
        var maximum=each with{Scope=each.Scope! with{MaximumParents=3,MinimumParents=1}};
        Check(!MetadataRegionalDesignGenerator.EvaluateWorldRequirements([maximum],result.World,new(1000000,default),default).Single().Satisfied,"parent limit applies to whole world");
        Reject(()=>MetadataRegionalDesignGenerator.EvaluateWorldRequirements([total],result.World,new(1,default,failureCode:"METADATA_REGIONAL_DESIGN_REQUIREMENT_WORK_BUDGET"),default),"METADATA_REGIONAL_DESIGN_REQUIREMENT_WORK_BUDGET");
        Reject(()=>engine.Generate(request with{WorldRequirements=[total with{Id="bad id"}]}),"METADATA_WORLD_DESIGN_REQUIREMENT_INVALID");
        Reject(()=>engine.Generate(request with{Requirements=[new("all-modules","arrival","module",1,2)]}),"METADATA_WORLD_DESIGN_REQUIREMENT_INVALID");
        Reject(()=>engine.Generate(request with{WorldRequirements=[each with{Scope=each.Scope! with{MinimumParents=0}}]}),"METADATA_WORLD_DESIGN_SCOPE_INVALID");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await engine.PublishAsync(result,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(result),"world requirement witnesses replay");
        var forged=result.Receipt with{Attempts=result.Receipt.Attempts.Select((a,i)=>i==1?a with{WorldCounts=[a.WorldCounts![0] with{Count=999},a.WorldCounts[1]]}:a).ToArray()};
        Reject(()=>engine.Verify(forged,result.World),"METADATA_REGIONAL_DESIGN_REPLAY_MISMATCH");
        Console.WriteLine("PASS: global counts and per-parent requirements select complete regional programs with retained journeys and replay.");
    }
}
