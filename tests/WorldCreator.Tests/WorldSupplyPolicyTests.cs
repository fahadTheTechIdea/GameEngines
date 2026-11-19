using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Catalogs.Modern;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

internal static class WorldSupplyPolicyTests
{
    private sealed class SpatialPolicyCatalog : ISettlementCatalogProvider,ISettlementCatalogPolicy
    {
        private readonly ModernCatalog source=new();
        public SettlementCatalogDescriptor Descriptor=>source.Descriptor;
        public IReadOnlyList<BuildingDefinition> Structures=>source.Structures;
        public IReadOnlyList<FacilityDefinition> Facilities=>source.Facilities;
        public ISettlementDemandPolicy Demand=>source.Demand;
        public IInfrastructureDesignStrategy Infrastructure=>source.Infrastructure;
        public ISettlementCatalogPolicy Policy=>this;
        public string Id=>"test.source-position";
        public BuildingDefinition Resolve(BuildingDefinition definition,string technology)=>source.Policy.Resolve(definition,technology);
        public IReadOnlySet<string> RequiredCapabilities(BuildingDefinition definition,string technology)=>source.Policy.RequiredCapabilities(definition,technology);
        public IReadOnlySet<string> RequiredCapabilities(MockupWorldDesign design)=>source.Policy.RequiredCapabilities(design);
        public bool RequiresNeighborPolicy(IReadOnlySet<string> first,IReadOnlySet<string> second)=>source.Policy.RequiresNeighborPolicy(first,second);
        public IReadOnlyList<CatalogSupplyIssue> ValidateSupply(BuildingDefinition definition,StructureResourceRequirement requirement,CatalogSupplySource supply,CatalogSupplyContext context,CancellationToken token=default)
        {
            var errors=source.Policy.ValidateSupply(definition,requirement,supply,context,token).ToList();
            if(context.Sources.Values.Single(s=>s.ProviderId is not null).ServicePoint is not {} port)throw new ArgumentException("TEST_SOURCE_PORT_REQUIRED");
            if(port.Y<15)errors.Add(new("test.port-location","Synthetic gameplay rule requires a source port at y >= 15",true));
            return errors;
        }
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldServiceTests.PhysicalExample();
        var definition=new ModernCatalog().Structures.Single(d=>d.Id=="village-house");
        var requirements=definition.SitingProfile!.Requirements;
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{
            PhysicalFootprint=null,Structure=new(ModernCatalog.Selection,"village-house","cottage",FacilityTechnology.Standard),Services=new([],[],true),
            Access=new(MetadataOccupancyKind.Solid,[new("entry","Source entrance","test:policy",new(0,-5),new(0,-9),2)])}:
            a.Id=="fixed"?a with{Services=new(requirements.Select(r=>new MetadataServiceSupply(r.Id,r.Kind,r.Unit,100,
                r.RequiredQualities?.Order(StringComparer.Ordinal).ToArray()??[],r.RequiredOrigin?.ToString().ToLowerInvariant()??"utilitynetwork","Declared game supply","test:policy")).ToArray(),[])}:a).ToArray()};
        var modes=requirements.Select(r=>r.Mode.ToString().ToLowerInvariant()).Distinct().Order(StringComparer.Ordinal).ToArray();
        string Link(string mode)=>"placement-feedback/explicit/"+mode;
        var recipe=e.Recipe with{AdditionalConnections=modes.Select(m=>new MetadataExplicitConnection(m,m,"placement-feedback/sites-0","placement-feedback/fixed-0")).ToArray(),
            RouteRules=modes.Select(m=>e.Recipe.RouteRules![0] with{Id=m+"-route",ConnectionKind=m}).ToArray(),
            Services=new(requirements.Select(r=>new MetadataServiceBinding("placement-feedback/sites-0",r.Id,"placement-feedback/fixed-0",r.Id,[Link(r.Mode.ToString().ToLowerInvariant())],"Required source policy","test:policy")).ToArray(),
                modes.Select(m=>new MetadataServiceLink(Link(m),100,"game-service-units","Declared shared link","test:policy")).ToArray()),
            RemainingScopes=["Registered supply policy executed. Region context, required surroundings, containment, neighbors, complete world, artwork and native runtime remain unqualified."]};
        return(recipe,catalog);
    }
    internal static async Task WriteExampleAsync(string dir)
    {
        if(Directory.Exists(dir)||File.Exists(dir))throw new IOException("Example exists");Directory.CreateDirectory(dir);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(dir,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var registry=new SettlementCatalogRegistry([new ModernCatalog()]);var generator=new WorldMetadataGenerator(registry);var e=Example();
        var world=generator.Generate(e.Recipe,e.Catalog);var plan=world.ServicePlan!;
        Check(plan.PolicyEvaluations is {Count:4}&&plan.PolicyEvaluations.All(p=>p.PolicyId=="modern.siting"),"all active registered requirements invoke actual policy");
        new MetadataServicePlanner(registry).ValidateDocument(world);
        Reject(()=>new MetadataServicePlanner().ValidateDocument(world),"METADATA_SERVICE_POLICY_CATALOG_REQUIRED");
        Reject(()=>new MetadataServicePlanner(registry).ValidateDocument(world with{ServicePlan=plan with{PolicyEvaluations=[]}}),"METADATA_SERVICE_PLAN_MISMATCH");
        var key=world.StructureDefinitions.Keys.Single();var source=world.StructureDefinitions[key];
        var altered=world with{StructureDefinitions=new Dictionary<string,MetadataStructureDefinition>{{key,source with{Catalog=source.Catalog with{ContentSha256=new string('0',64)}}}}};
        Reject(()=>new MetadataServicePlanner(registry).ValidateDocument(altered),"METADATA_SERVICE_POLICY_CATALOG_MISMATCH");
        var identities=source.Catalog.ImplementationIdentities.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);identities[identities.Keys.First()]="forged";
        Reject(()=>new MetadataServicePlanner(registry).ValidateDocument(world with{StructureDefinitions=new Dictionary<string,MetadataStructureDefinition>{{key,source with{Catalog=source.Catalog with{ImplementationIdentities=identities}}}}}),"METADATA_SERVICE_POLICY_CATALOG_MISMATCH");
        Reject(()=>new MetadataServicePlanner(registry).ValidateDocument(world with{StructureDefinitions=new Dictionary<string,MetadataStructureDefinition>{{key,source with{Variant=source.Variant with{ServiceCapacity=6}}}}}),"METADATA_SERVICE_POLICY_SOURCE_MISMATCH");
        var badCatalog=ChangeSupply(e.Catalog,s=>s.Id=="potable-water"?s with{Qualities=["potable","reliable","marine"]}:s);
        Reject(()=>generator.Generate(e.Recipe,badCatalog),"METADATA_SERVICE_SUPPLY_POLICY:modern.water-quality-conflict");
        Reject(()=>generator.Generate(e.Recipe,ChangeSupply(e.Catalog,s=>s.Id=="power"?s with{UpstreamSources=[new("placement-feedback/fixed-0","missing")]}:s)),"CATALOG_SUPPLY_UPSTREAM_MISSING");
        Reject(()=>generator.Generate(e.Recipe,ChangeSupply(e.Catalog,s=>s.Id=="power"?s with{UpstreamSources=[new("placement-feedback/fixed-0","power")]}:s)),"CATALOG_SUPPLY_UPSTREAM_CYCLE");
        Reject(()=>generator.Generate(e.Recipe,ChangeSupply(e.Catalog,s=>s.Id=="power"?s with{PortId="undeclared"}:s)),"METADATA_SERVICE_SUPPLY_PORT_REQUIRED");
        CommonPolicyCases();
        var spatialCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Services=a.Services! with{Outputs=[new("ParentFacility","entry")]}}:a).ToArray()};
        var moved=new WorldMetadataGenerator(new SettlementCatalogRegistry([new SpatialPolicyCatalog()])).Generate(e.Recipe,spatialCatalog);
        Check(moved.PlacementPlan!.FeasibilityAttempts!.Any(a=>a.Rejection?.StartsWith("METADATA_SERVICE_SUPPLY_POLICY:test.port-location")==true)&&
            moved.Nodes.Single(n=>n.Id=="placement-feedback/sites-0").Position![1]>=20,"typed spatial source policy reconsiders placement until the named boundary port meets the independent y oracle");
        string dir=Path.Combine(Path.GetTempPath(),"worldcreator-supply-policy-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(dir);await generator.GenerateFilesAsync(Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),Path.Combine(dir,"api"));
            var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"api/world-metadata.json"),catalogs:registry);
            Check(Json(saved)==Json(world),"file replay uses same actual catalog policy");
            await RejectAsync(()=>WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(dir,"api/world-metadata.json")),"METADATA_SERVICE_POLICY_CATALOG_REQUIRED");
            await RejectAsync(()=>new WorldMetadataSchematicExporter().CreateAsync(world),"METADATA_SERVICE_POLICY_CATALOG_REQUIRED");
            if(cli is not null)
            {
                Check(await Run(cli,dir,Path.Combine(dir,"cli"))==0,"registered policy actual CLI");
                await File.WriteAllTextAsync(Path.Combine(dir,"catalog.json"),Json(badCatalog));
                Check(await Run(cli,dir,Path.Combine(dir,"failed"))==1&&!Directory.Exists(Path.Combine(dir,"failed")),"policy failure cannot publish");
                var audit=await File.ReadAllTextAsync(Path.Combine(dir,".gameframework/operations/audit.jsonl"));Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"actual policy audit");
            }
        }
        finally{if(Directory.Exists(dir)&&Path.GetDirectoryName(Path.GetFullPath(dir))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(dir).StartsWith("worldcreator-supply-policy-",StringComparison.Ordinal))Directory.Delete(dir,true);}
        Console.WriteLine("PASS: actual modern catalog supply policy, common upstream contamination and intake/outfall checks, parent-provider identity, missing/cyclic graph, missing port, source provenance, missing registry, tamper, file/export replay and CLI audit/nonpublication when supplied. Full region context/neighbor/containment and art/runtime remain open.");
    }
    private static void CommonPolicyCases()
    {
        var pack=new ModernCatalog();var policy=pack.Policy;var definition=pack.Structures.Single(d=>d.Id=="water-treatment-plant");
        var requirement=definition.SitingProfile!.Requirements.Single(r=>r.Id=="source-water");
        CatalogSupplySource Source(string id,IReadOnlySet<string> qualities,IReadOnlyList<string>? upstream=null)=>new(id,requirement.Kind,requirement.Unit,"surfacewater",qualities,new(0,0),null,upstream??[]);
        var clean=Source("intake",new HashSet<string>{"fresh","protected","reliable"},["upstream"]);var dirty=Source("upstream",new HashSet<string>{"contaminated"});
        var graph=new CatalogSupplyPolicy([clean,dirty]);
        Check(graph.Evaluate(policy,definition,requirement,"intake",FacilityTechnology.Standard,new Dictionary<string,string>{{"source-water","intake"}}).Single().Code=="modern.protected-intake-contaminated","transitive source contamination uses shared facts");
        var generation=pack.Structures.Single(d=>d.Id=="power-station");var cooling=generation.SitingProfile!.Requirements.Single(r=>r.Id=="cooling-intake");
        var intake=clean with{Kind=cooling.Kind,UpstreamSourceIds=[]};var outfall=dirty with{Id="outfall",Kind=SitingResourceKind.WaterOutfall,Qualities=new HashSet<string>()};
        var inputs=new Dictionary<string,string>{{"cooling-intake","intake"},{"water-outfall","outfall"}};
        var overlap=new CatalogSupplyPolicy([intake,outfall]).Evaluate(policy,generation,cooling,"intake",FacilityTechnology.NuclearOnceThrough,inputs);
        Check(overlap.Single().Code=="modern.intake-outfall-coincident"&&overlap.Single().Spatial,"coincident ports are an explicit spatial policy issue");
        Check(new CatalogSupplyPolicy([intake,outfall with{ServicePoint=new(10,0)}]).Evaluate(policy,generation,cooling,"intake",FacilityTechnology.NuclearOnceThrough,inputs).Count==0,"separate intake/outfall accepted");
        Check(new CatalogSupplyPolicy([intake with{ServicePoint=null},outfall]).Evaluate(policy,generation,cooling,"intake",FacilityTechnology.NuclearOnceThrough,inputs).Single().Code=="modern.service-point-required","missing physical facts cannot pass");
        var parent=new StructureResourceRequirement("parent",SitingResourceKind.ParentFacility,SitingNetworkMode.SiteInterface);
        Check(new CatalogSupplyPolicy([intake]).Evaluate(policy,definition,parent,"intake",FacilityTechnology.Standard,new Dictionary<string,string>()).Single().Code=="modern.parent-facility-required","source label cannot invent registered provider");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{new CatalogSupplyPolicy([clean,dirty],cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
    }
    private static WorldMetadataCatalog ChangeSupply(WorldMetadataCatalog catalog,Func<MetadataServiceSupply,MetadataServiceSupply> change)=>catalog with{Archetypes=catalog.Archetypes.Select(a=>a.Id=="fixed"?a with{Services=a.Services! with{Supplies=a.Services!.Supplies.Select(change).ToArray()}}:a).ToArray()};
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task RejectAsync(Func<Task> action,string code){try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static async Task<int> Run(string cli,string dir,string output){var info=new ProcessStartInfo("dotnet"){WorkingDirectory=dir,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};foreach(var arg in new[]{cli,"generate-metadata",Path.Combine(dir,"recipe.json"),Path.Combine(dir,"catalog.json"),output})info.ArgumentList.Add(arg);using var process=Process.Start(info)!;var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));try{await process.WaitForExitAsync(timeout.Token);}catch(OperationCanceledException){process.Kill(true);throw;}Console.Write(await stdout);Console.Write(await stderr);return process.ExitCode;}
}
