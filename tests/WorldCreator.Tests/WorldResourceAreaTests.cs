using System.Diagnostics;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

internal static class WorldResourceAreaTests
{
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldPlacementFeedbackTests.Example(false);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Access=null,
            PhysicalFootprint=a.PhysicalFootprint! with{Reservation=Box(-3,-3,6,6)},
            Services=new([],[new("gather","energy","charges",2,"resource","Required gathering site","test:resource",SourceContainsSite:true)])}:
            a.Id=="fixed"?a with{Services=new([new("field","energy","charges",4,[],"resourcearea","Authored energy field","test:field",Area:Box(0,10,40,20))],[])}:a).ToArray()};
        var recipe=e.Recipe with{RouteRules=null,AdditionalConnections=null,
            Services=new([new("placement-feedback/sites-0","gather","placement-feedback/fixed-0","field",[],"Gather within source coverage","test:binding")],[]),
            Context=new([new("wilds","forest",Box(0,0,40,30),"test:forest")],[new("placement-feedback/sites-0","wilds",["gatherer"])],[],
                Exclusions:[new("protected-strip",Box(0,14,40,3),new HashSet<string>{"gatherer"},"test:protection")]),
            RemainingScopes=["Declared source-area and exclusion constraints only; hydrology, complete surroundings, junctions, complete worlds and GameArts/Godot remain open."]};
        return(recipe,catalog);
    }
    private sealed class ContainedCatalog(ISettlementCatalogProvider source):ISettlementCatalogProvider
    {
        public SettlementCatalogDescriptor Descriptor=>source.Descriptor;
        public IReadOnlyList<BuildingDefinition> Structures=>source.Structures.Select(d=>d with{SitingProfile=d.SitingProfile! with{
            Requirements=d.SitingProfile!.Requirements.Select(r=>r with{SourceContainsSite=true}).ToArray()}}).ToArray();
        public IReadOnlyList<FacilityDefinition> Facilities=>source.Facilities;
        public ISettlementCatalogPolicy Policy=>source.Policy;
        public ISettlementDemandPolicy Demand=>source.Demand;
        public IInfrastructureDesignStrategy Infrastructure=>source.Infrastructure;
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    public static async Task RunAsync(string? cli=null)
    {
        var e=Example();var generator=new WorldMetadataGenerator();var world=generator.Generate(e.Recipe,e.Catalog);var plan=world.ServicePlan!;
        var allocation=plan.Allocations.Single();
        Check(allocation.AreaContained&&!allocation.Physical&&allocation.Distance is null&&allocation.ConnectionIds.Count==0,"contained resource is not transported service");
        Check(plan.Usage.Count==1&&plan.Usage.Values.Single()==2&&world.Connections.Count==0,"only source capacity consumed, no artificial connection");
        var site=world.PlacementPlan!.Placement.Sites.Single();
        Check(site.Reservation!.Footprint.Vertices.All(p=>p.Y>17&&p.Y<=30),"full reserved space above excluded strip and inside source");
        Check(world.PlacementPlan.FeasibilityAttempts!.Any(a=>a.Rejection?.StartsWith("METADATA_SERVICE_AREA_CONTAINMENT")==true)&&
            world.PlacementPlan.FeasibilityAttempts!.Any(a=>a.Rejection?.StartsWith("METADATA_CONTEXT:exclusion:")==true),"both spatial constraints reconsider placement");
        new MetadataServicePlanner().ValidateDocument(world);new MetadataContextPlanner().ValidateDocument(world);new MetadataPlacementPlanner().ValidateDocument(world);
        Reject(()=>generator.Generate(e.Recipe,Supply(e.Catalog,s=>s with{Area=null})),"METADATA_SERVICE_AREA_REQUIRED");
        Reject(()=>generator.Generate(e.Recipe,Supply(e.Catalog,s=>s with{Area=new WorldPolygon([])})),"METADATA_SERVICE_AREA_INVALID");
        Reject(()=>new MetadataServicePlanner().ValidateDocument(world with{ServicePlan=plan with{Sources=plan.Sources.Select(s=>s with{Supplies=s.Supplies.Select(p=>p with{Area=null}).ToArray()}).ToArray()}}),"METADATA_SERVICE_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe,Supply(e.Catalog,s=>s with{Quantity=1})),"METADATA_SERVICE_CAPACITY_EXCEEDED");
        Reject(()=>generator.Generate(e.Recipe with{Services=e.Recipe.Services! with{Bindings=e.Recipe.Services.Bindings.Select(b=>b with{ConnectionIds=["invented"]}).ToArray()}},e.Catalog),"METADATA_SERVICE_AREA_LINK_CONFLICT");
        Reject(()=>generator.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Services=a.Services! with{
            Requirements=a.Services.Requirements.Select(r=>r with{RequirePhysicalRoute=true}).ToArray()}}:a).ToArray()}),"METADATA_SERVICE_REQUIREMENT_INVALID");
        Reject(()=>new MetadataServicePlanner().Plan(plan.Definition,plan.Profiles,world.StructureDefinitions,world.Nodes,world.Connections,world.Space),"METADATA_SERVICE_AREA_ALLOCATION_REQUIRED");
        var changed=plan.Profiles.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
        changed["fixed"]=changed["fixed"] with{Supplies=changed["fixed"].Supplies.Select(s=>s with{Area=site.Footprint.Footprint}).ToArray()};
        Reject(()=>new MetadataServicePlanner().Plan(plan.Definition,changed,world.StructureDefinitions,world.Nodes,world.Connections,world.Space,placement:world.PlacementPlan),"METADATA_SERVICE_AREA_CONTAINMENT");
        double x=site.Position.X,y=site.Position.Y;
        var concave=new WorldPolygon([new(x-5,y-5),new(x+5,y-5),new(x+5,y+5),new(x+.5,y+5),new(x+.5,y-3),new(x-.5,y-3),new(x-.5,y+5),new(x-5,y+5)]);
        Check(site.Footprint.Footprint.Vertices.All(concave.ContainsPoint),"all primary footprint vertices inside the concave resource negative");
        changed["fixed"]=changed["fixed"] with{Supplies=changed["fixed"].Supplies.Select(s=>s with{Area=concave}).ToArray()};
        Reject(()=>new MetadataServicePlanner().Plan(plan.Definition,changed,world.StructureDefinitions,world.Nodes,world.Connections,world.Space,placement:world.PlacementPlan),"METADATA_SERVICE_AREA_CONTAINMENT");
        Reject(()=>new MetadataServicePlanner().ValidateDocument(world with{ServicePlan=plan with{Allocations=[allocation with{AreaContained=false}]}}),"METADATA_SERVICE_PLAN_MISMATCH");
        Reject(()=>generator.Generate(e.Recipe with{Services=e.Recipe.Services! with{MaximumWork=1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
        // Two full-size consumers share the same contained source; no links or demand reduction.
        var twoCatalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id==e.Recipe.RootArchetypeId?a with{
            Parts=a.Parts.Select(p=>p.Id=="sites"?p with{Count=2}:p).ToArray(),ChildRequirements=[new("site",3,3)]}:a).ToArray()};
        twoCatalog=Supply(twoCatalog,s=>s with{Area=Box(0,0,40,30)});
        var placement=e.Recipe.Placement!;
        var twoRecipe=e.Recipe with{Context=null,Placement=placement with{
            Request=placement.Request with{Demands=[placement.Request.Demands[0],placement.Request.Demands[0] with{Id="second"}]},
            Bindings=[placement.Bindings[0],new("second","placement-feedback/sites-1",true)]},
            Services=e.Recipe.Services! with{Bindings=[e.Recipe.Services.Bindings[0],e.Recipe.Services.Bindings[0] with{ConsumerNodeId="placement-feedback/sites-1"}]}};
        var two=generator.Generate(twoRecipe,twoCatalog);
        Check(two.ServicePlan!.Allocations.Count==2&&two.ServicePlan.Usage.Values.Single()==4,"contained consumers share exact source accounting");
        Reject(()=>generator.Generate(twoRecipe,Supply(twoCatalog,s=>s with{Quantity=3})),"METADATA_SERVICE_CAPACITY_EXCEEDED");
        Exclusions();Registered(e);
        using(var cts=new CancellationTokenSource()){cts.Cancel();try{new MetadataServicePlanner().ValidateDocument(world,cts.Token);throw new Exception("No cancellation");}catch(OperationCanceledException){}}
        string directory=Path.Combine(Path.GetTempPath(),"worldcreator-resource-area-"+Guid.NewGuid().ToString("N"));await WriteExampleAsync(directory);
        await generator.GenerateFilesAsync(Path.Combine(directory,"recipe.json"),Path.Combine(directory,"catalog.json"),Path.Combine(directory,"api"));
        Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(directory,"api/world-metadata.json")))==Json(world),"saved area and exclusion replay");
        if(cli is not null)
        {
            Check(await Run(cli,directory,"cli")==0,"actual CLI");
            await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(Supply(e.Catalog,s=>s with{Area=null})));
            Check(await Run(cli,directory,"failed")==1&&!Directory.Exists(Path.Combine(directory,"failed")),"missing area does not publish");
            var audit=await File.ReadAllTextAsync(Path.Combine(directory,".gameframework/operations/audit.jsonl"));
            Check(audit.Contains("world.metadata.succeeded")&&audit.Contains("world.metadata.failed"),"both operation outcomes audited");
        }
        Console.WriteLine("PASS: generic and registered contained resources, source capacity without links, full reserved geometry and concave boundaries, tagged exclusions including touching/contained/crossing/surroundings-only cases, independent tag scope, spatial feedback, replay/tamper, budgets/cancellation and CLI when supplied. Complete surroundings, corridors, world/art/native gates remain open.");
    }
    private static void Registered((WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) e)
    {
        using var plugin=new SettlementCatalogPlugin(TestCatalogs.PluginPath);
        var registry=new SettlementCatalogRegistry([new ContainedCatalog(plugin.Providers.Single())]);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{PhysicalFootprint=null,
            Structure=new(new("test.arcane",new HashSet<string>{"test.mana"}),"test.arcane-hall","small","test.leycraft"),Services=new([],[],true)}:
            a.Id=="fixed"?a with{Services=new([new("field","test.mana","game-service-units",4,["attuned"],"resourcearea","Authored ritual area","test:field",Area:Box(0,10,40,20))],[])}:a).ToArray()};
        var recipe=e.Recipe with{Context=null,Services=e.Recipe.Services! with{Bindings=e.Recipe.Services.Bindings.Select(b=>b with{RequirementId="mana"}).ToArray()}};
        var generator=new WorldMetadataGenerator(registry);var world=generator.Generate(recipe,catalog);
        Check(world.ServicePlan!.Allocations.Single().AreaContained&&world.ServicePlan.Allocations.Single().Quantity==1.5&&
            world.ServicePlan.PolicyEvaluations!.Single().PolicyId=="test.ward-algorithm","registered source requirement, quantity and callback preserved");
        new MetadataServicePlanner(registry).ValidateDocument(world);
        Reject(()=>generator.Generate(recipe,Supply(catalog,s=>s with{Qualities=[]})),"METADATA_SERVICE_SUPPLY_POLICY:test.ward-requires-attuned-mana");
    }
    private static void Exclusions()
    {
        var region=new CatalogContextRegion("r","wilds",Box(0,0,20,20),"test:region");
        var site=new CatalogContextSite("s","r",Box(5,5,4,4),[],new HashSet<string>{"built"});
        var owner=new CatalogContextPolicy();var policies=new Dictionary<string,ISettlementCatalogPolicy>();
        foreach(var shape in new[]{Box(4,6,6,1),Box(6,6,1,1),Box(4,4,6,6),Box(9,5,2,2)})
        {
            var zone=new CatalogContextExclusion("z",shape,new HashSet<string>{"built"},"test:zone");
            Check(owner.Evaluate([region],[site],[],policies,exclusions:[zone]).Single(e=>e.RuleId=="exclusion:z").Passed==false,"crossing, contained, surrounding or touching exclusion fails");
            Check(owner.Evaluate([region],[site with{Tags=new HashSet<string>{"allowed"}}],[],policies,exclusions:[zone]).All(e=>e.Passed),"unaffected tag remains permitted");
        }
        var surrounding=site with{Surroundings=[Box(4,4,6,6)]};
        Check(owner.Evaluate([region],[surrounding],[],policies,exclusions:[new("z",Box(4.2,4.2,.3,.3),new HashSet<string>{"built"},"test:zone")]).Any(e=>e.RuleId=="exclusion:z"&&!e.Passed),"surrounding-only overlap rejects");
        Reject(()=>owner.Evaluate([region],[site],[],policies,exclusions:[new("z",Box(1,1,1,1),new HashSet<string>(),"test:zone")]),"CATALOG_CONTEXT_EXCLUSION_INVALID");
    }
    private static WorldMetadataCatalog Supply(WorldMetadataCatalog catalog,Func<MetadataServiceSupply,MetadataServiceSupply> change)=>catalog with{
        Archetypes=catalog.Archetypes.Select(a=>a.Id=="fixed"?a with{Services=a.Services! with{Supplies=a.Services!.Supplies.Select(change).ToArray()}}:a).ToArray()};
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
