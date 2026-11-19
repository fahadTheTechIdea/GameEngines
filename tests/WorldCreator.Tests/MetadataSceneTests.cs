using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Capture;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class MetadataSceneTests
{
    static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    static void Check(bool value,string message){if(!value)throw new Exception("Scene capture: "+message);}
    static async Task Reject(Func<Task> action,string code)
    {try{await action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}

    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var properties=new Dictionary<string,JsonElement>();var boundary=Box(0,0,100,100);
        var catalog=new WorldMetadataCatalog(1,"scene-content",
            [new("root","region","Declared scene program",properties,[new("sites","site",2,"Required structures"),
                new("junctions","junction",3,"Logical street nodes",Layout:MetadataLineLayout.Parameters([25,30],[25,0]))],
                [new("streets","street",MetadataConnectionPattern.Chain,"junctions")],[],LayerId:"intent"),
             new("site","structure","Source-sized structure",properties,[],[],[],LayerId:"intent",PhysicalFootprint:new("meters","Physical source","scene:site",Box(-3,-2,6,4)),
                Access:new(MetadataOccupancyKind.Solid,[new("front","Front access","scene:front",new(0,-2),new(0,-4),2)])),
             new("junction","junction","Logical graph junction, no artwork",properties,[],[],[],LayerId:"intent")]);
        var environment=new WorldEnvironmentDefinition(1,"scene-fields",[new("height","Authored ground height","scene:height","meters",0,[new(WorldFieldInput.Elevation,1)])]);
        var suitability=new WorldSuitabilityProfile("site-ground","Dry source placement","scene:ground",["ground"],[new("height","height","Source height range","scene:height",5,15)],[]);
        var profile=new WorldTraversalProfile("passage","Declared one-meter passage","scene:passage",.5,.1,new Dictionary<string,double>{{"ground",1}});
        var placement=new MetadataPlacementDefinition(new("sites","Exact authored source sites","scene:sites",boundary,
            [new("a","First structure","scene:a",null,suitability),new("b","Second structure","scene:b",null,suitability)],
            [new("west",new(25,50)),new("east",new(75,50))],
            [new("west","a","west","Preserve source position","scene:west",0,0,0,1),new("east","b","east","Preserve source position","scene:east",0,0,0,1)]),
            [new("a","scene-world/sites-0",true),new("b","scene-world/sites-1",true)]);
        var surface=new WorldSurfaceDefinition(1,"scene-surface","meters",boundary,[new("protected","Declared protected ground","scene:protected",Box(80,80,8,8))],
            [new("ground","ground","Authored ground","scene:ground",boundary,0,new(0,0),10,0,0),
             new("water","water","Water region","scene:water",Box(40,70,6,6),1,new(0,0),10,0,0)]);
        var recipe=new WorldMetadataRecipe(1,"scene-world","game-framework-test",catalog.Id,"root","region",MetadataCoverage.PartialScope,
            ["Synthetic geometry qualification, artwork and native runtime are unverified"],new Dictionary<string,int>(),new(["x","y"],"meters"),[0,0],true,
            AdditionalConnections:[new("west-drive","drive","scene-world/sites-0","scene-world/junctions-0"),new("east-drive","drive","scene-world/sites-1","scene-world/junctions-2")],
            Layers:[new("intent","game-intent","Declared scene","scene:intent",[],MetadataCoverage.CompleteDeclaredScope,[])],
            Surface:surface,Environment:environment,Placement:placement,
            RouteRules:[new("streets","street",profile,new(.2,[],MaximumWork:1000000)),new("drives","drive",profile,new(.2,[],MaximumWork:1000000),"front")],
            Corridors:new([new("streets","street","scene:street",0,"connect-at-grade"),new("drives","drive","scene:drive",0,"connect-at-grade")]));
        return(recipe,catalog);
    }

    public static async Task RunAsync(string folder)
    {
        if(Directory.Exists(folder)||File.Exists(folder))throw new IOException("Output exists");Directory.CreateDirectory(folder);
        var e=Example();var generator=new WorldMetadataGenerator();var owner=new StructureFirstPipeline();var world=generator.Generate(e.Recipe,e.Catalog);
        var reached=new HashSet<string>();var queue=new Queue<string>();queue.Enqueue("scene-world/sites-0");
        while(queue.TryDequeue(out var id))if(reached.Add(id))foreach(var connection in world.Connections)
        {if(connection.FromId==id)queue.Enqueue(connection.ToId);if(connection.Bidirectional&&connection.ToId==id)queue.Enqueue(connection.FromId);}
        Check(reached.Count==5,"global explicit drives connect every site and junction");
        var art=new MetadataArtSpecification(1,"scene-art",StructureFirstPipeline.WorldDigest(world),
            [new("root",MetadataArtRole.Group),new("site",MetadataArtRole.Structure),new("junction",MetadataArtRole.Logical)],
            [new("complete",null,"Complete declared scene",[world.Id])]);
        var handoff=owner.PlanMetadata(world,e.Recipe,e.Catalog,art,generator);
        Check(handoff.Members.Count==6&&handoff.Members.Count(m=>m.Role==MetadataArtRole.Logical)==3&&handoff.Jobs.Count(j=>j.Kind=="ReferenceConcept")==1,"logical nodes retained without artwork jobs");
        await Reject(()=>Task.FromResult(owner.PlanMetadata(world,e.Recipe,e.Catalog,art with{Bindings=art.Bindings.Select(b=>b.ArchetypeId=="site"?b with{Role=MetadataArtRole.Logical}:b).ToArray()},generator)),"METADATA_ART_LOGICAL_PHYSICAL_CONFLICT");
        var png=await File.ReadAllBytesAsync("tests/fixtures/mockup-capture-source.png");await File.WriteAllBytesAsync(Path.Combine(folder,"source.png"),png);
        string hash=Convert.ToHexStringLower(SHA256.HashData(png));var image=new CaptureSource("synthetic-scene","r1","source.png",hash,100,100,"test","orthographic","test-no-approval");
        var calibration=new CaptureCalibration(1,0,0,0,1,0,"Synthetic authored meter frame",.001,[new(new(0,0),0,0),new(new(100,0),100,0),new(new(0,100),0,100)]);
        var physical=world.PlacementPlan!.Footprints;var bindings=new List<MetadataGroundBinding>();var groundFeatures=new List<CaptureFeature>();
        foreach(var source in physical)
        {
            var n=world.Nodes.Single(n=>n.Id==source.NodeId);var p=n.Position!;string id="body-"+bindings.Count;
            bindings.Add(new(n.Id,id,new(p[0],p[1]),0));groundFeatures.Add(new(id,"structure.footprint",CaptureShape.Polygon,
                source.Polygon.Vertices.Select(v=>new CapturePixel(v.X+p[0],v.Y+p[1])).ToArray(),CaptureEvidence.Observed,CaptureReview.Accepted,.001,false,"Synthetic source footprint; no actual vision."));
        }
        var groundCapture=new MockupCaptureDocument(1,image,CaptureCoverage.Complete,[],groundFeatures,[],calibration);
        var identity=groundCapture with{Calibration=null,Features=groundFeatures.Select(f=>f with{Semantic="building.roof"}).ToArray()};
        var identitySpec=new MetadataCaptureSpecification(1,MockupCapturePipeline.MetadataHandoffDigest(handoff),MockupCapturePipeline.CaptureDigest(identity),"scope:complete",
            bindings.Select(b=>new MetadataCaptureBinding(b.NodeId,b.FeatureId)).ToArray());
        var groundRequest=new MetadataGroundRequest(2,[new("overview",identity,identitySpec,groundCapture,MockupCapturePipeline.CaptureDigest(groundCapture),bindings)],
            bindings.Select(b=>new MetadataGroundOwner(b.NodeId,"overview")).ToArray(),.02);
        var ground=await MockupCapturePipeline.CompileMetadataGroundAsync(handoff,groundRequest,folder,generator);
        var contour=new WorldPolygon([new(0,0),new(100,0),new(100,100),new(10,100),new(0,80)]);
        CaptureFeature Polygon(string id,string semantic,WorldPolygon p)=>new(id,semantic,CaptureShape.Polygon,p.Vertices.Select(v=>new CapturePixel(v.X,v.Y)).ToArray(),
            CaptureEvidence.Observed,CaptureReview.Accepted,.001,false,"Synthetic scene trace; this is not actual image interpretation.");
        var features=new List<CaptureFeature>{Polygon("domain","terrain.domain",contour),Polygon("ground","terrain.ground",contour),Polygon("pond","water.region",Box(38,27,8,6)),
            Polygon("protected","terrain.exclusion",Box(82,78,8,8))};
        var pathBindings=new List<MetadataScenePathBinding>();var pathByConnection=new Dictionary<string,string>();
        foreach(var route in ground.World.RoutePlan!.Routes)
        {
            string id="path-"+pathBindings.Count;var connection=world.Connections.Single(c=>c.Id==route.ConnectionId);
            var points=connection.Kind=="street"&&connection.FromId.EndsWith("junctions-0",StringComparison.Ordinal)?
                new WorldPoint[]{new(25,30),new(35,22),new(45,24),new(50,30)}:route.Forward.Path!.Points.ToArray();
            features.Add(new(id,"road.centerline",CaptureShape.Polyline,points.Select(p=>new CapturePixel(p.X,p.Y)).ToArray(),CaptureEvidence.Observed,CaptureReview.Accepted,.001,false,
                "Synthetic prescribed street/drive trace",Road:new(1,CaptureWidthKind.FullCorridor,0,["walk"])));
            pathBindings.Add(new(route.ConnectionId,id));pathByConnection.Add(route.ConnectionId,id);
        }
        var junctions=world.Nodes.Where(n=>n.ArchetypeId=="junction").Select((n,i)=>new CaptureJunction("junction-"+i,new(n.Position![0],n.Position[1]),
            world.Connections.Where(c=>c.FromId==n.Id||c.ToId==n.Id).Select(c=>pathByConnection[c.Id]).ToArray(),CaptureJunctionKind.AtGrade,CaptureReview.Accepted,"Synthetic reviewed graph junction")).ToArray();
        var capture=new MockupCaptureDocument(1,image,CaptureCoverage.Complete,[],features,junctions,calibration);
        var view=new MetadataSceneView("overview",capture,MockupCapturePipeline.CaptureDigest(capture),
            [new(MetadataSceneSurfaceKind.Domain,e.Recipe.Surface!.Id,"domain"),new(MetadataSceneSurfaceKind.Patch,"ground","ground"),new(MetadataSceneSurfaceKind.Patch,"water","pond"),
                new(MetadataSceneSurfaceKind.Exclusion,"protected","protected")],pathBindings);
        var input=new MetadataSceneRequest(1,MockupCapturePipeline.GroundCompilationDigest(ground),[view],.02);
        MetadataSceneRequest WithCapture(MockupCaptureDocument c)=>input with{Views=[view with{Capture=c,CaptureSha256=MockupCapturePipeline.CaptureDigest(c)}]};
        Task<MetadataSceneCompilation> Compile(MetadataSceneRequest r)=>MockupCapturePipeline.CompileMetadataSceneAsync(ground,r,folder,generator);
        var result=await owner.ExportMetadataSceneAsync(ground,input,folder,Path.Combine(folder,"world"),generator);
        Check(Json(result)==Json(await Compile(input with{Views=[view with{Surfaces=view.Surfaces.Reverse().ToArray(),Paths=view.Paths.Reverse().ToArray()}]})),"binding order invariant canonical replay");
        Check(result.World.Nodes.Count==6&&result.Surfaces.Count==4&&result.Paths.Count==4,"complete scene and logical topology retained");
        Check(result.World.Surface!.Boundary!.Vertices.SequenceEqual(contour.Vertices),"irregular source domain compiled exactly");
        Check(result.World.Surface.Patches.Single(p=>p.Id=="water").Boundary.Vertices.SequenceEqual(Box(38,27,8,6).Vertices),"water moves to captured region");
        Check(result.World.Surface.Exclusions.Single().Boundary.Vertices.SequenceEqual(Box(82,78,8,8).Vertices),"exclusion follows its captured region");
        foreach(var path in result.Paths)
        {
            var route=result.World.RoutePlan!.Routes.Single(r=>r.ConnectionId==path.Binding.ConnectionId);
            Check(route.Forward.Path!.Points.SequenceEqual(path.Path.Points)&&route.Reverse!.Path!.Points.SequenceEqual(path.Path.Points.Reverse()),"all forward/reverse trace points preserved");
            Check(route.Forward.SearchModel=="prescribed-polyline-v1"&&route.Forward.CandidateCount==path.Path.Points.Count,"no candidate search substitutes the observed trace");
        }
        Check(Json(result.World)==Json(generator.Generate(result.Recipe,e.Catalog)),"ordinary generator replay");
        Check(Json(result.World)==Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"world/world-metadata.json"))),"ordinary saved-world validation");
        var saved=await StructureFirstPipeline.ReadJsonAsync<MetadataSceneCompilation>(Path.Combine(folder,"world/scene-compilation.json"));
        await MockupCapturePipeline.ValidateMetadataSceneCompilationAsync(saved,folder,generator);
        await Reject(()=>MockupCapturePipeline.ValidateMetadataSceneCompilationAsync(saved with{Status="Approved"},folder,generator),"METADATA_SCENE_COMPILATION_REPLAY_MISMATCH");
        var curved=result.Paths.Single(p=>p.Path.Points.Count==4);var badFeatures=features.Select(f=>f.Id==curved.Binding.FeatureId?f with{Points=[new(25,30),new(50,30)]}:f).ToArray();
        var blocked=WithCapture(capture with{Features=badFeatures});await Reject(()=>Compile(blocked),"METADATA_ROUTE_ALIGNMENT_REJECTED");
        await Reject(()=>Compile(input with{Views=[view with{Surfaces=view.Surfaces.Skip(1).ToArray()}]}),"METADATA_SCENE_UNASSIGNED_FEATURES");
        await Reject(()=>Compile(input with{GroundCompilationSha256=new('0',64)}),"METADATA_SCENE_REQUEST_INVALID_OR_STALE");
        await Reject(()=>Compile(WithCapture(capture with{Features=features.Select(f=>f.Road is {} road?f with{Road=road with{WidthPixels=2}}:f).ToArray()})),"METADATA_SCENE_PATH_WIDTH_MISMATCH");
        await Reject(()=>Compile(WithCapture(capture with{Features=features.Select(f=>f.Road is {} road?f with{Road=road with{Level=1}}:f).ToArray()})),"METADATA_SCENE_PATH_WIDTH_OR_LEVEL_INVALID");
        await Reject(()=>Compile(WithCapture(capture with{Features=features.Select(f=>f.Id=="domain"?f with{Semantic="building.roof"}:f).ToArray()})),"METADATA_SCENE_FEATURE_UNRESOLVED");
        await Reject(()=>Compile(input with{MaximumWork=1}),"METADATA_SCENE_WORK_BUDGET");
        await Reject(()=>Compile(WithCapture(capture with{Junctions=[]})),"METADATA_SCENE_JUNCTION_REVIEW_REQUIRED");
        var shiftedEndpoint=features.Select(f=>f.Id==curved.Binding.FeatureId?f with{Points=[..f.Points.Take(f.Points.Count-1),new(50.1,30)]}:f).ToArray();
        await Reject(()=>Compile(WithCapture(capture with{Features=shiftedEndpoint})),"METADATA_SCENE_JUNCTION_GRAPH_MISMATCH");
        using(var cancel=new CancellationTokenSource())
        {
            cancel.Cancel();bool observed=false;try{await MockupCapturePipeline.CompileMetadataSceneAsync(ground,input,folder,generator,cancel.Token);}catch(OperationCanceledException){observed=true;}
            Check(observed,"cancellation propagates");
        }
        var original=await File.ReadAllTextAsync(Path.Combine(folder,"world/world-metadata.json"));bool exists=false;
        try{await owner.ExportMetadataSceneAsync(ground,input,folder,Path.Combine(folder,"world"),generator);}catch(IOException){exists=true;}
        var unchanged=await File.ReadAllTextAsync(Path.Combine(folder,"world/world-metadata.json"));Check(exists&&original==unchanged,"existing publication preserved");
        var standalone=new WorldSurfaceRoutePlanner();var rule=result.Recipe.RouteRules!.Single(r=>r.Id=="streets");
        var traceRequest=new WorldRouteRequest("blocked-trace","Preserve trace","test",new(25,30),new(50,30),rule.Profile,rule.Search);
        await Reject(()=>Task.FromResult(standalone.Compile(result.World.Surface,traceRequest,new([new(25.01,30),new(50,30)]))),"WORLD_ROUTE_TRACE_ENDPOINT_MISMATCH");
        Check(standalone.Compile(result.World.Surface,traceRequest,new([new(25,30),new(50,30)])).Status==WorldRouteStatus.TraceRejected,"blocked trace never uses a feasible detour fallback");
        Check(standalone.Plan(result.World.Surface,traceRequest).Status==WorldRouteStatus.Found,"ordinary search can detour, distinguishing trace rejection from global infeasibility");
        CapturePixel Inverse(CapturePixel p)=>new((p.X+5)/1.2,(p.Y+4)/1.1);
        var affineCal=new CaptureCalibration(1.2,0,-5,0,1.1,-4,"Synthetic unequal image scales",.001,
            [new(new(0,0),-5,-4),new(new(100,0),115,-4),new(new(0,100),-5,106)]);
        var affineGroundCapture=groundCapture with{Calibration=affineCal,Features=groundCapture.Features.Select(f=>f with{Points=f.Points.Select(Inverse).ToArray()}).ToArray()};
        var affineIdentity=affineGroundCapture with{Calibration=null,Features=affineGroundCapture.Features.Select(f=>f with{Semantic="building.roof"}).ToArray()};
        var affineView=groundRequest.Views.Single() with{GroundCapture=affineGroundCapture,GroundCaptureSha256=MockupCapturePipeline.CaptureDigest(affineGroundCapture),
            IdentityCapture=affineIdentity,IdentitySpecification=identitySpec with{CaptureSha256=MockupCapturePipeline.CaptureDigest(affineIdentity)},
            Bindings=bindings.Select(b=>b with{OriginPixel=Inverse(b.OriginPixel)}).ToArray()};
        var affineGround=await MockupCapturePipeline.CompileMetadataGroundAsync(handoff,groundRequest with{Views=[affineView]},folder,generator);
        var affineCapture=capture with{Calibration=affineCal,Features=features.Select(f=>
        {
            var p=f.Points.Select(Inverse).ToArray();if(f.Road is not {} road)return f with{Points=p};
            var widths=Enumerable.Range(1,p.Length-1).Select(i=>
            {
                double pixels=Math.Sqrt((p[i].X-p[i-1].X)*(p[i].X-p[i-1].X)+(p[i].Y-p[i-1].Y)*(p[i].Y-p[i-1].Y));
                var a=f.Points[i-1];var b=f.Points[i];return Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y))/(1.32*pixels);
            }).ToArray();
            return f with{Points=p,Road=road with{WidthPixels=widths[0],SegmentWidthsPixels=widths}};
        }).ToArray(),Junctions=junctions.Select(j=>j with{Position=Inverse(j.Position)}).ToArray()};
        var affineInput=input with{GroundCompilationSha256=MockupCapturePipeline.GroundCompilationDigest(affineGround),
            Views=[view with{Capture=affineCapture,CaptureSha256=MockupCapturePipeline.CaptureDigest(affineCapture)}]};
        var affineResult=await MockupCapturePipeline.CompileMetadataSceneAsync(affineGround,affineInput,folder,generator);
        Check(affineResult.Paths.SelectMany(p=>p.WidthsMeters).All(w=>Math.Abs(w-1)<1e-8),"direction-specific pixel widths recover the same full meter width");
        Check(affineCapture.Features.Where(f=>f.Points.Count==4&&f.Road is not null).Single().Road!.SegmentWidthsPixels!.Distinct().Count()>1,"curved road has multiple measured image widths");
        var missingWidth=affineCapture with{Features=affineCapture.Features.Select(f=>f.Road is {} road?f with{Road=road with{SegmentWidthsPixels=[]}}:f).ToArray()};
        await Reject(()=>MockupCapturePipeline.CompileMetadataSceneAsync(affineGround,affineInput with{Views=[view with{Capture=missingWidth,CaptureSha256=MockupCapturePipeline.CaptureDigest(missingWidth)}]},folder,generator),"CAPTURE_ROAD_INVALID");
        await File.WriteAllTextAsync(Path.Combine(folder,"affine-scene.json"),Json(affineResult));
        var detailGroundView=groundRequest.Views.Single() with{Id="detail"};
        var splitGround=await MockupCapturePipeline.CompileMetadataGroundAsync(handoff,groundRequest with{Views=[groundRequest.Views.Single(),detailGroundView]},folder,generator);
        MetadataSceneView ScenePart(string id,bool drives)
        {
            var selected=view.Paths.Where(p=>world.Connections.Single(c=>c.Id==p.ConnectionId).Kind=="drive"==drives).ToArray();
            var allowed=selected.Select(p=>p.FeatureId).Concat(drives?[]:view.Surfaces.Select(s=>s.FeatureId)).ToHashSet(StringComparer.Ordinal);
            var part=capture with{Features=capture.Features.Where(f=>allowed.Contains(f.Id)).ToArray(),Junctions=capture.Junctions.Where(j=>j.RoadIds.All(allowed.Contains)).ToArray()};
            return new(id,part,MockupCapturePipeline.CaptureDigest(part),drives?[]:view.Surfaces,selected);
        }
        var splitInput=input with{GroundCompilationSha256=MockupCapturePipeline.GroundCompilationDigest(splitGround),Views=[ScenePart("overview",false),ScenePart("detail",true)]};
        var splitResult=await MockupCapturePipeline.CompileMetadataSceneAsync(splitGround,splitInput,folder,generator);
        Check(splitResult.Paths.All(p=>p.Path.Points.SequenceEqual(result.Paths.Single(o=>o.Binding.ConnectionId==p.Binding.ConnectionId).Path.Points)),"separate scene views preserve the complete traced network");
        var driveView=splitInput.Views.Single(v=>v.ViewId=="detail");var driveLinks=driveView.Paths.Select(p=>p.ConnectionId).Reverse().ToArray();
        var wrongGraph=splitInput with{Views=[splitInput.Views[0],driveView with{Paths=driveView.Paths.Select((p,i)=>p with{ConnectionId=driveLinks[i]}).ToArray()}]};
        await Reject(()=>MockupCapturePipeline.CompileMetadataSceneAsync(splitGround,wrongGraph,folder,generator),"METADATA_SCENE_JUNCTION_GRAPH_MISMATCH");
        await File.WriteAllTextAsync(Path.Combine(folder,"split-scene.json"),Json(splitResult));
        foreach(var pair in new[]{("ground.json",Json(ground)),("scene-request.json",Json(input)),("blocked-request.json",Json(blocked))})await File.WriteAllTextAsync(Path.Combine(folder,pair.Item1),pair.Item2);
        Console.WriteLine("PASS: six-member scene with three logical junctions, two exact physical structures, irregular domain, shifted pond, four exact street/drive paths and reverse traces, complete capture/topology, shared terrain/access/corridor validation, replay/export and negative gates. Actual vision/art/native runtime NOT RUN.");
    }
}
