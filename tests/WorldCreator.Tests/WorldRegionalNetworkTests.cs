using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalNetworkTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool v,string message){if(!v)throw new Exception("Regional network: "+message);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Reject(Action run,string prefix){try{run();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing regional-network refusal: "+prefix);}
    internal static MetadataRegionalOwnershipRequest Example(double bogCost=1,bool redundant=false)
    {
        var source=WorldRegionalTopologyTests.Example();var ids=new[]{"arrival","grove","outpost","pass"};
        var regions=source.Regions.Where(r=>ids.Contains(r.Id)).Select(r=>{
            var placement=r.Recipe.Placement!;WorldSuitabilityProfile Eligible(WorldSuitabilityProfile p)=>p with{AllowedKinds=["ground","bog"]};
            WorldPlacementRequest Request(WorldPlacementRequest q)=>q with{Demands=q.Demands.Select(d=>d with{Profile=Eligible(d.Profile)}).ToArray()};
            return r with{Profile=Eligible(r.Profile),Recipe=r.Recipe with{Placement=placement with{Request=Request(placement.Request),
                Programs=placement.Programs!.Select(p=>p with{Request=Request(p.Request)}).ToArray()}}};
        }).ToArray();
        var profile=new WorldTraversalProfile("regional-walk","Terrain-aware travel between complete locations","network:travel",.3,.1,new Dictionary<string,double>{{"ground",1},{"bog",bogCost}},1,.5);
        var options=new WorldNetworkOptions("Required connected regional journeys","network:journeys","overland",profile,new(.1,[]),
            redundant?WorldNetworkConnectivity.SingleConnectionFailure:WorldNetworkConnectivity.Connected,NearestNeighbors:2,MaximumPairChecks:6,MaximumWork:50000000);
        var destinations=ids.Select(id=>new MetadataRegionalNetworkDestination(id,"Reach complete "+id+" program","network:destination:"+id,new(id,"region-west/compounds-0","east"))).ToArray();
        return source with{Regions=regions,Connections=[],Topology=null,MaximumWork=200000000,NetworkRules=[new("regional-journeys","regional-travel",destinations,options)],
            Surface=source.Surface with{Patches=[new("bog-band","bog","Explicit travel-cost terrain band","network:bog",Box(0,35,100,10),10,source.Surface.Terrain!.Origin,2,.01,.005)]}};
    }
    private static void Complete(MetadataRegionalWorldDocument world)
    {
        Check(world.Regions.Count==4&&world.Regions.Sum(r=>r.World.Nodes.Count)==20&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12,"every original compound/module remains");
        Check(world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"both directions of every selected connection qualify");
        Check(world.NetworkPlan?.Networks.Count==world.Definition.NetworkRules!.Count&&world.NetworkPlan.Work>0&&world.NetworkPlan.Work<=world.Work,"network provenance/work retained");
    }
    internal static async Task SemanticAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        Directory.CreateDirectory(output);
        var source=Example();var rule=source.NetworkRules![0];
        var semantic=rule with{Destinations=rule.Destinations.Select(d=>d with{Endpoint=d.Endpoint with{
            NodeId="",Selector=new("compound","compound")}}).ToArray()};
        source=source with{NetworkRules=[semantic]};
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var definition=planner.Plan(source);var world=engine.Generate(definition);Complete(world);
        var witness=world.NetworkPlan!.Networks.Single().ResolvedDestinations!;
        Check(witness.Count==4&&witness.All(d=>d.Endpoint.NodeId=="region-west/compounds-0"),"unique generated nodes resolve into concrete network endpoints");
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"resolved destinations survive source replay");
        var malformed=semantic with{Destinations=semantic.Destinations.Select((d,i)=>i==0?d with{Endpoint=d.Endpoint with{NodeId="explicit"}}:d).ToArray()};
        Reject(()=>planner.Plan(source with{NetworkRules=[malformed]}),"METADATA_REGIONAL_NETWORK_DESTINATION_INVALID");
        var absent=semantic with{Destinations=semantic.Destinations.Select((d,i)=>i==0?d with{Endpoint=d.Endpoint with{Selector=new("absent")}}:d).ToArray()};
        Reject(()=>engine.Generate(planner.Plan(source with{NetworkRules=[absent]})),"METADATA_REGIONAL_NETWORK_SELECTOR_COUNT:");
        var corrupt=witness.Select((d,i)=>i==0?d with{Endpoint=d.Endpoint with{NodeId="forged"}}:d).ToArray();
        Reject(()=>engine.ValidateDocument(world with{NetworkPlan=world.NetworkPlan with{Networks=[world.NetworkPlan.Networks[0] with{ResolvedDestinations=corrupt}]}}),"METADATA_REGIONAL_DOCUMENT_REPLAY_MISMATCH");
        Console.WriteLine("PASS: generated semantic regional endpoints, physical routes, missing and malformed selectors, witness tampering, and saved replay.");
    }
    internal static async Task SemanticTerrainAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var source=WorldRegionalCouplingTests.Example();var original=source.Connections[0];
        var options=new WorldNetworkOptions("Connect generated regional roles","semantic:network","regional",original.Profile,original.Search,
            NearestNeighbors:1,MaximumPairChecks:1,MaximumWork:50000000);
        var rule=new MetadataRegionalNetworkRule("dynamic-roles","role-journey",[
            new("alpha","First activity area","semantic:alpha",new("alpha","","east",new("activity-area","compound"))),
            new("beta","Second activity area","semantic:beta",new("beta","","west",new("activity-area","compound")))],options);
        source=source with{NetworkRules=[rule]};
        var engine=new MetadataRegionalWorldGenerator();var definition=new MetadataRegionalOwnershipPlanner().Plan(source);
        var world=engine.Generate(definition);var bound=world.NetworkPlan!.Networks.Single().ResolvedDestinations!;
        Check(world.Regions.Count==3&&world.Connections.Count==3,"all generated regions and both original plus new routes survive");
        foreach(var region in world.Regions){
            var count=definition.OwnershipPlan!.ProgramDemands!.Single(d=>d.RegionId==region.RegionId).Analysis.TerrainAnalysis!.Plan.Results.Single().Count;
            Check(count>=4&&region.World.Nodes.Count==16+count&&region.World.Nodes.Count(n=>n.Kind=="discovery")==count,"terrain-dependent required content retained");
        }
        Check(bound.Count==2&&bound.All(b=>world.Regions.Single(r=>r.RegionId==b.Endpoint.RegionId).World.Nodes.Any(n=>n.Id==b.Endpoint.NodeId&&n.Kind=="activity-area")),"roles resolve against generated nodes");
        Check(world.Connections.All(c=>c.Forward.Status==WorldRouteStatus.Found&&c.Reverse?.Status==WorldRouteStatus.Found),"all routes qualify both directions");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));
        await engine.PublishAsync(world,Path.Combine(output,"world"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"terrain-dependent saved world replays");
        var template=source with{NetworkRules=[rule with{Destinations=[
            rule.Destinations[0] with{Endpoint=original.From},rule.Destinations[1] with{Endpoint=original.To}]}]};
        var alpha=template.Regions.Single(r=>r.Id=="alpha");
        var request=new MetadataRegionalDesignRequest(1,"dynamic-semantic-design","Compose terrain-dependent connected programs","semantic:design",template,
            [new("alpha",[new("baseline",0,alpha.Recipe,alpha.Catalog)])],
            [new("required-discoveries","alpha","discovery",4,1024,true)],Bindings:[
                new("dynamic-roles","alpha","activity-area","compound"),new("dynamic-roles","beta","activity-area","compound")]);
        var designer=new MetadataRegionalDesignGenerator();var design=designer.Generate(request);
        Check(design.Receipt.Attempts.Single().Accepted&&design.Receipt.Attempts.Single().BoundEndpoints!.Count==2,"designer binds roles after terrain-dependent composition");
        Check(design.World.Regions.Sum(r=>r.World.Nodes.Count)==world.Regions.Sum(r=>r.World.Nodes.Count)&&design.World.Connections.Count==3,"designer keeps complete dynamic content and routes");
        await designer.PublishAsync(design,Path.Combine(output,"design"));
        Check(Json(await designer.ReadAsync(Path.Combine(output,"design")))==Json(design),"dynamic design and resolved endpoints replay");
        Console.WriteLine("PASS: semantic regional connections resolve after terrain-dependent content generation, with complete regional content and two-way physical paths.");
    }
    internal static async Task ComposedAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var source=Example();var topology=WorldRegionalTopologyTests.Example().Topology!;
        string Node(string id)=>MetadataTopologyPlanner.RegionalDestinationId(source.Id,id);
        var rule=topology.Rules[0];var stages=rule.Topology with{ExtraConnections=1,Stages=[
            new("first","Keep the required transition",Node("grove"),["early-site"],1,1),new("last","Reach the final destination",Node("pass"),[],0,0)]};
        source=source with{Topology=topology with{Destinations=topology.Destinations.Where(d=>source.Regions.Any(r=>r.Id==d.Endpoint.RegionId)).ToArray(),
            Rules=[rule with{Topology=stages,Profile=source.NetworkRules![0].Options.Profile}]},PhysicalFeedback=new("Qualify both declared channels","network:composed",1)};
        var definition=new MetadataRegionalOwnershipPlanner().Plan(source);var engine=new MetadataRegionalWorldGenerator();var world=engine.Generate(definition);Complete(world);
        Check(definition.Connections.Count==4&&world.Connections.Count==7,"four staged links and three terrain-selected links retain independent kind ownership");
        Check(definition.OwnershipPlan!.PhysicalFeedbackPlan!.Attempts.Single().Accepted&&definition.TopologyPlan is not null,"real physical feedback qualifies the complete combined pipeline");
        await engine.PublishAsync(world,Path.Combine(output,"world"));Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(world),"complete composed source/export replay");
        await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));
        Console.WriteLine("PASS: staged topology, terrain-selected network and physical ownership feedback compose in one complete20-node/12-source world with seven links/14 directed regional paths and exact saved replay.");
    }
    internal static void Preflight()
    {
        var source=Example();var rule=source.NetworkRules![0];var domain=Box(10,10,30,20);
        var guidance=new WorldRouteGuidance("outside-scope","Retain declared scope","network:guidance",Box(50,50,10,10),[new(1,0)],1);
        var invalid=source with{NetworkRules=[rule with{RoutingDomain=domain,Options=rule.Options with{Profile=rule.Options.Profile with{Guidance=[guidance]}}}]};
        Reject(()=>new MetadataRegionalOwnershipPlanner().Plan(invalid),"WORLD_ROUTE_GUIDANCE_OUTSIDE_DOMAIN:");
        var valid=invalid with{NetworkRules=invalid.NetworkRules!.Select(r=>r with{Options=r.Options with{Profile=r.Options.Profile with{Guidance=[guidance with{Boundary=Box(12,12,10,10)}]}}}).ToArray()};
        Check(new MetadataRegionalOwnershipPlanner().Plan(valid).NetworkRules!.Single().Options.Profile.Guidance!.Single().Boundary.ContainsPoint(new(15,15)),"valid local guidance survives exact preflight");
        var impossible=rule with{Options=rule.Options with{ForbiddenConnections=[new("arrival","grove"),new("arrival","outpost"),new("arrival","pass")]}};
        Reject(()=>new MetadataRegionalOwnershipPlanner().Plan(source with{NetworkRules=[impossible],MaximumWork=1}),"METADATA_REGIONAL_NETWORK_ALLOWED_GRAPH_DISCONNECTED:");
        var bridge=rule with{Options=rule.Options with{Connectivity=WorldNetworkConnectivity.SingleConnectionFailure,
            ForbiddenConnections=[new("arrival","grove"),new("arrival","outpost")]}};
        Reject(()=>new MetadataRegionalOwnershipPlanner().Plan(source with{NetworkRules=[bridge],MaximumWork=1}),"METADATA_REGIONAL_NETWORK_ALLOWED_GRAPH_NOT_REDUNDANT:");
        var feasible=rule with{Options=rule.Options with{Connectivity=WorldNetworkConnectivity.SingleConnectionFailure,ForbiddenConnections=[new("arrival","grove")]}};
        Check(new MetadataRegionalOwnershipPlanner().Plan(source with{NetworkRules=[feasible]}).NetworkRules![0].Options.ForbiddenConnections!.Count==1,"structurally feasible constrained network survives preflight");
        Console.WriteLine("PASS: scoped guidance and allowed-graph connectivity preflight, including impossible direct-link policies before world expansion.");
    }
    internal static async Task RunAsync(string output,bool smoke=false)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var source=Example();string original=Json(source);var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        var originalTerrain=new WorldSurfaceQuery(source.Surface with{Patches=[]});var costTerrain=new WorldSurfaceQuery(source.Surface);
        foreach(var point in new[]{new WorldPoint(32,34),new WorldPoint(32,35),new WorldPoint(32,40),new WorldPoint(82,45)})
            Check(Math.Abs(originalTerrain.Sample(point).Elevation!.Value-costTerrain.Sample(point).Elevation!.Value)<1e-10,"terrain cost band preserves original elevation plane");
        var definition=planner.Plan(source);var world=engine.Generate(definition);Complete(world);Check(Json(source)==original,"source immutable");
        Check(definition.Connections.Count==0&&world.Connections.Count==3,"solver selects exactly a connected sparse backbone from real regional endpoints");
        await engine.PublishAsync(world,Path.Combine(output,"initial"));await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(source));
        var selected=world.NetworkPlan!.Networks[0].Plan.Edges[0].Link;
        var rule=source.NetworkRules![0];
        var restrictedSource=source with{NetworkRules=[rule with{Options=rule.Options with{ForbiddenConnections=[new(selected.ToId,selected.FromId)]}}]};
        var restrictedDefinition=planner.Plan(restrictedSource);var restricted=engine.Generate(restrictedDefinition);Complete(restricted);
        Check(restricted.NetworkPlan!.Networks[0].Plan.Attempts.All(a=>!(a.Link.FromId==selected.FromId&&a.Link.ToId==selected.ToId)),"forbidden regional pair never routed");
        Check(restricted.Connections.Count==3,"forbidden pair keeps complete connected world via other links");
        await engine.PublishAsync(restricted,Path.Combine(output,"restricted"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"restricted/world-regions.json")))==Json(restricted),"restricted regional world replays");
        Reject(()=>planner.Plan(source with{NetworkRules=[rule with{Options=rule.Options with{ForbiddenConnections=[new("unknown","grove")]}}]}),"METADATA_REGIONAL_NETWORK_FORBIDDEN_INVALID");
        Reject(()=>planner.Plan(source with{NetworkRules=[rule with{Options=rule.Options with{RequiredConnections=[new("arrival","grove")],ForbiddenConnections=[new("grove","arrival")]}}]}),"METADATA_REGIONAL_NETWORK_REQUIRED_FORBIDDEN_CONFLICT");
        if(smoke){Console.WriteLine("PASS: complete regional programs, restricted direct connection, connected alternative and saved replay.");return;}
        var changedSource=Example(9);var changed=planner.Plan(changedSource);var changedWorld=engine.Generate(changed);Complete(changedWorld);
        string Pairs(MetadataRegionalWorldDocument w)=>Json(w.NetworkPlan!.Networks[0].Plan.Edges.Select(e=>new{e.Link.FromId,e.Link.ToId}).OrderBy(e=>e.FromId).ThenBy(e=>e.ToId));
        Check(Pairs(world)!=Pairs(changedWorld),"terrain-kind travel cost changes which regional links are selected");
        Check(Json(world.Regions)==Json(changedWorld.Regions),"travel-cost edit preserves exact complete local source packets");
        await engine.PublishAsync(changedWorld,Path.Combine(output,"cost-changed"));await File.WriteAllTextAsync(Path.Combine(output,"cost-request.json"),Json(changedSource));
        var reversed=source with{Regions=source.Regions.Reverse().ToArray(),NetworkRules=source.NetworkRules!.Select(r=>r with{Destinations=r.Destinations.Reverse().ToArray()}).ToArray()};
        Check(Json(planner.Plan(reversed))==Json(definition),"source declarations canonical");
        Check(Json(engine.Assemble(definition,world.Regions.Reverse().ToArray()))==Json(world),"reversed independent packet assembly equals whole world");
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"initial/world-regions.json")))==Json(world),"read replays all candidates, costs and selected physical routes");
        var robust=engine.Generate(planner.Plan(Example(redundant:true)));Complete(robust);Check(robust.NetworkPlan!.Networks[0].Plan.Analysis.Bridges.Count==0,"declared single-link-failure resilience");
        await engine.PublishAsync(robust,Path.Combine(output,"redundant"));
        var main=source.NetworkRules![0];var within=new MetadataRegionalNetworkRule("within-program","opening-link",
            [new("east","East opening","network:opening",new("arrival","region-west/compounds-0","east")),new("west","West opening","network:opening",new("arrival","region-west/compounds-0","west"))],
            main.Options with{Scale="program",MaximumPairChecks=1,NearestNeighbors=1},RoutingDomain:Box(10,10,30,20));
        var multipleSource=source with{NetworkRules=[main,within]};var multiple=engine.Generate(planner.Plan(multipleSource));Complete(multiple);
        Check(multiple.NetworkPlan!.Networks.Count==2&&multiple.Connections.Any(c=>c.Definition.From.RegionId==c.Definition.To.RegionId),"independent channels and multiple exact destinations in one region");
        await engine.PublishAsync(multiple,Path.Combine(output,"multiple"));
        var session=await MetadataRegionalGenerationSession.StartDerivedAsync(new(),source);var initial=session.Snapshot;
        MetadataRegionalSourceEditRequest Edit(string id,MetadataRegionalOwnershipRequest s)=>new(id,"Revise declared travel design","network:edit",session.Snapshot.World.DefinitionSha256,session.Snapshot.Report.WorldSha256,s);
        var accepted=await session.ApplyDerivedAndPublishAsync(Edit("cost-design",changedSource),Path.Combine(output,"session"));
        Check(Json(accepted.World)==Json(changedWorld)&&accepted.Report.Regions.All(r=>r.Reused),"actual source edit recomputes the network and reuses all four complete local packets");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(new(),Path.Combine(output,"session/generation-session.json"));Check(Json(restored.Snapshot)==Json(accepted),"complete source history restores");
        var blocked=source with{Surface=source.Surface with{Exclusions=[new("barrier","Required hard terrain barrier","network:barrier",Box(0,35,100,10))]},
            NetworkRules=[main with{Options=main.Options with{RequiredConnections=[new("arrival","grove")]}}]};
        try{engine.Generate(planner.Plan(blocked));throw new Exception("Missing hard required-link refusal");}
        catch(MetadataRegionalNetworkInfeasibleException e){Check(e.Plan.Status==WorldNetworkStatus.RequiredConnectionUnresolved&&e.Plan.Attempts.Count==1&&e.Work>0,"actual required-link failure witness");await File.WriteAllTextAsync(Path.Combine(output,"required-failure.json"),Json(e.Plan));}
        try{await session.ApplyDerivedAndPublishAsync(Edit("blocked",blocked),Path.Combine(output,"rejected"));throw new Exception("Missing failed source edit");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted)&&!Directory.Exists(Path.Combine(output,"rejected")),"failed world redesign preserves accepted snapshot and output");
        var rollback=await session.ApplyDerivedAsync(Edit("rollback",source));Check(Json(rollback.World)==Json(initial.World),"exact complete world rollback");
        await File.WriteAllTextAsync(Path.Combine(output,"blocked-request.json"),Json(blocked));
        Reject(()=>planner.Plan(source with{NetworkRules=[main with{ConnectionKind="bad kind"}]}),"METADATA_REGIONAL_NETWORK_RULE_INVALID");
        Reject(()=>planner.Plan(source with{NetworkRules=[main,main]}),"METADATA_REGIONAL_NETWORK_RULE_INVALID");
        Reject(()=>planner.Plan(source with{NetworkRules=[main with{Options=main.Options with{RequiredConnections=[new("unknown","grove")]}}]}),"METADATA_REGIONAL_NETWORK_REQUIRED_INVALID");
        Reject(()=>engine.Generate(definition with{NetworkRules=[main with{Options=main.Options with{MaximumPairChecks=1}}],OwnershipPlan=null,OwnershipDerivationId=null}),"WORLD_NETWORK_PAIR_BUDGET:");
        Reject(()=>engine.Generate(definition with{MaximumTotalConnections=4,OwnershipPlan=null,OwnershipDerivationId=null}),"METADATA_REGIONAL_CONTENT_BUDGET");
        Reject(()=>engine.ValidateDocument(world with{NetworkPlan=world.NetworkPlan! with{Work=world.NetworkPlan.Work+1}}),"METADATA_REGIONAL_DOCUMENT_REPLAY_MISMATCH");
        Reject(()=>engine.ValidateDocument(world with{Connections=world.Connections.Skip(1).ToArray()}),"METADATA_REGIONAL_DOCUMENT_REPLAY_MISMATCH");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{engine.Generate(definition,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
        Console.WriteLine("PASS: terrain-aware regional network selection, complete20-node/12-source worlds, cost-driven graph redesign with exact local reuse, redundancy, two independent scales, same-region destinations, required-link failure witness, input/route/budget/tamper refusal, source history and exact rollback.");
    }
}
