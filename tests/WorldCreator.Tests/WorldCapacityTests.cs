using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Catalogs.Modern;

internal static class WorldCapacityTests
{
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldPlacementTests.Example();var source=e.Recipe.Placement!.Request;
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id switch{
            "root"=>a with{Parts=[a.Parts[0] with{Count=1,Capacity=new("rest",false,0,"rest-spaces","Provide requested rest spaces","example:rest",QuantityParameter:"visitors",MaximumCount:8)}],ChildRequirements=[new("camp",1,8)]},
            "camp"=>a with{Capacities=[new("rest","rest-spaces",10,"Capacity of this game-authored camp","catalog:rest")],
                PhysicalFootprint=new("meters","Camp source footprint","catalog:camp",source.Demands[0].Footprint!)},_=>a}).ToArray()};
        return(e.Recipe with{Id="capacity-world",Parameters=new Dictionary<string,int>{{"visitors",25}},
            Placement=new(source with{Demands=[source.Demands[0] with{Id="camp",Footprint=null}],Relations=[]},[new("camp","capacity-world",true,PartId:"camps")]),
            NetworkRules=[e.Recipe.NetworkRules![0] with{RegionNodeId="capacity-world",MinimumDestinations=2,MaximumDestinations=8}],
            RemainingScopes=["Declared content capacity and exact physical allocation; service delivery, complete siting policy, junctions, art and native runtime remain open."]},catalog);
    }
    public static async Task WriteExampleAsync(string dir)
    {
        if(Directory.Exists(dir)||File.Exists(dir))throw new IOException("Example exists");Directory.CreateDirectory(dir);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));
    }
    private static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Logical()
    {
        var e=Example();return(e.Recipe with{Placement=null,Surface=null,Environment=null,NetworkRules=null,RequireAllPositions=false,Origin=null},
            e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a with{PhysicalFootprint=null,Parts=a.Parts.Select(p=>p with{Layout=null}).ToArray()}).ToArray()});
    }
    public static async Task RunAsync(string? cli=null)
    {
        var gen=new WorldMetadataGenerator();var e=Example();var world=gen.Generate(e.Recipe,e.Catalog);var allocation=world.CapacityPlan!.Allocations.Single();
        Check(allocation.Count==3&&allocation.ProvidedQuantity==30&&allocation.Request.RequiredQuantity==25,"independent ceil 25/10 oracle");
        Check(world.Nodes.Count==10&&world.PlacementPlan!.Placement.Sites.Count==3&&world.PlacementPlan.Definition.Bindings.Count==3,"all required instances and six constituents placed");
        Check(world.PlacementPlan!.TemplateDefinition!.Bindings.Single().PartId=="camps"&&world.NetworkPlan!.Networks.Single().Plan.Destinations.Count==3,"template and dependent network follow expanded content");
        foreach(var shape in world.PlacementPlan.Footprints)Check(Json(shape.Polygon)==Json(e.Catalog.Archetypes.Single(a=>a.Id=="camp").PhysicalFootprint!.Polygon),"source shape unchanged");
        var larger=gen.Generate(e.Recipe with{Parameters=new Dictionary<string,int>{{"visitors",31}}},e.Catalog);
        Check(larger.CapacityPlan!.Allocations.Single().Count==4&&larger.PlacementPlan!.Placement.Sites.Count==4&&larger.Nodes.Count==13,"parameter growth expands placement and constituents");
        var logical=Logical();var minimum=logical.Catalog with{Archetypes=logical.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{Count=5}],ChildRequirements=[new("camp",1,8)]}:a).ToArray()};
        Check(gen.Generate(logical.Recipe,minimum).CapacityPlan!.Allocations.Single().Count==5,"minimum content preserved above demand");
        var decimals=logical.Catalog with{Archetypes=logical.Catalog.Archetypes.Select(a=>a.Id switch{
            "root"=>a with{Parts=[a.Parts[0] with{Capacity=a.Parts[0].Capacity! with{QuantityParameter=null,RequiredQuantity=.3}}]},
            "camp"=>a with{Capacities=[a.Capacities![0] with{Quantity=.1}]},_=>a}).ToArray()};
        Check(gen.Generate(logical.Recipe with{Parameters=new Dictionary<string,int>()},decimals).CapacityPlan!.Allocations.Single().Count==3,"decimal capacity avoids spurious fourth instance");
        var nested=logical.Catalog with{Archetypes=logical.Catalog.Archetypes.Select(a=>a.Id switch{
            "camp"=>a with{Parts=[a.Parts[0] with{Count=1,Capacity=new("charge",false,7,"charges","Required charge supply","test")}]},
            "station"=>a with{Capacities=[new("charge","charges",6,"Game-authored device charges","test")]},_=>a}).ToArray()};
        Check(gen.Generate(logical.Recipe,nested).CapacityPlan!.Allocations.Count==4,"nested demand scopes tracked independently");
        var zero=logical.Catalog with{Archetypes=logical.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{Count=0}],ChildRequirements=[]}:a).ToArray()};
        Check(gen.Generate(logical.Recipe with{Parameters=new Dictionary<string,int>{{"visitors",0}}},zero).CapacityPlan!.Allocations.Single().Count==0,"zero demand and zero minimum add no instances");
        var related=e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Anchors=[new("entry",new(35,35))],
            Relations=[new("near-entry","camp","entry","Keep all required camps near entry","test",0,100,20,1)]}}};
        Check(gen.Generate(related,e.Catalog).PlacementPlan!.Definition.Request.Relations.Count==3,"anchor relation expands to each group member");
        var hierarchy=WorldHierarchyPlacementTests.Example();var stage=hierarchy.Recipe.Placement!.NestedStages!.First(s=>s.Id=="camp-0");
        var groupedStage=stage with{Request=stage.Request with{Demands=[stage.Request.Demands[0]]},Bindings=[new(stage.Request.Demands[0].Id,stage.ParentNodeId,true,PartId:"stations")]};
        var grouped=gen.Generate(hierarchy.Recipe with{Placement=hierarchy.Recipe.Placement with{NestedStages=hierarchy.Recipe.Placement.NestedStages!.Select(s=>s.Id==stage.Id?groupedStage:s).ToArray()}},hierarchy.Catalog);
        Check(grouped.PlacementPlan!.NestedStages.Single(s=>s.Id==stage.Id).Placement.Sites.Count==2,"nested direct-part group expands through the same solver");
        var prefix=logical.Catalog with{Archetypes=logical.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0],a.Parts[0] with{Id="camps-extra",Count=1,Capacity=null}]}:a).ToArray()};
        Check(gen.Generate(logical.Recipe,prefix).CapacityPlan!.Allocations.Single().Count==3,"similar part prefixes not double-counted");
        var registered=logical.Catalog with{Archetypes=logical.Catalog.Archetypes.Select(a=>a.Id switch{
            "root"=>a with{Parts=[a.Parts[0] with{Capacity=new(null,true,1,"service-units","Selected source capacity","test",MaximumCount:8)}]},
            "camp"=>a with{Capacities=null,Structure=new(ModernCatalog.Selection,"warehouse","small")},_=>a}).ToArray()};
        // The selected source owns the unit; inspect that actual fact before setting the rule.
        var pack=TestCatalogs.Registry.Resolve(ModernCatalog.Selection);var building=pack.Structures.Single(b=>b.Id=="warehouse");var variant=building.SizeVariants!.Single(v=>v.Id=="small");
        registered=registered with{Archetypes=registered.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{Capacity=a.Parts[0].Capacity! with{Unit=building.CapacityUnit,RequiredQuantity=variant.ServiceCapacity+1}}]}:a).ToArray()};
        var registeredWorld=new WorldMetadataGenerator(TestCatalogs.Registry).Generate(logical.Recipe with{Parameters=new Dictionary<string,int>()},registered);
        Check(registeredWorld.CapacityPlan!.Allocations.Single().Count==2&&registeredWorld.CapacityPlan.Allocations[0].Contributions.Single().Source.Quantity==variant.ServiceCapacity,"selected variant capacity is authoritative");
        Reject(()=>gen.Generate(e.Recipe with{Parameters=new Dictionary<string,int>{{"visitors",81}}},e.Catalog),"METADATA_CAPACITY_INSTANCE_BUDGET");
        Reject(()=>gen.Generate(e.Recipe with{MaximumNodes=3},e.Catalog),"METADATA_");
        var tooMany=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{Capacity=a.Parts[0].Capacity! with{MaximumCount=100}}],ChildRequirements=[new("camp",1,100)]}:a).ToArray()};
        Reject(()=>gen.Generate(e.Recipe with{Parameters=new Dictionary<string,int>{{"visitors",170}}},tooMany),"METADATA_PLACEMENT_PART_BINDING_COUNT");
        Reject(()=>gen.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="camp"?a with{Capacities=[a.Capacities![0] with{Unit="different"}]}:a).ToArray()}),"METADATA_CAPACITY_UNIT_MISMATCH");
        Reject(()=>gen.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="camp"?a with{Capacities=[a.Capacities![0] with{Quantity=0}]}:a).ToArray()}),"METADATA_CAPACITY_DEFINITION_INVALID");
        Reject(()=>gen.Generate(e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=[e.Recipe.Placement.Bindings[0] with{PartId="missing"}]}},e.Catalog),"METADATA_PLACEMENT_PART_BINDING_COUNT");
        Check(Json(world.CapacityPlan)==Json(gen.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Reverse().ToArray()}).CapacityPlan),"catalog order deterministic");
        var tampered=world with{CapacityPlan=world.CapacityPlan with{Allocations=[allocation with{ProvidedQuantity=1}]}};
        Reject(()=>new MetadataCapacityPlanner().ValidateDocument(tampered),"METADATA_CAPACITY_PLAN_MISMATCH");
        Reject(()=>new MetadataCapacityPlanner().ValidateDocument(world with{CapacityPlan=world.CapacityPlan with{Allocations=[allocation with{Contributions=[allocation.Contributions[0] with{Source=allocation.Contributions[0].Source with{Quantity=9}}]}]}}),"METADATA_CAPACITY_PLAN_MISMATCH");
        Reject(()=>new MetadataCapacityPlanner().ValidateDocument(world with{Nodes=world.Nodes.Where(n=>n.Id!="capacity-world/camps-0").ToArray()}),"METADATA_CAPACITY_MEMBERSHIP_MISMATCH");
        Reject(()=>new MetadataPlacementPlanner().ValidateDocument(world with{PlacementPlan=world.PlacementPlan with{TemplateDefinition=world.PlacementPlan.TemplateDefinition! with{Bindings=[new("camp","capacity-world",true,PartId:"missing")]}}}),"METADATA_PLACEMENT_PART_BINDING_COUNT");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{gen.Generate(e.Recipe,e.Catalog,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        string dir=Path.Combine(Path.GetTempPath(),"worldcreator-capacity-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(dir);await gen.GenerateFilesAsync(Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"output"));
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"output/world-metadata.json"))),"capacity and placement replay");
            await File.WriteAllTextAsync(Path.Combine(dir,"bad.json"),Json(tampered));await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"bad.json")),"METADATA_CAPACITY_PLAN_MISMATCH");
            await RejectAsync(()=>new WorldMetadataSchematicExporter().CreateAsync(tampered),"METADATA_CAPACITY_PLAN_MISMATCH");
            if(cli is not null)
            {
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"cli"))==0,"actual CLI");
                await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe with{Parameters=new Dictionary<string,int>{{"visitors",81}}}));
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"failed"))==1&&!Directory.Exists(Path.Combine(dir,"failed")),"capacity failure prevents publication");
                var audit=await File.ReadAllTextAsync(Path.Combine(dir,".gameframework/operations/audit.jsonl"));Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"capacity outcomes audited");
            }
        }
        finally{if(Directory.Exists(dir)&&Path.GetDirectoryName(Path.GetFullPath(dir))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(dir).StartsWith("worldcreator-capacity-",StringComparison.Ordinal))Directory.Delete(dir,true);}
        Console.WriteLine("PASS: capacity ceiling/minimum/decimal oracles, parameter-driven physical expansion, selected variant source, generic nested scopes, prefix isolation, bounds/units/source validation, templates, replay/tamper, cancellation and CLI when supplied. Service delivery, full siting, junctions, art and runtime remain open.");
    }
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    private static async Task RejectAsync(Func<Task> action,string prefix){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    private static async Task<int> Run(string cli,string dir,params string[] args)
    {
        var info=new ProcessStartInfo("dotnet"){WorkingDirectory=dir,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);
        using var p=Process.Start(info)!;var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try{await p.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){p.Kill(true);throw;}Console.Write(await stdout);Console.Write(await stderr);return p.ExitCode;
    }
}

