using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;

internal static class WorldCorridorCapacityTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions(true));
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Corridor capacity: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static MetadataServiceProfile Supply()=>new([new("stock","support","units",8,[],"facility","Game service source","test:source")],[]);
    private static MetadataServiceProfile Demand()=>new([],[new("need","support","units",4,"travel","Required game delivery","test:demand",RequirePhysicalRoute:true,MinimumWidth:1)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool opposite=false)
    {
        var e=WorldRouteReuseTests.Example();
        var root=e.Catalog.Archetypes[0] with{Parts=e.Catalog.Archetypes[0].Parts.Select(p=>p with{ArchetypeId=(p.Id=="a"?!opposite:p.Id=="b"?opposite:p.Id=="c")?"supply":"consumer"}).ToArray()};
        var leaf=e.Catalog.Archetypes[1];var catalog=e.Catalog with{Archetypes=[root,leaf with{Id="supply",Services=Supply()},leaf with{Id="consumer",Services=Demand()}]};
        string Node(string id)=>"corridor-reuse/"+id+"-0";string Link(string id)=>"corridor-reuse/explicit/"+id;
        var services=new MetadataServiceDefinition([new(Node(opposite?"a":"b"),"need",Node(opposite?"b":"a"),"stock",[Link("a-trunk")],"First delivery","test:first"),
            new(Node("d"),"need",Node("c"),"stock",[Link("b-branch")],"Second delivery","test:second")],
            [new(Link("a-trunk"),4,"units","First link limit","test:link"),new(Link("b-branch"),4,"units","Second link limit","test:link")],
            CorridorCapacities:[new("trails",8,"units","Shared physical passage budget","test:capacity")]);
        return(e.Recipe with{Services=services,RemainingScopes=["Diagnostic shared-segment capacity; independent resilience, adaptive regions, artwork and native runtime remain open"]},catalog);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("EXAMPLE_EXISTS");Directory.CreateDirectory(directory);var e=Example();
        await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(e.Catalog));
    }
    private static void Feedback()
    {
        var e=WorldPlacementFeedbackTests.Example(false,true);var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId);
        root=root with{Parts=[..root.Parts,new("other","other",1,"Fixed second consumer",Layout:MetadataLineLayout.Parameters([5,5],[0,0]))]};
        var other=new WorldMetadataArchetype("other","destination","Required fixed delivery",new Dictionary<string,JsonElement>(),[],[],[],LayerId:"intent",Services:Demand() with{Requirements=[Demand().Requirements[0] with{MinimumWidth=.5}]});
        var catalog=e.Catalog with{Archetypes=[..e.Catalog.Archetypes.Select(a=>a.Id==root.Id?root:a.Id=="fixed"?a with{Services=Supply()}:a.Id=="site"?a with{Services=other.Services}:a),other]};
        string Link(string id)=>"placement-feedback/explicit/"+id;
        var rule=new MetadataRouteRule("delivery","travel",e.Recipe.NetworkRules![0].Options.Profile,e.Recipe.NetworkRules[0].Options.Search,Reuse:new(1));
        var recipe=e.Recipe with{NetworkRules=null,RouteRules=[rule],AdditionalConnections=[
            new("a-fixed","travel","placement-feedback/fixed-0","placement-feedback/other-0"),new("b-moving","travel","placement-feedback/fixed-0","placement-feedback/sites-0")],
            Corridors=new([new("delivery","travel","test:delivery",0,"connect-at-grade")]),
            Services=new([new("placement-feedback/other-0","need","placement-feedback/fixed-0","stock",[Link("a-fixed")],"Fixed delivery","test:fixed"),
                new("placement-feedback/sites-0","need","placement-feedback/fixed-0","stock",[Link("b-moving")],"Movable delivery","test:moving")],
                [new(Link("a-fixed"),4,"units","Link capacity","test:link"),new(Link("b-moving"),4,"units","Link capacity","test:link")],
                CorridorCapacities:[new("delivery",4,"units","Each physical segment can carry one demand","test:capacity")])};
        var world=new WorldMetadataGenerator().Generate(recipe,catalog);
        Check(world.PlacementPlan!.FeasibilityAttempts!.Any(a=>a.Rejection?.StartsWith("METADATA_SERVICE_CORRIDOR_CAPACITY_EXCEEDED",StringComparison.Ordinal)==true),"physical shared-capacity conflict drives placement reconsideration");
        Check(world.ServicePlan!.Usage.Where(p=>p.Key.StartsWith("segment:",StringComparison.Ordinal)).All(p=>p.Value<=4),"accepted geometry stays within every physical limit");
        new MetadataPlacementPlanner().ValidateDocument(world);new MetadataServicePlanner().ValidateDocument(world);
    }
    public static async Task RunAsync()
    {
        var e=Example();var gen=new WorldMetadataGenerator();var world=gen.Generate(e.Recipe,e.Catalog);var plan=world.ServicePlan!;
        var shared=world.CorridorPlan!.Groups.Single().SharedDependencies.Single().SegmentId;
        Check(plan.Usage["segment:"+shared]==8&&plan.SegmentCapacities!.Count==5,"two distinct logical links consume one physical pool");
        Check(plan.Allocations.All(a=>a.SegmentUses!.Single(u=>u.SegmentId==shared).Quantity==4),"each delivery exposes its shared traversal");
        Check(plan.Usage.Where(p=>p.Key.StartsWith("link:",StringComparison.Ordinal)).All(p=>p.Value==4),"individual links remain within their independent limits");
        var tooSmall=e.Recipe with{Services=e.Recipe.Services! with{CorridorCapacities=[e.Recipe.Services.CorridorCapacities![0] with{CapacityPerSegment=7}]}};
        Reject(()=>gen.Generate(tooSmall,e.Catalog),"METADATA_SERVICE_CORRIDOR_CAPACITY_EXCEEDED");
        var opposite=Example(true);var reversed=gen.Generate(opposite.Recipe,opposite.Catalog);
        Check(reversed.ServicePlan!.Usage["segment:"+shared]==8,"opposite directions consume the same segment pool");
        var unselected=gen.Generate(e.Recipe with{Services=e.Recipe.Services! with{CorridorCapacities=null}},e.Catalog);
        Check(unselected.ServicePlan!.SegmentCapacities is null&&unselected.ServicePlan.Allocations.All(a=>a.SegmentUses is null),"unselected capacity does not infer limits from width");
        Reject(()=>gen.Generate(e.Recipe with{Corridors=null},e.Catalog),"METADATA_SERVICE_CORRIDOR_GROUP_REQUIRED");
        Reject(()=>gen.Generate(e.Recipe with{Services=e.Recipe.Services! with{CorridorCapacities=[e.Recipe.Services.CorridorCapacities![0] with{Unit="other-units"}]}},e.Catalog),"METADATA_SERVICE_CORRIDOR_UNIT_OR_SEGMENT_INVALID");
        Reject(()=>gen.Generate(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="consumer"?a with{Services=Demand() with{Requirements=[Demand().Requirements[0] with{RequirePhysicalRoute=false,MinimumWidth=0}]}}:a).ToArray()}),"METADATA_SERVICE_CORRIDOR_PHYSICAL_REQUIRED");
        new MetadataServicePlanner().ValidateDocument(world);
        var forged=world with{ServicePlan=plan with{Allocations=plan.Allocations.Select((a,i)=>i==0?a with{SegmentUses=[]}:a).ToArray()}};
        Reject(()=>new MetadataServicePlanner().ValidateDocument(forged),"METADATA_SERVICE_PLAN_MISMATCH");
        var ledger=new ServiceCapacityLedger(new Dictionary<string,double>{{"source",8},{"link",4},{"segment",3}});
        Check(!ledger.TryReserve(new Dictionary<string,double>{{"source",4},{"link",4},{"segment",4}})&&ledger.Usage.Values.All(q=>q==0),"physical failure cannot partially reserve source/link capacity");
        var r=WorldRouteReuseTests.Example();var root=r.Catalog.Archetypes[0];var leaf=r.Catalog.Archetypes[1];
        root=root with{Parts=root.Parts.Take(3).Select(p=>p with{ArchetypeId=p.Id=="a"?"supply":p.Id=="b"?"relay":"consumer",Layout=MetadataLineLayout.Parameters([p.Id=="a"?10:p.Id=="b"?90:40,20],[0,0])}).ToArray()};
        var repeatCatalog=r.Catalog with{Archetypes=[root,leaf with{Id="supply",Services=Supply()},leaf with{Id="relay"},leaf with{Id="consumer",Services=Demand()}]};
        string first="corridor-reuse/explicit/a-trunk",second="corridor-reuse/explicit/b-return";
        var repeatRecipe=r.Recipe with{RouteRules=[r.Recipe.RouteRules![0] with{Reuse=null}],AdditionalConnections=[r.Recipe.AdditionalConnections![0],new("b-return","travel","corridor-reuse/b-0","corridor-reuse/c-0")],
            Services=new([new("corridor-reuse/c-0","need","corridor-reuse/a-0","stock",[first,second],"Two legs cross the same physical segment","test:revisit")],
                [new(first,4,"units","First leg","test:first"),new(second,4,"units","Second leg","test:second")],CorridorCapacities:[new("trails",8,"units","Shared traversal budget","test:repeat")])};
        var repeated=gen.Generate(repeatRecipe,repeatCatalog);var twice=repeated.ServicePlan!.Allocations.Single().SegmentUses!.Single(u=>u.Traversals==2);
        Check(twice.Quantity==8&&repeated.ServicePlan.Usage["segment:"+twice.SegmentId]==8,"one delivery charges twice when its chain traverses a segment twice");
        Reject(()=>gen.Generate(repeatRecipe with{Services=repeatRecipe.Services! with{CorridorCapacities=[repeatRecipe.Services.CorridorCapacities![0] with{CapacityPerSegment=7}]}},repeatCatalog),"METADATA_SERVICE_CORRIDOR_CAPACITY_EXCEEDED");
        Feedback();
        string directory=Path.Combine(Path.GetTempPath(),"worldcreator-corridor-capacity-"+Guid.NewGuid().ToString("N"));
        try
        {
            await WriteExampleAsync(directory);var output=Path.Combine(directory,"world");
            await gen.GenerateFilesAsync(Path.Combine(directory,"recipe.json"),Path.Combine(directory,"catalog.json"),output);
            Check(Json(world)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,"world-metadata.json"))),"capacity replays from saved routes and definitions");
        }
        finally{if(Directory.Exists(directory)&&Path.GetDirectoryName(Path.GetFullPath(directory))==Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))&&Path.GetFileName(directory).StartsWith("worldcreator-corridor-capacity-",StringComparison.Ordinal))Directory.Delete(directory,true);}
        Console.WriteLine("PASS: shared physical capacity, exact threshold, opposite directions, atomic reservations, unit/physical/group constraints, unselected behavior, placement feedback and saved replay/tamper. No temporal traffic or independent resilience claim.");
    }
}
