using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Catalogs.Modern;
using WorldCreator.Core;
using WorldCreator.Core.Capture;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class MetadataGroundTests
{
    static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    static void Check(bool ok,string message){if(!ok)throw new Exception("Metadata ground: "+message);}
    static async Task Reject(Func<Task> action,string code)
    {try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}

    static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool registered)
    {
        var e=WorldHierarchyPlacementTests.Example();var boundary=Box(0,0,100,100);
        var surface=new WorldSurfaceDefinition(1,"capture-test-ground","meters",boundary,[],
            [new("ground","ground","Authored synthetic ground","capture-test",boundary,0,new(0,0),10,0,0)]);
        var request=e.Recipe.Placement!.Request with{Boundary=boundary,Anchors=[new("west",new(25,50)),new("east",new(75,50))],
            Relations=[new("west-preference","camp-a","west","Authored west activity center","capture-test",0,15,0,1),
                new("east-preference","camp-b","east","Authored east activity center","capture-test",0,15,0,1)]};
        var nested=e.Recipe.Placement.NestedStages!.Select(s=>s.Id.StartsWith("camp-",StringComparison.Ordinal)?s with{Request=s.Request with
        {Anchors=[new("left",new(-5,0)),new("right",new(5,0))],Relations=[new("left","station-0","left","Left station","capture-test",0,3,0,1),new("right","station-1","right","Right station","capture-test",0,3,0,1)]}}:s).ToArray();
        var catalog=registered?e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="camp"?a with
        {Parts=[],ChildRequirements=[],PhysicalFootprint=null,Structure=new(ModernCatalog.Selection,"warehouse","small")}:a).ToArray()}:e.Catalog;
        return(e.Recipe with{Surface=surface,Placement=e.Recipe.Placement with{Request=request,NestedStages=registered?null:nested}},catalog);
    }

    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var bytes=await File.ReadAllBytesAsync("tests/fixtures/mockup-capture-source.png");string hash=Convert.ToHexStringLower(SHA256.HashData(bytes));
        foreach(bool registered in new[]{false,true})
        {
            string folder=Path.Combine(output,registered?"registered":"nested");Directory.CreateDirectory(folder);await File.WriteAllBytesAsync(Path.Combine(folder,"source.png"),bytes);
            var e=Example(registered);var generator=registered?new WorldMetadataGenerator(TestCatalogs.Registry):new WorldMetadataGenerator();var owner=new StructureFirstPipeline();
            var world=generator.Generate(e.Recipe,e.Catalog);
            var specification=new MetadataArtSpecification(1,"ground-test",StructureFirstPipeline.WorldDigest(world),world.Nodes.DistinctBy(n=>n.ArchetypeId)
                .Select(n=>new MetadataArtBinding(n.ArchetypeId,n.ParentId is null?MetadataArtRole.Group:MetadataArtRole.Structure)).ToArray(),
                [new("complete",null,"Entire declared synthetic physical program",[world.Id])]);
            var handoff=owner.PlanMetadata(world,e.Recipe,e.Catalog,specification,generator);
            var sources=world.PlacementPlan!.Footprints.Concat(world.PlacementPlan.NestedStages.SelectMany(s=>s.Footprints)).OrderBy(f=>f.NodeId,StringComparer.Ordinal).ToArray();
            var nodes=world.Nodes.ToDictionary(n=>n.Id);var featureIds=sources.Select((f,i)=>(f.NodeId,Id:"physical-"+i)).ToDictionary(p=>p.NodeId,p=>p.Id);
            var bindings=new List<MetadataGroundBinding>();var features=new List<CaptureFeature>();
            foreach(var source in sources)
            {
                var node=nodes[source.NodeId];var p=node.Position!;var origin=new WorldPoint(p[0]+2,p[1]+1);
                double rotation=node.ArchetypeId=="station"?30:registered?15:0;double angle=rotation*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
                var polygon=source.Polygon.Vertices.Select(v=>new CapturePixel(v.X*c-v.Y*s+origin.X,v.X*s+v.Y*c+origin.Y)).ToArray();
                string id=featureIds[node.Id];string? parent=node.ParentId is {} parentId?featureIds.GetValueOrDefault(parentId):null;
                // Subdivision and opposite winding must not change a source-sized contour.
                if(features.Count==0)polygon=[polygon[2],polygon[1],new((polygon[0].X+polygon[1].X)/2,(polygon[0].Y+polygon[1].Y)/2),polygon[0],polygon[3]];
                features.Add(new(id,"structure.footprint",CaptureShape.Polygon,polygon,CaptureEvidence.Observed,CaptureReview.Accepted,.001,false,
                    "Synthetic ground annotation for contract qualification; not actual extracted image evidence.",parent));
                bindings.Add(new(node.Id,id,new(origin.X,origin.Y),rotation));
            }
            var sourceImage=new CaptureSource("synthetic","r1","source.png",hash,100,100,"test","orthographic","test-no-approval");
            var capture=new MockupCaptureDocument(1,sourceImage,CaptureCoverage.Complete,[],features,[],
                new(1,0,0,0,1,0,"Synthetic authored meter scale",.001,[new(new(0,0),0,0),new(new(100,0),100,0),new(new(0,100),0,100)]));
            var identity=capture with{Calibration=null,Features=features.Select(f=>f with{Semantic="building.roof"}).ToArray()};
            var ids=new MetadataCaptureSpecification(1,MockupCapturePipeline.MetadataHandoffDigest(handoff),MockupCapturePipeline.CaptureDigest(identity),"scope:complete",
                bindings.Select(b=>new MetadataCaptureBinding(b.NodeId,b.FeatureId)).ToArray());
            var view=new MetadataGroundView("whole",identity,ids,capture,MockupCapturePipeline.CaptureDigest(capture),bindings);
            var input=new MetadataGroundRequest(2,[view],bindings.Select(b=>new MetadataGroundOwner(b.NodeId,"whole")).ToArray(),.02);
            Task<MetadataGroundCompilation> Compile(MetadataGroundRequest r)=>MockupCapturePipeline.CompileMetadataGroundAsync(handoff,r,folder,generator);
            MetadataGroundRequest WithCapture(MockupCaptureDocument c)=>input with{Views=[view with{GroundCapture=c,GroundCaptureSha256=MockupCapturePipeline.CaptureDigest(c)}]};
            MetadataGroundRequest WithBindings(MetadataGroundRequest r,IReadOnlyList<MetadataGroundBinding> b)=>r with{Views=[r.Views[0] with{Bindings=b}]};
            var result=await owner.ExportMetadataGroundAsync(handoff,input,folder,Path.Combine(folder,"world"),generator);
            Check(result.World.Nodes.Count==world.Nodes.Count&&result.Footprints.Count==sources.Length,"complete physical hierarchy retained");
            foreach(var b in bindings)
            {
                var before=nodes[b.NodeId].Position!;var after=result.World.Nodes.Single(n=>n.Id==b.NodeId).Position!;
                Check(Math.Abs(after[0]-before[0]-2)<1e-7&&Math.Abs(after[1]-before[1]-1)<1e-7,"captured origins change actual generated positions without relocation");
            }
            Check(result.Footprints.SelectMany(f=>f.Observations).All(f=>f.PairedBoundaryDeviationMeters+f.UncertaintyMeters<=input.ToleranceMeters),"continuous boundary certificate within tolerance");
            Check(Json(result.World.Surface)==Json(world.Surface)&&Json(result.World.StructureDefinitions)==Json(world.StructureDefinitions),"source terrain and registered dimensions preserved");
            Check(result.World.NetworkPlan!.Networks.Single().Plan.Edges.Count==world.NetworkPlan!.Networks.Single().Plan.Edges.Count,"shared connection generation retained at captured origins");
            Check(Json(generator.Generate(result.Recipe,e.Catalog))==Json(result.World),"complete generated recipe replay");
            var loaded=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"world/world-metadata.json"),catalogs:registered?TestCatalogs.Registry:null);
            Check(Json(loaded)==Json(result.World),"normal engine validator and persisted world agree");
            var saved=await StructureFirstPipeline.ReadJsonAsync<MetadataGroundCompilation>(Path.Combine(folder,"world/capture-compilation.json"));
            await MockupCapturePipeline.ValidateMetadataGroundCompilationAsync(saved,folder,generator);
            await Reject(()=>MockupCapturePipeline.ValidateMetadataGroundCompilationAsync(saved with{Status="ApprovedMap"},folder,generator),"METADATA_GROUND_COMPILATION_REPLAY_MISMATCH");
            var overlay=await File.ReadAllTextAsync(Path.Combine(folder,"world/capture-overlay-whole.svg"));
            Check(overlay.Contains("compiled-footprint")&&overlay.Contains("data:image/png;base64,"),"overlay includes source pixels and compiled footprints");
            await File.WriteAllTextAsync(Path.Combine(folder,"handoff.json"),Json(handoff));await File.WriteAllTextAsync(Path.Combine(folder,"ground-request.json"),Json(input));
            var roof=WithCapture(capture with{Features=features.Select((f,i)=>i==0?f with{Semantic="building.roof"}:f).ToArray()});
            await Reject(()=>Compile(roof),"METADATA_GROUND_REVIEWED_FOOTPRINT_REQUIRED");
            var changed=features[0] with{Points=features[0].Points.Select(p=>new CapturePixel(p.X+.5,p.Y)).ToArray()};
            var mismatch=WithCapture(capture with{Features=[changed,..features.Skip(1)]});
            await Reject(()=>Compile(mismatch),"METADATA_GROUND_SOURCE_DIMENSIONS_MISMATCH");
            await File.WriteAllTextAsync(Path.Combine(folder,"mismatched-request.json"),Json(mismatch));
            await Reject(()=>Compile(WithBindings(input,bindings.Skip(1).ToArray())),"METADATA_GROUND_PHYSICAL_COVERAGE_MISMATCH");
            await Reject(()=>Compile(WithBindings(input,[..bindings,bindings[0]])),"METADATA_GROUND_BINDING_INVALID");
            await Reject(()=>Compile(input with{Views=[view with{IdentitySpecification=ids with{Bindings=ids.Bindings.Skip(1).ToArray()}}]}),"METADATA_GROUND_IDENTITY_UNRESOLVED");
            await Reject(()=>Compile(WithCapture(capture with{Calibration=null})),"CAPTURE_CALIBRATION_REQUIRED");
            await Reject(()=>Compile(WithCapture(capture with{Source=sourceImage with{Revision="other"}})),"METADATA_GROUND_SOURCE_REVISION_MISMATCH");
            await Reject(()=>Compile(input with{Views=[view with{GroundCaptureSha256=new('0',64)}]}),"METADATA_GROUND_REQUEST_INVALID_OR_STALE");
            await Reject(()=>Compile(WithCapture(capture with{Features=[features[0] with{UncertaintyPixels=1},..features.Skip(1)]})),"METADATA_GROUND_UNCERTAINTY_EXCEEDS_TOLERANCE");
            await Reject(()=>Compile(input with{MaximumBoundaryWork=1}),"METADATA_GROUND_BOUNDARY_WORK_BUDGET");
            bool Second(string id)=>id=="terrain-placement/camps-1"||id.StartsWith("terrain-placement/camps-1/",StringComparison.Ordinal);
            var collisionFeatures=features.Select(f=>Second(bindings.Single(b=>b.FeatureId==f.Id).NodeId)?f with
                {Points=f.Points.Select(p=>new CapturePixel(p.X-50,p.Y)).ToArray()}:f).ToArray();
            var collision=WithBindings(WithCapture(capture with{Features=collisionFeatures}),bindings.Select(b=>Second(b.NodeId)?b with
                {OriginPixel=new(b.OriginPixel.X-50,b.OriginPixel.Y)}:b).ToArray());
            await Reject(()=>Compile(collision),"METADATA_PLACEMENT_UNRESOLVED");
            await File.WriteAllTextAsync(Path.Combine(folder,"conflicting-request.json"),Json(collision));
            if(!registered)
            {
                var child=features.First(f=>f.ParentId is not null);
                await Reject(()=>Compile(WithCapture(capture with{Features=features.Select(f=>f.Id==child.Id?f with{ParentId=null}:f).ToArray()})),"METADATA_GROUND_PARENT_MISMATCH");
                CapturePixel Inverse(CapturePixel p)=>new((.9*(p.X+4)-.1*(p.Y+2))/.985,(-.05*(p.X+4)+1.1*(p.Y+2))/.985);
                var affine=capture with{Features=features.Select(f=>f with{Points=f.Points.Select(Inverse).ToArray()}).ToArray(),
                    Calibration=new(1.1,.1,-4,.05,.9,-2,"Synthetic affine calibration",.001,
                        [new(new(0,0),-4,-2),new(new(100,0),106,3),new(new(0,100),6,88)])};
                var affineResult=await Compile(WithBindings(WithCapture(affine),bindings.Select(b=>b with{OriginPixel=Inverse(b.OriginPixel)}).ToArray()));
                Check(affineResult.Footprints.All(f=>f.Origin.DistanceTo(result.Footprints.Single(o=>o.NodeId==f.NodeId).Origin)<1e-7),"sheared anisotropic calibration preserves world origins");
                Check(affineResult.Footprints.SelectMany(f=>f.Observations).All(f=>f.UncertaintyMeters>.002&&f.UncertaintyMeters<.003),"affine singular scale bounds pixel uncertainty");
                await File.WriteAllTextAsync(Path.Combine(folder,"affine-compilation.json"),Json(affineResult));
            }
            using var cancel=new CancellationTokenSource();cancel.Cancel();bool cancelled=false;
            try{await MockupCapturePipeline.CompileMetadataGroundAsync(handoff,input,folder,generator,cancel.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"cancellation");
            var existing=await File.ReadAllBytesAsync(Path.Combine(folder,"world/world-metadata.json"));bool preserved=false;
            try{await owner.ExportMetadataGroundAsync(handoff,input,folder,Path.Combine(folder,"world"),generator);}catch(IOException){preserved=true;}
            var unchanged=await File.ReadAllBytesAsync(Path.Combine(folder,"world/world-metadata.json"));
            Check(preserved&&existing.SequenceEqual(unchanged),"existing export preserved");
            Console.WriteLine($"PASS: {(registered?"registered":"nested")}: {world.Nodes.Count} members, {sources.Length} rotated/source-sized ground footprints, exact translated origins, full replay/export and negative gates.");
        }
        Console.WriteLine("Synthetic capture compilation only; real image interpretation, lifecycle approvals and complete map/Godot acceptance NOT RUN.");
    }
}
