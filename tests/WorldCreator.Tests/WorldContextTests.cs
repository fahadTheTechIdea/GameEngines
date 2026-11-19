using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Catalogs.Modern;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

internal static class WorldContextTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldSupplyPolicyTests.Example();
        var use=new ModernCatalog().Structures.Single(d=>d.Id=="village-house").SitingProfile!.AllowedDistricts.Order(StringComparer.Ordinal).First();
        return(e.Recipe with{Context=new([new("habitat",use,Box(0,15,40,15),"test:habitat")],
            [new("placement-feedback/sites-0","habitat")],[]),
            RemainingScopes=["Bounded registered region and service checks only; source-area resources, full surroundings, junctions, world variety, artwork and native runtime remain open."]},e.Catalog);
    }
    private static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) GenericExample()
    {
        var e=WorldPlacementFeedbackTests.Example(false);var placement=e.Recipe.Placement!;
        var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id==root.Id?a with{Parts=a.Parts.Select(p=>p.Id=="fixed"?p with{Layout=null}:p).ToArray()}:
            a.Id=="fixed"?a with{PhysicalFootprint=new("meters","Hazard marker footprint","test:hazard",Box(-2,-2,4,4))}:a with{Access=null}).ToArray()};
        return(e.Recipe with{RouteRules=null,AdditionalConnections=null,Placement=placement with{
            Request=placement.Request with{Demands=[placement.Request.Demands[0],placement.Request.Demands[0] with{Id="hazard"}]},
            Bindings=[placement.Bindings[0],new("hazard","placement-feedback/fixed-0",true)]},
            Context=new([new("wilds","forest",Box(0,0,40,30),"test:forest")],
                [new("placement-feedback/sites-0","wilds",["rest"]),new("placement-feedback/fixed-0","wilds",["hazard"])],
                [new("rest-buffer","rest","hazard",10,"authored game encounter buffer","test:buffer")])},catalog);
    }
    public static async Task WriteExampleAsync(string directory,bool generic=false)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=generic?GenericExample():Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        CommonCases();
        var registry=new SettlementCatalogRegistry([new ModernCatalog()]);var generator=new WorldMetadataGenerator(registry);var e=Example();
        var world=generator.Generate(e.Recipe,e.Catalog);var context=world.ContextPlan!;
        Check(context.Sources.Single().PolicyId=="modern.siting"&&context.Evaluations.Count==2&&context.Evaluations.All(e=>e.Passed),"registered context from actual policy");
        Check(world.Nodes.Single(n=>n.Id=="placement-feedback/sites-0").Position![1]>=20,"8 by 10 building contained above y=15");
        Check(world.PlacementPlan!.FeasibilityAttempts!.Any(a=>a.Rejection?.StartsWith("METADATA_CONTEXT:region-containment")==true),"region changes candidate selection");
        new MetadataContextPlanner(registry).ValidateDocument(world);
        Reject(()=>new MetadataContextPlanner().ValidateDocument(world),"METADATA_CONTEXT_POLICY_CATALOG_REQUIRED");
        Reject(()=>new MetadataContextPlanner(registry).ValidateDocument(world with{ContextPlan=context with{Evaluations=[]}}),"METADATA_CONTEXT_PLAN_MISMATCH");
        Reject(()=>new MetadataPlacementPlanner(registry).ValidateDocument(world with{ContextPlan=null}),"METADATA_PLACEMENT_FEEDBACK_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{Context=e.Recipe.Context! with{Sites=[]}},e.Catalog),"METADATA_CONTEXT_DEFINITION_INVALID");
        Reject(()=>generator.Generate(e.Recipe with{Context=e.Recipe.Context! with{Sites=[new("placement-feedback/sites-0","habitat",[])]}},e.Catalog),"METADATA_CONTEXT_REGISTERED_TAG_OVERRIDE");
        Reject(()=>generator.Generate(e.Recipe with{Context=e.Recipe.Context! with{Regions=[e.Recipe.Context.Regions[0] with{Use="incompatible"}]}},e.Catalog),"METADATA_CONTEXT:region-use");
        Reject(()=>generator.Generate(e.Recipe with{Context=e.Recipe.Context! with{MaximumWork=1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        var key=world.StructureDefinitions.Keys.Single();var saved=world.StructureDefinitions[key];
        Reject(()=>new MetadataContextPlanner(registry).ValidateDocument(world with{StructureDefinitions=new Dictionary<string,MetadataStructureDefinition>{{key,saved with{
            Definition=saved.Definition with{SitingProfile=saved.Definition.SitingProfile! with{AllowedDistricts=new HashSet<string>{"invented"}}}}}}}),"METADATA_CONTEXT_POLICY_SOURCE_MISMATCH");
        // Context must not depend on service declarations being enabled.
        var noServices=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a with{Services=null}).ToArray()};
        Check(generator.Generate(e.Recipe with{Services=null},noServices).ContextPlan!.Evaluations.All(c=>c.Passed),"independent context capability");
        var generic=GenericExample();var genericWorld=new WorldMetadataGenerator().Generate(generic.Recipe,generic.Catalog);
        var separation=genericWorld.ContextPlan!.Evaluations.Single(c=>c.RuleId=="separation:rest-buffer");
        Check(separation.Distance>=10&&genericWorld.ContextPlan.Sources.All(s=>s.PolicyId is null),"nonurban generic clearance with no registered catalog");
        Check(genericWorld.PlacementPlan!.FeasibilityAttempts!.Any(a=>a.Rejection?.StartsWith("METADATA_CONTEXT:separation")==true),"neighbor geometry drives reconsideration");
        var positions=genericWorld.Nodes.Where(n=>n.Id is "placement-feedback/sites-0" or "placement-feedback/fixed-0").Select(n=>n.Position!).ToArray();
        double dx=Math.Max(0,Math.Abs(positions[0][0]-positions[1][0])-4),dy=Math.Max(0,Math.Abs(positions[0][1]-positions[1][1])-4);
        Check(Math.Abs(separation.Distance!.Value-Math.Sqrt(dx*dx+dy*dy))<1e-8,"independent axis-aligned box edge-distance oracle");
        Reject(()=>new WorldMetadataGenerator().Generate(generic.Recipe with{Context=generic.Recipe.Context! with{Sites=[generic.Recipe.Context.Sites[0]]}},generic.Catalog),"METADATA_CONTEXT_SITE_COVERAGE");
        var mixedCatalog=generic.Catalog with{Archetypes=generic.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{
            PhysicalFootprint=null,Structure=new(ModernCatalog.Selection,"village-house","cottage",FacilityTechnology.Standard)}:a).ToArray()};
        var mixedContext=generic.Recipe.Context! with{Regions=[generic.Recipe.Context.Regions[0] with{Use=e.Recipe.Context!.Regions[0].Use}],
            Sites=[new("placement-feedback/sites-0","wilds"),new("placement-feedback/fixed-0","wilds",["heavy-impact"])],Separations=[]};
        Reject(()=>generator.Generate(generic.Recipe with{Context=mixedContext},mixedCatalog),"METADATA_CONTEXT:neighbor-policy-required");
        new MetadataPlacementPlanner().ValidateDocument(genericWorld);new MetadataContextPlanner().ValidateDocument(genericWorld);
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.Generate(e.Recipe,e.Catalog,cancelled.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}}
        var directory=Path.Combine(Path.GetTempPath(),"worldcreator-context-"+Guid.NewGuid().ToString("N"));
        await WriteExampleAsync(directory);
        await generator.GenerateFilesAsync(Path.Combine(directory,"recipe.json"),Path.Combine(directory,"catalog.json"),Path.Combine(directory,"api"));
        var loaded=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(directory,"api/world-metadata.json"),catalogs:registry);
        Check(Json(loaded)==Json(world),"exact persisted context replay");
        if(cli is not null)
        {
            Check(await Run(cli,directory,"cli")==0,"actual CLI context");
            await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe with{Context=e.Recipe.Context! with{Regions=[e.Recipe.Context.Regions[0] with{Use="incompatible"}]}}));
            Check(await Run(cli,directory,"failed")==1&&!Directory.Exists(Path.Combine(directory,"failed")),"failed context cannot publish");
            var audit=await File.ReadAllTextAsync(Path.Combine(directory,".gameframework/operations/audit.jsonl"));
            Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"existing audit records both outcomes");
        }
        Console.WriteLine("PASS: common context, concave containment, reserved space, registered provenance/use, both neighbor policies, nonurban separation feedback, complete allocation coverage, independent context, replay/tamper, work/cancellation and CLI when supplied. Source-area resources, complete surroundings, junctions, art/native acceptance remain open.");
    }
    private static void CommonCases()
    {
        var owner=new CatalogContextPolicy();var policies=new Dictionary<string,ISettlementCatalogPolicy>();
        var concave=new WorldPolygon([new(0,0),new(10,0),new(10,10),new(7,10),new(7,3),new(3,3),new(3,10),new(0,10)]);
        var bridge=Box(1,6,8,2);Check(bridge.Vertices.All(concave.ContainsPoint),"negative contains every vertex");
        var region=new CatalogContextRegion("region","forest",concave,"test:region");
        var site=new CatalogContextSite("site","region",bridge,[],new HashSet<string>());
        Check(owner.Evaluate([region],[site],[],policies).Single(e=>e.RuleId=="region-containment").Passed==false,"whole edges cannot cross concave cutout");
        region=region with{Boundary=Box(0,0,20,20)};site=site with{Footprint=Box(2,2,2,2),Surroundings=[Box(-1,1,4,4)]};
        Check(owner.Evaluate([region],[site],[],policies).Single(e=>e.RuleId=="region-containment").Passed==false,"required surrounding must fit too");
        var sensitive=site with{Id="a",Surroundings=[],Tags=new HashSet<string>{"sensitive"}};
        var impact=site with{Id="z",Footprint=Box(10,2,2,2),Surroundings=[],Tags=new HashSet<string>{"heavy-impact"}};
        // Only the earlier site's catalog owns a policy; pair iteration must still invoke it.
        policies.Add("a",new ModernCatalog().Policy);
        Check(owner.Evaluate([region],[sensitive,impact],[],policies).Any(e=>e.RuleId=="neighbor-policy-required"&&!e.Passed&&!e.Spatial),"either site's policy requires an explicit rule");
        var rules=new[]{new SitingSeparationRule("buffer","sensitive","heavy-impact",6,"game-authored","test:buffer")};
        var accepted=owner.Evaluate([region],[impact,sensitive],rules,policies);
        Check(accepted.All(e=>e.Passed)&&accepted.Single(e=>e.Distance is not null).Distance==6,"edge clearance exactly six meters including reversed input order");
        Check(owner.Evaluate([region],[impact with{Footprint=Box(9,2,2,2)},sensitive],rules,policies).Any(e=>!e.Passed&&e.Spatial),"spatial separation failure");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task<int> Run(string cli,string directory,string output)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=directory,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(var arg in new[]{cli,"generate-metadata",Path.Combine(directory,"recipe.json"),Path.Combine(directory,"catalog.json"),Path.Combine(directory,output)})info.ArgumentList.Add(arg);
        using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}
        Console.Write(await stdout);Console.Write(await stderr);return process.ExitCode;
    }
}
