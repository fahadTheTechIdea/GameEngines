using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

internal static class WorldServiceTests
{
    private static Dictionary<string,JsonElement> Properties()=>new();
    private static MetadataServiceProfile Supply(double quantity=10)=>new([new("stock","rest","rest-points",quantity,["reliable"],"station","Authored rest service","test:stock")],[]);
    private static MetadataServiceProfile Demand(bool physical=false,double? distance=null)=>new([],[new("rest","rest","rest-points",4,physical?"travel":"provision","Required game service","test:need",["reliable"],"station",physical,physical?.5:0,distance)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        WorldMetadataArchetype Leaf(string id,MetadataServiceProfile? services=null)=>new(id,id,"Required game role",Properties(),[],[],[],Services:services);
        var root=new WorldMetadataArchetype("root","region","Service graph",Properties(),[new("supply","supply",1,"Required provider"),new("relay","relay",1,"Connection hub"),new("users","user",2,"Required consumers")],[],[]);
        var catalog=new WorldMetadataCatalog(1,"service-catalog",[root,Leaf("supply",Supply()),Leaf("relay"),Leaf("user",Demand())]);
        string P(string suffix)=>"service-world/"+suffix;string L(string id)=>P("explicit/"+id);
        var definition=new MetadataServiceDefinition(Enumerable.Range(0,2).Select(i=>new MetadataServiceBinding(P("users-"+i),"rest",P("supply-0"),"stock",[L("trunk"),L("branch-"+i)],"Deliver required rest","test:binding")).ToArray(),
            [new(L("trunk"),8,"rest-points","Shared bottleneck","test:trunk"),new(L("branch-0"),4,"rest-points","First consumer","test:branch"),new(L("branch-1"),4,"rest-points","Second consumer","test:branch")]);
        var recipe=new WorldMetadataRecipe(1,"service-world","exploration","service-catalog","root","level",MetadataCoverage.PartialScope,
            ["Explicit logical service delivery; physical routes, context, wider world and artwork/runtime remain separate."],new Dictionary<string,int>(),new(["x","y"],"meters"),
            AdditionalConnections:[new("trunk","provision",P("supply-0"),P("relay-0"),false),new("branch-0","provision",P("relay-0"),P("users-0"),false),new("branch-1","provision",P("relay-0"),P("users-1"),false)],Services:definition);
        return(recipe,catalog);
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) PhysicalExample()
    {
        var e=WorldPlacementFeedbackTests.Example(false);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="fixed"?a with{Services=Supply()}:a.Id=="site"?a with{Services=Demand(true,30)}:a).ToArray()};
        string link="placement-feedback/explicit/journey";
        return(e.Recipe with{Services=new([new("placement-feedback/sites-0","rest","placement-feedback/fixed-0","stock",[link],"Deliver within catchment","test:physical")],
            [new(link,4,"rest-points","Declared delivery capacity","test:physical")]),RemainingScopes=["Declared physical service catchment and placement feedback; full catalog context, shared corridors, world generation, artwork and native runtime remain open."]},catalog);
    }
    public static async Task WriteExampleAsync(string dir,bool physical=false)
    {
        if(Directory.Exists(dir)||File.Exists(dir))throw new IOException("Example exists");Directory.CreateDirectory(dir);var e=physical?PhysicalExample():Example();
        await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));
    }
    private sealed class ServiceCatalog(ISettlementCatalogProvider source,bool provides=false) : ISettlementCatalogProvider
    {
        public SettlementCatalogDescriptor Descriptor=>source.Descriptor;
        public IReadOnlyList<BuildingDefinition> Structures=>source.Structures.Select(d=>d with{
            SizeVariants=d.SizeVariants!.Select(v=>v with{Id="capacity-variant",ServiceCapacity=9}).ToArray(),
            SitingProfile=provides?d.SitingProfile! with{Requirements=[],Provides=new HashSet<string>{"test.mana"},OutputUnitsPerCapacity=2}:d.SitingProfile}).ToArray();
        public ISettlementCatalogPolicy Policy=>source.Policy;
        public ISettlementDemandPolicy Demand=>source.Demand;
        public IInfrastructureDesignStrategy Infrastructure=>source.Infrastructure;
        public IReadOnlyList<FacilityDefinition> Facilities=>source.Facilities;
    }
    private static void RegisteredSource()
    {
        var e=PhysicalExample();
        var reference=new MetadataStructureReference(new("test.arcane",new HashSet<string>{"test.mana"}),"test.arcane-hall","capacity-variant","test.leycraft");
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{
            PhysicalFootprint=null,Structure=reference,Services=new([],[],true),
            Access=new(MetadataOccupancyKind.Solid,[new("entry","Required source entrance","test:entry",new(0,-4),new(0,-8),2)])}:
            a.Id=="fixed"?a with{Services=new([new("stock","test.mana","game-service-units",10,["attuned"],"station","Source demand fixture","test:stock")],[])}:a).ToArray()};
        var recipe=e.Recipe with{AdditionalConnections=e.Recipe.AdditionalConnections!.Select(c=>c with{Kind="utility"}).ToArray(),
            RouteRules=e.Recipe.RouteRules!.Select(r=>r with{ConnectionKind="utility"}).ToArray(),
            Services=e.Recipe.Services! with{Bindings=e.Recipe.Services.Bindings.Select(b=>b with{RequirementId="mana"}).ToArray(),
                Links=e.Recipe.Services.Links.Select(l=>l with{Unit="game-service-units"}).ToArray()}};
        using var plugin=new SettlementCatalogPlugin(TestCatalogs.PluginPath);
        var registry=new SettlementCatalogRegistry([new ServiceCatalog(plugin.Providers.Single())]);
        var gen=new WorldMetadataGenerator(registry);
        var world=gen.Generate(recipe,catalog);var allocation=world.ServicePlan!.Allocations.Single();
        Check(Math.Abs(allocation.Quantity-1.9)<1e-8,"selected variant capacity, not definition default, determines demand");
        Check(world.StructureDefinitions.Values.Single().Technology=="test.leycraft"&&world.ServicePlan.Sources.Single(s=>s.NodeId==allocation.ConsumerNodeId).RemainingRequirements.Contains("catalog-context"),"selected technology and unqualified catalog context retained");
        foreach(var technology in new string?[]{null,"unsupported"})
            Reject(()=>gen.Generate(recipe,catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Structure is null?a:a with{Structure=a.Structure with{Technology=technology}}).ToArray()}),
                technology is null?"METADATA_SERVICE_STRUCTURE_TECHNOLOGY_REQUIRED":"METADATA_STRUCTURE_TECHNOLOGY_INVALID");
        Reject(()=>gen.Generate(recipe,catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="site"?a with{Services=Demand(true)}:a).ToArray()}),"METADATA_SERVICE_SOURCE_CONFLICT");
        var service=new MetadataServicePlanner(registry);
        Reject(()=>service.Plan(world.ServicePlan.Definition,world.ServicePlan.Profiles,world.StructureDefinitions,world.Nodes,world.Connections,world.Space,world.RoutePlan,world.NetworkPlan),"METADATA_SERVICE_STRUCTURE_ACCESS_REQUIRED");
        service.ValidateDocument(world);
        Reject(()=>gen.Generate(recipe,catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="fixed"?a with{Services=a.Services! with{Supplies=a.Services!.Supplies.Select(s=>s with{Qualities=[]}).ToArray()}}:a).ToArray()}),
            "METADATA_SERVICE_SUPPLY_POLICY:test.ward-requires-attuned-mana");
        var logical=Example();
        var suppliedCatalog=logical.Catalog with{Archetypes=logical.Catalog.Archetypes.Select(a=>a.Id=="supply"?a with{Structure=reference,Services=new([],[],true,["reliable"])}:
            a.Id=="user"?a with{Services=new([],[Demand().Requirements[0] with{Kind="test.mana",Unit="game-service-units",RequiredOrigin="facility"}])}:a).ToArray()};
        var suppliedRecipe=logical.Recipe with{Services=logical.Recipe.Services! with{
            Bindings=logical.Recipe.Services.Bindings.Select(b=>b with{SupplyId="test.mana"}).ToArray(),
            Links=logical.Recipe.Services.Links.Select(l=>l with{Unit="game-service-units"}).ToArray()}};
        var supplied=new WorldMetadataGenerator(new SettlementCatalogRegistry([new ServiceCatalog(plugin.Providers.Single(),true)])).Generate(suppliedRecipe,suppliedCatalog);
        Check(supplied.ServicePlan!.Sources.Single(s=>s.NodeId=="service-world/supply-0").Supplies.Single().Quantity==18&&
            supplied.ServicePlan.Usage["source:service-world/supply-0/test.mana"]==8,"registered output uses exact selected variant and source multiplier");
        var portCatalog=catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="site"?a with{Services=new([],[],true,["reliable"],[new("test.mana","entry")])}:
            a.Id=="fixed"?a with{Services=new([],[new("delivery","test.mana","game-service-units",4,"utility","Physical output","test:output",["reliable"],"facility",true,.5)])}:a).ToArray()};
        var portRecipe=recipe with{Services=recipe.Services! with{Bindings=[new("placement-feedback/fixed-0","delivery","placement-feedback/sites-0","test.mana",["placement-feedback/explicit/journey"],"Named output entrance","test:output")]}};
        var portWorld=new WorldMetadataGenerator(new SettlementCatalogRegistry([new ServiceCatalog(plugin.Providers.Single(),true)])).Generate(portRecipe,portCatalog);
        Check(portWorld.ServicePlan!.Sources.Single(s=>s.NodeId=="placement-feedback/sites-0").Supplies.Single().PortId=="entry"&&
            Math.Abs(portWorld.ServicePlan.Allocations.Single().Distance!.Value-portWorld.RoutePlan!.Routes.Single().Forward.Cost!.Distance-4)<1e-8,"registered named output port uses actual boundary and approach");
    }
    public static async Task RunAsync(string? cli=null)
    {
        var ledger=new ServiceCapacityLedger(new Dictionary<string,double>{{"source",.3},{"link",.2}});
        Check(ledger.TryReserve(new Dictionary<string,double>{{"source",.1},{"link",.1}}),"first decimal reserve");
        Check(!ledger.TryReserve(new Dictionary<string,double>{{"source",.2},{"link",.2}})&&ledger.Usage["source"]==.1&&ledger.Usage["link"]==.1,"failed batch is atomic");
        Check(ledger.TryReserve(new Dictionary<string,double>{{"source",.2},{"link",.1}})&&ledger.Usage["source"]==.3&&ledger.Usage["link"]==.2,"exact decimal source/link limits");
        Reject(()=>ledger.TryReserve(new Dictionary<string,double>{{"source",-1}}),"SERVICE_CAPACITY_QUANTITY_INVALID");
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);var plan=world.ServicePlan!;
        Check(world.Nodes.Count==5&&plan.Allocations.Count==2&&plan.Allocations.All(a=>!a.Physical&&a.Distance is null),"logical service scope");
        Check(plan.Usage["source:service-world/supply-0/stock"]==8&&plan.Usage["link:service-world/explicit/trunk"]==8,"independent shared source and trunk oracle");
        Reject(()=>generator.Generate(e.Recipe with{Services=null},e.Catalog),"METADATA_SERVICES_DECLARATION_REQUIRED");
        Reject(()=>generator.Generate(e.Recipe with{Services=e.Recipe.Services! with{Bindings=[e.Recipe.Services.Bindings[0]]}},e.Catalog),"METADATA_SERVICE_BINDING_REQUIRED");
        Reject(()=>generator.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="supply"?a with{Services=Supply(7)}:a).ToArray()}),"METADATA_SERVICE_CAPACITY_EXCEEDED");
        Reject(()=>generator.Generate(e.Recipe with{Services=e.Recipe.Services! with{Links=e.Recipe.Services.Links.Select(l=>l.ConnectionId.EndsWith("trunk")?l with{Capacity=7}:l).ToArray()}},e.Catalog),"METADATA_SERVICE_CAPACITY_EXCEEDED");
        Reject(()=>generator.Generate(e.Recipe with{AdditionalConnections=e.Recipe.AdditionalConnections!.Select(c=>c.Id=="trunk"?c with{FromId=c.ToId,ToId=c.FromId}:c).ToArray()},e.Catalog),"METADATA_SERVICE_CHAIN_DISCONNECTED");
        Reject(()=>generator.Generate(e.Recipe with{Services=e.Recipe.Services! with{Bindings=e.Recipe.Services.Bindings.Select(b=>b with{ConnectionIds=["service-world/explicit/branch-0"]}).ToArray()}},e.Catalog),"METADATA_SERVICE_CHAIN_DISCONNECTED");
        Reject(()=>generator.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="user"?a with{Services=Demand() with{Requirements=[Demand().Requirements[0] with{RequiredQualities=["missing"]}]}}:a).ToArray()}),"METADATA_SERVICE_SOURCE_INCOMPATIBLE");
        Reject(()=>generator.Generate(e.Recipe with{Services=e.Recipe.Services! with{MaximumWork=1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        var altered=world with{ServicePlan=plan with{Allocations=plan.Allocations.Select(a=>a with{Quantity=3}).ToArray()}};
        Reject(()=>new MetadataServicePlanner().ValidateDocument(altered),"METADATA_SERVICE_PLAN_MISMATCH");
        var cycCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="supply"?a with{Services=new(Supply().Supplies,Demand().Requirements)}:a.Id=="user"?a with{Services=new(Supply().Supplies,Demand().Requirements)}:a).ToArray()};
        var cycRecipe=e.Recipe with{AdditionalConnections=e.Recipe.AdditionalConnections!.Append(new("back","provision","service-world/users-0","service-world/supply-0",false)).ToArray(),
            Services=e.Recipe.Services! with{Bindings=e.Recipe.Services.Bindings.Append(new("service-world/supply-0","rest","service-world/users-0","stock",["service-world/explicit/back"],"Explicit dependency","test:cycle")).ToArray(),
                Links=e.Recipe.Services.Links.Append(new("service-world/explicit/back",4,"rest-points","Cycle fixture","test:cycle")).ToArray()}};
        Reject(()=>generator.Generate(cycRecipe,cycCatalog),"METADATA_SERVICE_PROVIDER_CYCLE");
        var physical=PhysicalExample();var baseline=generator.Generate(physical.Recipe with{Services=null},physical.Catalog with{Archetypes=physical.Catalog.Archetypes.Select(a=>a with{Services=null}).ToArray()});
        var placed=generator.Generate(physical.Recipe,physical.Catalog);var delivery=placed.ServicePlan!.Allocations.Single();
        Check(delivery.Physical&&delivery.Distance<=30&&placed.PlacementPlan!.FeasibilityAttempts!.Any(a=>a.Rejection?.StartsWith("METADATA_SERVICE_CATCHMENT_EXCEEDED")==true),"physical catchment reconsiders placement");
        Check(placed.Nodes.Single(n=>n.Id==delivery.ConsumerNodeId).Position![1]>baseline.Nodes.Single(n=>n.Id==delivery.ConsumerNodeId).Position![1],"independent closer-position oracle");
        var route=placed.RoutePlan!.Routes.Single().Reverse!;
        Check(Math.Abs(delivery.Distance!.Value-route.Cost!.Distance-4)<1e-8,"physical distance includes final entrance approach");
        Reject(()=>generator.Generate(physical.Recipe with{RouteRules=null},physical.Catalog),"METADATA_SERVICE_PHYSICAL_ROUTE_REQUIRED");
        RegisteredSource();
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.Generate(e.Recipe,e.Catalog,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        string dir=Path.Combine(Path.GetTempPath(),"worldcreator-service-"+Guid.NewGuid().ToString("N"));
        try{
            await WriteExampleAsync(dir);await generator.GenerateFilesAsync(Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"output"));
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"output/world-metadata.json"))),"actual service file replay");
            if(cli is not null){
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"cli"))==0,"actual CLI");
                await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe with{Services=e.Recipe.Services! with{Bindings=[]}}));
                Check(await Run(cli,dir,Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"failed"))==1&&!Directory.Exists(Path.Combine(dir,"failed")),"required service failure cannot publish");
                var audit=await File.ReadAllTextAsync(Path.Combine(dir,".gameframework/operations/audit.jsonl"));Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"service audit");
            }
        }finally{if(Directory.Exists(dir)&&Path.GetDirectoryName(Path.GetFullPath(dir))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(dir).StartsWith("worldcreator-service-",StringComparison.Ordinal))Directory.Delete(dir,true);}
        Console.WriteLine("PASS: shared source/link/decimal/atomic accounting, full demand coverage, directed chains, qualities, provider cycles, selected registered variant demand/output and technology, required source access, logical versus physical scope, catchment-driven placement, approach distance, replay/tamper, budgets/cancellation and CLI when supplied. Full catalog context, containment, junctions and art/runtime remain open.");
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task<int> Run(string cli,string dir,params string[] args){var info=new ProcessStartInfo("dotnet"){WorkingDirectory=dir,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.ArgumentList.Add(cli);info.ArgumentList.Add("generate-metadata");foreach(var a in args)info.ArgumentList.Add(a);using var process=Process.Start(info)!;var output=process.StandardOutput.ReadToEndAsync();var errors=process.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}Console.Write(await output);Console.Write(await errors);return process.ExitCode;}
}
