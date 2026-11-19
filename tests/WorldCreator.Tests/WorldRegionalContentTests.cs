using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalContentTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Regional content: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing "+code);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private sealed class Probe : IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>"request-probe";public int Calls;public List<string> Regions=[];
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.EmptyConfigurationSha256;
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();
            // The common schema validator probes registered layouts with zero
            // members. Observe actual content expansion separately from that.
            if(count>0){Calls++;Regions.Add(parameters.GetProperty("Region").GetString()!);}
            return Enumerable.Range(0,count).Select(_=>(IReadOnlyList<double>)new double[dimensions]).ToArray();
        }
    }
    private sealed class MutableLayout(string id,Action? duringExpansion=null) : IMetadataLayout,IWorldGenerationDependency
    {
        public string Id=>id;public int Version;
        public string ConfigurationSha256=>WorldGenerationDependencyIdentity.Digest(new{Version});
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();if(count>0)duringExpansion?.Invoke();
            return Enumerable.Range(0,count).Select(_=>(IReadOnlyList<double>)new[]{(double)Version,0}).ToArray();
        }
    }
    private static WorldTraversalProfile Walk(double radius=.3)=>new("walk","Required full-width regional travel","regional:walk",radius,.1,new Dictionary<string,double>{{"ground",1}},1,.5);
    internal static MetadataRegionalWorldDefinition Portable(MetadataRegionalWorldDefinition definition)=>definition with{Regions=definition.Regions.Select(r=>r with{Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a with{
        Parts=a.Parts.Select(p=>p.Layout?.StrategyId=="request-probe"?p with{Layout=MetadataLineLayout.Parameters([0,0],[1,0])}:p).ToArray()}).ToArray()}}).ToArray()};
    private static MetadataGenerationRegion Region(string id,WorldPolygon ownership,WorldPolygon domain,double x,int modules=2,double span=4)
    {
        var fields=new WorldEnvironmentDefinition(1,"height",[new("height","Altitude","regional:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var profile=new WorldSuitabilityProfile("usable","Required usable ground","regional:usable",["ground"],[new("altitude","height","Allowed height","regional:height",0,20)],[]);
        MetadataCatalogAccess Access(double half)=>new(MetadataOccupancyKind.Reservation,[new("east","Eastern opening","regional:port",new(half,0),new(half+1,0),1.2),new("west","Western opening","regional:port",new(-half,0),new(-half-1,0),1.2)]);
        var compoundAccess=Access(6);var moduleAccess=Access(.5) with{Occupancy=MetadataOccupancyKind.Solid};
        WorldMetadataArchetype Type(string type,string kind,IReadOnlyList<MetadataPart> parts,IReadOnlyList<MetadataChildRequirement> requirements,MetadataCatalogFootprint? shape=null,MetadataCatalogAccess? access=null)
            =>new(type,kind,"Required "+kind,new Dictionary<string,JsonElement>(),parts,[],requirements,LayerId:"intent",PhysicalFootprint:shape,Access:access);
        var root=Type("root","program-region",[new("compounds","compound",1,"Complete compound"),new("markers","marker",1,"Request observation",Layout:new("request-probe",JsonSerializer.SerializeToElement(new{Region=id})))],[new("compound",1,1),new("marker",1,1)]);
        var compound=Type("compound","compound",[new("modules","module",modules,"All required modules")],[new("module",modules,modules)],new("meters","Full source compound","regional:compound",Box(-6,-5,12,10)),compoundAccess);
        var module=Type("module","module",[],[],new("meters","Unchanged module source","regional:module",Box(-.5,-.75,1,1.5)),moduleAccess);
        var marker=Type("marker","marker",[],[]);var catalog=new WorldMetadataCatalog(1,"regional-catalog",[root,compound,module,marker]);string recipeId="region-"+id;
        string parent=recipeId+"/compounds-0";
        var rootRequest=new WorldPlacementRequest("compound","Retain the exact seam control","regional:placement",null,[new("compound","Required compound","regional:compound",null,profile)],
            [new("fixed",new(x,30))],[new("pin","compound","fixed","Required source position","regional:pin",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
        var program=new MetadataNestedProgram("modules","Generate every required nested member","regional:modules",recipeId,"compound",1,1,
            new("modules","Source-sized modules inside the compound","regional:modules",null,[new("module","All required modules","regional:module",null,profile)],[],[],MaximumWork:1000000),
            [new("module","modules",Population:new("Purposeful source path","regional:module-path",Path:new(new([new(-span/2,0),new(span/2,0)]),span/(modules-1))))]);
        var recipe=new WorldMetadataRecipe(1,recipeId,"framework-reference",catalog.Id,"root","generation-region",MetadataCoverage.PartialScope,
            ["Shared-coordinate content generation control; complete game design, artwork and Godot remain separate."],new Dictionary<string,int>(),new(["x","y"],"meters"),[id=="west"?20:80,20],true,
            AdditionalConnections:[new("local","local-walk",parent+"/modules-0",parent+"/modules-"+(modules-1),true)],
            Layers:[new("intent","program","All required content","regional:content",[],MetadataCoverage.CompleteDeclaredScope,[])],
            RouteRules:[new("local","local-walk",Walk(.2),new(.1,[]),"east","west")],Environment:fields,
            Placement:new(rootRequest,[new("compound",parent,true)],Programs:[program]));
        return new(id,"Complete "+id+" content","regional:"+id,ownership,domain,recipe,catalog);
    }
    internal static MetadataRegionalWorldDefinition Example()
    {
        var west=new WorldPolygon([new(0,0),new(50,0),new(50,20),new(45,40),new(50,60),new(50,80),new(0,80)]);
        var east=new WorldPolygon([new(50,0),new(100,0),new(100,80),new(50,80),new(50,60),new(45,40),new(50,20)]);
        var surface=new WorldSurfaceDefinition(1,"shared-land","meters",Box(0,0,100,80),[],[],
            new(1,"source","ground","Shared global source","regional:source","meters",new(-3.1,-7.3),2.5,2.5,513,513,2,.01,.005,[]));
        return new(1,"regional-world","Independently requested complete physical programs","regional:world",surface,
            [Region("west",west,Box(0,0,60,80),43),Region("east",east,Box(35,0,65,80),70)],
            [new("seam-journey","travel","Connect actual compound ports across the seam","regional:journey",new("west","region-west/compounds-0","east"),
                new("east","region-east/compounds-0","west"),"west",surface.Boundary!,Walk(),new(.1,[]),true)]);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var probe=new Probe();var engine=new MetadataRegionalWorldGenerator(new WorldMetadataGenerator(layouts:[new MetadataLineLayout(),new MetadataPathLayout(),new MetadataArcLayout(),probe]));
        var definition=Example();string input=Json(definition);
        var invalidOpening=definition with{Regions=definition.Regions.Select(r=>r.Id!="west"?r:r with{Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a.Id!="module"?a:a with{
            PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-.5,-.5,1,1)}}).ToArray()}}).ToArray()};
        Reject(()=>engine.GenerateRegion(invalidOpening,"west"),"METADATA_ACCESS_BOUNDARY_OPENING_INVALID");probe.Calls=0;probe.Regions.Clear();
        var west=engine.GenerateRegion(definition,"west");int westCalls=probe.Calls;Check(westCalls>0&&probe.Regions.All(r=>r=="west"),"one request expands only its selected region: calls="+westCalls+", regions="+Json(probe.Regions));
        var east=engine.GenerateRegion(definition,"east");Check(probe.Calls>westCalls&&probe.Regions.Skip(westCalls).All(r=>r=="east"),"second region generates independently");
        var assembled=engine.Assemble(definition,[east,west]);var whole=engine.Generate(definition);
        Check(Json(assembled)==Json(whole),"request order equals whole generation including geometry, ownership, routes and work");
        Check(whole.Regions.Sum(r=>r.World.Nodes.Count)==10&&whole.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==6,"all required nested sources retained");
        var seam=whole.Members.Single(m=>m.Id=="region-west/compounds-0");Check(seam.OwnerRegionId=="west"&&seam.RegionIds.SequenceEqual(new[]{"east","west"}),"one whole compound has exact cross-seam references");
        Check(whole.Connections.Single().Forward.Status==WorldRouteStatus.Found&&whole.Connections.Single().Reverse!.Status==WorldRouteStatus.Found,"both physical cross-region directions qualified");
        Check(whole.Regions.All(r=>r.World.Surface!.HeightField is null&&r.World.Surface.TerrainCapturePlan!.Window!.Heights.Count<263169),"each request stores only required original global terrain");
        Reject(()=>engine.Assemble(definition,[west]),"METADATA_REGIONAL_PACKETS_REQUIRED");
        Reject(()=>engine.Assemble(definition,[west,west]),"METADATA_REGIONAL_PACKETS_REQUIRED");
        Reject(()=>engine.Assemble(definition with{MaximumTotalNodes=9},[west,east]),"METADATA_REGIONAL_CONTENT_BUDGET");
        Reject(()=>engine.Generate(definition with{MaximumTotalConnections=2}),"METADATA_REGIONAL_CONTENT_BUDGET");
        Reject(()=>engine.Assemble(definition with{MaximumReferences=1},[west,east]),"METADATA_REGIONAL_REFERENCE_BUDGET");
        Reject(()=>engine.ReplayRegion(definition,west with{ImplementationId="stale"}),"METADATA_REGIONAL_PACKET_SOURCE_MISMATCH");
        Reject(()=>engine.ReplayRegion(definition,west with{World=west.World with{Nodes=west.World.Nodes.SkipLast(1).ToArray()}}),"METADATA_REGIONAL_PACKET_REPLAY_MISMATCH");
        var gap=definition with{Regions=[definition.Regions[0] with{Ownership=Box(0,0,40,80)},definition.Regions[1]]};
        Reject(()=>engine.GenerateRegion(gap,"west"),"METADATA_REGIONAL_OWNERSHIP_GAP");
        var overlap=definition with{Regions=[definition.Regions[0] with{Ownership=Box(0,0,60,80)},definition.Regions[1]]};
        Reject(()=>engine.GenerateRegion(overlap,"west"),"METADATA_REGIONAL_OWNERSHIP_OVERLAP");
        using(var c=new CancellationTokenSource()){c.Cancel();try{engine.GenerateRegion(definition,"west",c.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        Check(Json(definition)==input,"requests do not mutate authored definitions");
        var blockedApproach=definition with{Regions=[Region("west",definition.Regions[0].Ownership,definition.Regions[0].GenerationDomain,43,3),definition.Regions[1]]};
        Reject(()=>engine.GenerateRegion(blockedApproach,"west"),"METADATA_PLACEMENT_UNRESOLVED");
        var richer=definition with{Regions=[Region("west",definition.Regions[0].Ownership,definition.Regions[0].GenerationDomain,43,3,8),definition.Regions[1]]};
        var richWest=engine.GenerateRegion(richer,"west");var rich=engine.Assemble(richer,[east,richWest]);
        Check(rich.Regions.Sum(r=>r.World.Nodes.Count)==11&&Json(rich.Regions.Single(r=>r.RegionId=="east"))==Json(east),"local content edit retains richer required count and accepts exact unaffected packet");
        Check(Json(rich)==Json(engine.Generate(richer)),"local content edit assembly equals clean whole generation");
        var collision=definition with{Regions=[definition.Regions[0],Region("east",definition.Regions[1].Ownership,definition.Regions[1].GenerationDomain,45)]};
        var collidingEast=engine.GenerateRegion(collision,"east");
        Reject(()=>engine.Assemble(collision,[west,collidingEast]),"METADATA_REGIONAL_PHYSICAL_OVERLAP");
        var blocked=definition with{Surface=definition.Surface with{Patches=[new("barrier","blocked","Declared impassable seam barrier","regional:barrier",Box(54,0,2,80),1,new(54,0),2,0,0)]}};
        Reject(()=>engine.Generate(blocked),"METADATA_REGIONAL_CONNECTION_UNRESOLVED");
        Check(engine.Assemble(definition with{MaximumWork=whole.Work},[east,west]).Work==whole.Work,"exact shared preparation/assembly work allowance");
        Reject(()=>engine.Assemble(definition with{MaximumWork=whole.Work-1},[east,west]),"METADATA_REGIONAL_WORK_BUDGET");
        await engine.PublishRegionAsync(definition,west,Path.Combine(output,"west"));
        await engine.PublishRegionAsync(definition,east,Path.Combine(output,"east"));
        var loadedEast=await engine.ReadRegionAsync(Path.Combine(output,"east"));var loadedWest=await engine.ReadRegionAsync(Path.Combine(output,"west"));
        Check(Json(engine.Assemble(definition,[loadedEast,loadedWest]))==Json(whole),"independent reload order retains complete world geometry and sources");
        Check(Json(await engine.ReadRegionAsync(Path.Combine(output,"east"),richer))==Json(east),"unchanged saved packet remains eligible after a different region's local edit");
        await engine.PublishAsync(whole,Path.Combine(output,"world"));
        Check((await File.ReadAllTextAsync(Path.Combine(output,"world/index.html"))).Contains("href=\"schematic.html\""),"publication links combined world inspection");
        Check(File.Exists(Path.Combine(output,"world/schematic.html"))&&File.Exists(Path.Combine(output,"world/regional-schematic.js")),"combined inspector uses the current packaged regional renderer");
        await engine.PublishAsync(rich,Path.Combine(output,"richer"));
        Check(Json(await engine.ReadWorldAsync(Path.Combine(output,"world/world-regions.json")))==Json(whole),"normal bounded JSON and full source replay authenticate assembled publication");
        try{await engine.PublishRegionAsync(definition,west,Path.Combine(output,"west"));throw new Exception("Missing existing-output refusal");}catch(IOException e)when(e.Message=="METADATA_OUTPUT_EXISTS"){}
        Check(Json(await engine.ReadRegionAsync(Path.Combine(output,"west")))==Json(west),"existing-output refusal preserves accepted region packet");
        using(var c=new CancellationTokenSource()){c.Cancel();try{await engine.PublishRegionAsync(definition,west,Path.Combine(output,"cancelled"),c.Token);throw new Exception("Missing cancelled publication");}catch(OperationCanceledException){}}
        Check(!Directory.Exists(Path.Combine(output,"cancelled")),"cancelled publication leaves no accepted output");
        var portable=Portable(definition);
        var ordinary=new MetadataRegionalWorldGenerator();var portableWorld=ordinary.Generate(portable);
        Check(portableWorld.Regions.Sum(r=>r.World.Nodes.Count)==10,"portable public API retains complete content without test extensions");
        await ordinary.PublishAsync(portableWorld,Path.Combine(output,"portable"));
        var template=portable.Regions[0];var repeated=portable with{Regions=[template,portable.Regions[1] with{Catalog=template.Catalog,Recipe=template.Recipe with{Origin=[80,20],
            Placement=template.Recipe.Placement! with{Request=template.Recipe.Placement.Request with{Anchors=[new("fixed",new(70,30))]}}}}],
            Connections=portable.Connections.Select(c=>c with{To=c.To with{NodeId="region-west/compounds-0"}}).ToArray()};
        var repeatedWorld=ordinary.Generate(repeated);
        Check(repeatedWorld.Regions.Sum(r=>r.World.Nodes.Count)==10&&repeatedWorld.Members.Count(m=>m.Kind==MetadataSpatialMemberKind.Node&&m.Id=="region-west/compounds-0")==2,
            "one reusable recipe template keeps both complete instances with region-qualified identities");
        Check(repeatedWorld.Members.Select(m=>(m.OwnerRegionId,m.Kind,m.Id)).Distinct().Count()==repeatedWorld.Members.Count,"canonical regional addresses do not alias template instances");
        await ordinary.PublishAsync(repeatedWorld,Path.Combine(output,"repeated"));
        var mutableEast=new MutableLayout("mutable-east");var mutatingWest=new MutableLayout("mutating-west",()=>mutableEast.Version++);
        var unstable=portable with{Regions=portable.Regions.Select(r=>r with{Catalog=r.Catalog with{Archetypes=r.Catalog.Archetypes.Select(a=>a with{Parts=a.Parts.Select(p=>p.Layout is null?p:p with{
            Layout=p.Layout with{StrategyId=r.Id=="east"?mutableEast.Id:mutatingWest.Id}}).ToArray()}).ToArray()}}).ToArray()};
        var unstableEngine=new MetadataRegionalWorldGenerator(new WorldMetadataGenerator(layouts:[new MetadataLineLayout(),new MetadataPathLayout(),new MetadataArcLayout(),mutableEast,mutatingWest]));
        Reject(()=>unstableEngine.Generate(unstable),"METADATA_GENERATION_DEPENDENCY_CHANGED_DURING_RUN");
        await Save("portable-definition.json",portable);await Save("collision.definition.json",Portable(collision));await Save("blocked.definition.json",Portable(blocked));
        await Save("repeated-template.definition.json",repeated);await Save("repeated-template-world.json",repeatedWorld);
        await Save("definition.json",definition);await Save("west-region.json",west);await Save("east-region.json",east);await Save("world-regions.json",whole);await Save("richer-world-regions.json",rich);
        await Save("invalid-opening.definition.json",invalidOpening);
        await Save("blocked-approach.definition.json",blockedApproach);
        Console.WriteLine("PASS: independently requested same-coordinate regions, complete10-node/6-source nested program, irregular partition, whole seam-spanning compound, full-width outward/return routes, order/whole equality, source/tamper/missing/gap/overlap/cancellation refusal and richer local content edit with exact unaffected packet.");
        async Task Save(string name,object value)=>await File.WriteAllTextAsync(Path.Combine(output,name),Json(value));
    }

    internal static async Task ReadAndScaleAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var engine=new MetadataRegionalWorldGenerator();var source=Portable(Example());var observed=new List<object>();
        var original=await engine.ReadWorldAsync(Environment.GetEnvironmentVariable("WORLD_REGIONAL_PACKET")??"generated/regional-content-verified-cli-whole/world-regions.json");
        var changed=original with{Members=original.Members.SkipLast(1).ToArray()};string tampered=Path.Combine(output,"tampered-world.json");
        await File.WriteAllTextAsync(tampered,Json(changed));
        try{await engine.ReadWorldAsync(tampered);throw new Exception("Missing persisted ownership refusal");}catch(ArgumentException e)when(e.Message=="METADATA_REGIONAL_DOCUMENT_REPLAY_MISMATCH"){}
        foreach(int count in new[]{4,8,16})
        {
            double width=count*50;var bounds=Box(0,0,width,80);var regions=new List<MetadataGenerationRegion>();
            for(int i=0;i<count;i++)
            {
                double left=i*50,right=(i+1)*50;
                var points=new List<WorldPoint>{new(left,0),new(right,0)};
                if(i<count-1)points.AddRange([new(right,20),new(right-5,40),new(right,60)]);
                points.AddRange([new(right,80),new(left,80)]);
                if(i>0)points.AddRange([new(left,60),new(left-5,40),new(left,20)]);
                var ownership=new WorldPolygon(points);var domain=Box(Math.Max(0,left-10),0,Math.Min(width,right+10)-Math.Max(0,left-10),80);
                var template=source.Regions[0];var recipe=template.Recipe with{Origin=[left+10,20],Placement=template.Recipe.Placement! with{
                    Request=template.Recipe.Placement.Request with{Anchors=[new("fixed",new(left+20,30))]}}};
                regions.Add(template with{Id="region-"+i.ToString("D2"),Ownership=ownership,GenerationDomain=domain,Recipe=recipe});
            }
            var definition=source with{Surface=source.Surface with{Boundary=bounds},Regions=regions,
                Connections=Enumerable.Range(0,count-1).Select(i=>source.Connections[0] with{Id="journey-"+i.ToString("D2"),
                    From=new(regions[i].Id,"region-west/compounds-0","east"),To=new(regions[i+1].Id,"region-west/compounds-0","west"),
                    OwnerRegionId=regions[i].Id,RoutingDomain=Box(i*50,0,100,80)}).ToArray()};
            long allocated=GC.GetTotalAllocatedBytes(true);var watch=System.Diagnostics.Stopwatch.StartNew();
            var world=engine.Generate(definition);watch.Stop();long bytes=GC.GetTotalAllocatedBytes(true)-allocated;
            Check(world.Regions.Sum(r=>r.World.Nodes.Count)==count*5&&world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==count*3,"scale retains complete required programs and source-sized bodies");
            Check(world.Connections.Count==count-1&&world.Connections.All(c=>c.Reverse is not null),"every declared cross-region journey and return retained");
            var reversed=definition.Regions.Reverse().Select(r=>engine.GenerateRegion(definition,r.Id)).ToArray();
            Check(Json(engine.Assemble(definition,reversed))==Json(world),"complete independently requested reverse order equals whole generation at scale");
            var reordered=definition with{Regions=definition.Regions.Reverse().ToArray(),Connections=definition.Connections.Reverse().ToArray()};
            Check(Json(engine.Generate(reordered))==Json(world),"source declaration order does not change canonical world geometry or work");
            string file=Path.Combine(output,"world-"+count+".json");await File.WriteAllTextAsync(file,Json(world));
            await engine.PublishAsync(world,Path.Combine(output,"published-"+count));
            Check(Json(await engine.ReadWorldAsync(file))==Json(world),"complete saved scaled world authenticates every region and connection");
            await File.WriteAllTextAsync(Path.Combine(output,"definition-"+count+".json"),Json(definition));
            observed.Add(new{Regions=count,Nodes=count*5,PhysicalSources=count*3,DirectedRegionalPaths=(count-1)*2,world.Work,
                StoredSamples=world.Regions.Sum(r=>r.World.Surface!.TerrainCapturePlan!.Window!.Heights.Count),SourceColumns=513,SourceRows=513,
                GenerationSeconds=watch.Elapsed.TotalSeconds,GenerationAllocatedBytes=bytes,CumulativeProcessPeakWorkingSet=System.Diagnostics.Process.GetCurrentProcess().PeakWorkingSet64});
        }
        await File.WriteAllTextAsync(Path.Combine(output,"measurements.json"),Json(observed));
        Console.WriteLine("PASS: persisted ownership tamper refusal; complete4/8/16-region programs preserve20/40/80 nodes,12/24/48 physical sources and every directed journey; whole/reverse-request/declaration-order/complete saved replay equality, unchanged source/count/work limits and observed generation resource measurements.");
    }
}
