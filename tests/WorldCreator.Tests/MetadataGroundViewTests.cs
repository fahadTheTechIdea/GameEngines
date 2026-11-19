using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Capture;
using WorldCreator.Core.Metadata;

internal static class MetadataGroundViewTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    static void Check(bool value,string message){if(!value)throw new Exception("Ground views: "+message);}
    static async Task Reject(Func<Task> action,string code)
    {try{await action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}

    public static async Task RunAsync(string sourceDirectory,string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        foreach(string name in new[]{"nested","registered"})
        {
            string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            var source=await StructureFirstPipeline.ReadJsonAsync<MetadataGroundCompilation>(Path.Combine(sourceDirectory,name,"world/capture-compilation.json"));
            var generator=name=="registered"?new WorldMetadataGenerator(TestCatalogs.Registry):new WorldMetadataGenerator();var pipeline=new StructureFirstPipeline();
            var world=source.Handoff.World;var rootChildren=world.Nodes.Where(n=>n.ParentId==world.Id).OrderBy(n=>n.Id,StringComparer.Ordinal).ToArray();
            Check(rootChildren.Length==2,"two declared detail scopes in source fixture");
            var spec=source.Handoff.Specification with{Scopes=[new("overview",null,"Full synthetic program",[]),
                new("west","overview","Complete west subtree",[rootChildren[0].Id]),new("east","overview","Complete east subtree",[rootChildren[1].Id])]};
            var handoff=pipeline.PlanMetadata(world,source.Handoff.Recipe,source.Handoff.Catalog,spec,generator);
            var whole=source.Request.Views.Single();
            async Task<MetadataGroundView> View(string id,string jobId,string png,double x,double extent)
            {
                var bytes=await File.ReadAllBytesAsync(png);await File.WriteAllBytesAsync(Path.Combine(folder,id+".png"),bytes);
                int width=(int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)),height=(int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4));
                double sx=extent/width,sy=100d/height;
                CapturePixel Pixel(CapturePixel p)=>new((p.X-x)/sx,p.Y/sy);
                var members=handoff.Jobs.Single(j=>j.Id==jobId).NodeIds.ToHashSet(StringComparer.Ordinal);
                var bindings=whole.Bindings.Where(b=>members.Contains(b.NodeId)).Select(b=>b with{OriginPixel=Pixel(b.OriginPixel)}).ToArray();
                var ids=bindings.Select(b=>b.FeatureId).ToHashSet(StringComparer.Ordinal);
                var image=whole.GroundCapture.Source with{MockupId=id,Revision="synthetic-"+id,File=id+".png",Sha256=Convert.ToHexStringLower(SHA256.HashData(bytes)),Width=width,Height=height};
                var capture=whole.GroundCapture with{Source=image,Features=whole.GroundCapture.Features.Where(f=>ids.Contains(f.Id)).Select(f=>f with
                    {Points=f.Points.Select(Pixel).ToArray(),ParentId=f.ParentId is {} parent&&ids.Contains(parent)?parent:null,
                     Notes="Synthetic geometry assertions against unrelated fixture pixels; no actual vision qualification."}).ToArray(),
                    Calibration=new(sx,0,x,0,sy,0,"Synthetic per-image world registration",.001,
                        [new(new(0,0),x,0),new(new(width,0),x+extent,0),new(new(0,height),x,100)])};
                var identity=capture with{Calibration=null,Features=capture.Features.Select(f=>f with{Semantic="building.roof"}).ToArray()};
                return new(id,identity,new(1,MockupCapturePipeline.MetadataHandoffDigest(handoff),MockupCapturePipeline.CaptureDigest(identity),jobId,
                    bindings.Select(b=>new MetadataCaptureBinding(b.NodeId,b.FeatureId)).ToArray()),capture,MockupCapturePipeline.CaptureDigest(capture),bindings);
            }
            var west=await View("west","scope:west","tests/fixtures/mockup-atlas/closed.png",0,50);
            var east=await View("east","scope:east","tests/fixtures/mockup-atlas/opening.png",50,50);
            var overview=await View("overview","scope:overview","tests/fixtures/mockup-capture-source.png",0,100);
            var owners=west.Bindings.Select(b=>new MetadataGroundOwner(b.NodeId,"west")).Concat(east.Bindings.Select(b=>new MetadataGroundOwner(b.NodeId,"east"))).ToArray();
            var input=new MetadataGroundRequest(2,[west,east,overview],owners,.02);
            Task<MetadataGroundCompilation> Compile(MetadataGroundRequest r)=>MockupCapturePipeline.CompileMetadataGroundAsync(handoff,r,folder,generator);
            var result=await pipeline.ExportMetadataGroundAsync(handoff,input,folder,Path.Combine(folder,"world"),generator);
            Check(result.Footprints.Count==source.Footprints.Count&&result.World.Nodes.Count==world.Nodes.Count,"one world and one physical instance per source");
            Check(result.Identities.Count==3&&result.Footprints.All(f=>f.Observations.Count==2),"detail and overview observations retained");
            Check(result.Request.Views.Select(v=>v.GroundCapture.Source.Sha256).Distinct().Count()==3,"three independently verified source images");
            void SameGeometry(MetadataGroundCompilation other)
            {
                foreach(var footprint in result.Footprints)
                {
                    var actual=other.Footprints.Single(f=>f.NodeId==footprint.NodeId);
                    Check(actual.Origin.DistanceTo(footprint.Origin)<1e-7&&actual.RotationDegrees==footprint.RotationDegrees,"selected view fixes pose");
                    Check(actual.Compiled.Vertices.Zip(footprint.Compiled.Vertices).All(p=>p.First.DistanceTo(p.Second)<1e-7),"source-sized footprint remains single");
                }
            }
            SameGeometry(source);
            var reordered=await Compile(input with{Views=input.Views.Reverse().Select(v=>v with{Bindings=v.Bindings.Reverse().ToArray(),IdentitySpecification=v.IdentitySpecification with
                {Bindings=v.IdentitySpecification.Bindings.Reverse().ToArray()}}).ToArray(),Owners=owners.Reverse().ToArray()});
            Check(Json(reordered)==Json(result),"declaration reordering preserves canonical receipt and generated world");
            var details=await Compile(input with{Views=[east,west]});SameGeometry(details);
            Check(details.Footprints.All(f=>f.Observations.Count==1),"complete detail sheets suffice for declared geometry");
            MetadataGroundView Shift(MetadataGroundView view,double dx,double rotation=0)
            {
                var cal=view.GroundCapture.Calibration!;
                var capture=view.GroundCapture with{Features=view.GroundCapture.Features.Select(f=>f with{Points=f.Points.Select(p=>new CapturePixel(p.X+dx/cal.A,p.Y)).ToArray()}).ToArray()};
                var identity=view.IdentityCapture with{Features=view.IdentityCapture.Features.Select(f=>f with{Points=f.Points.Select(p=>new CapturePixel(p.X+dx/cal.A,p.Y)).ToArray()}).ToArray()};
                return view with{GroundCapture=capture,GroundCaptureSha256=MockupCapturePipeline.CaptureDigest(capture),IdentityCapture=identity,
                    IdentitySpecification=view.IdentitySpecification with{CaptureSha256=MockupCapturePipeline.CaptureDigest(identity)},
                    Bindings=view.Bindings.Select(b=>b with{OriginPixel=new(b.OriginPixel.X+dx/cal.A,b.OriginPixel.Y),RotationDegrees=b.RotationDegrees+rotation}).ToArray()};
            }
            var close=input with{Views=[west,east,Shift(overview,.005)]};var closeResult=await Compile(close);SameGeometry(closeResult);
            Check(closeResult.Footprints.SelectMany(f=>f.Observations).Any(o=>o.ViewId=="overview"&&o.PairedBoundaryDeviationMeters>.004),"small reviewed discrepancy is measured without averaging owner pose");
            var conflict=input with{Views=[west,east,Shift(overview,.1)]};
            await Reject(()=>Compile(conflict),"METADATA_GROUND_VIEW_POSE_CONFLICT");
            var near=Shift(overview,.01);
            var inconsistent=near.GroundCapture with{Features=near.GroundCapture.Features.Select(f=>f with
                {Points=f.Points.Select(p=>new CapturePixel(p.X+.01,p.Y)).ToArray()}).ToArray()};
            var geometryConflict=input with{Views=[west,east,near with{GroundCapture=inconsistent,GroundCaptureSha256=MockupCapturePipeline.CaptureDigest(inconsistent)}]};
            await Reject(()=>Compile(geometryConflict),"METADATA_GROUND_VIEW_GEOMETRY_CONFLICT");
            if(name=="nested")
            {
                string square=world.Nodes.First(n=>n.ArchetypeId=="object").Id;
                await Reject(()=>Compile(input with{Views=[west,east,overview with{Bindings=overview.Bindings.Select(b=>b.NodeId==square?b with
                    {RotationDegrees=b.RotationDegrees+90}:b).ToArray()}]}),"METADATA_GROUND_VIEW_POSE_CONFLICT");
            }
            await Reject(()=>Compile(input with{Views=[west,overview],Owners=owners.Select(o=>o.ViewId=="east"?o with{ViewId="overview"}:o).ToArray()}),"METADATA_GROUND_DETAIL_SCOPE_REQUIRED");
            await Reject(()=>Compile(input with{Owners=owners.Skip(1).ToArray()}),"METADATA_GROUND_OWNER_COVERAGE_MISMATCH");
            await Reject(()=>Compile(input with{Owners=[..owners,owners[0]]}),"METADATA_GROUND_OWNER_INVALID");
            await Reject(()=>Compile(input with{Owners=owners.Select(o=>o.ViewId=="east"?o with{ViewId="west"}:o).ToArray()}),"METADATA_GROUND_OWNER_OBSERVATION_REQUIRED");
            await Reject(()=>Compile(input with{Views=[west,east,overview with{GroundCaptureSha256=new('0',64)}]}),"METADATA_GROUND_REQUEST_INVALID_OR_STALE");
            await Reject(()=>Compile(input with{Views=[west,east,east]}),"METADATA_GROUND_REQUEST_INVALID_OR_STALE");
            await Reject(()=>Compile(input with{Views=[west with{Id="bad:stream"},east,overview]}),"METADATA_GROUND_REQUEST_INVALID_OR_STALE");
            await Reject(()=>Compile(input with{Views=[west with{Id="WEST"},east,overview]}),"METADATA_GROUND_REQUEST_INVALID_OR_STALE");
            await Reject(()=>Compile(input with{Version=1}),"METADATA_GROUND_REQUEST_INVALID_OR_STALE");
            await Reject(()=>Compile(input with{MaximumBoundaryWork=1}),"METADATA_GROUND_BOUNDARY_WORK_BUDGET");
            var saved=await StructureFirstPipeline.ReadJsonAsync<MetadataGroundCompilation>(Path.Combine(folder,"world/capture-compilation.json"));
            await MockupCapturePipeline.ValidateMetadataGroundCompilationAsync(saved,folder,generator);
            await Reject(()=>MockupCapturePipeline.ValidateMetadataGroundCompilationAsync(saved with{Footprints=saved.Footprints.Select((f,i)=>i==0?f with{OwnerViewId="overview"}:f).ToArray()},folder,generator),"METADATA_GROUND_COMPILATION_REPLAY_MISMATCH");
            foreach(var view in result.Request.Views)
                Check(File.Exists(Path.Combine(folder,"world/capture-overlay-"+view.Id+".svg")),"per-image source overlay exported");
            foreach(var item in new[]{("handoff.json",Json(handoff)),("ground-request.json",Json(input)),("conflicting-request.json",Json(conflict)),
                ("geometry-conflict-request.json",Json(geometryConflict)),("close-compilation.json",Json(closeResult)),("detail-compilation.json",Json(details))})
                await File.WriteAllTextAsync(Path.Combine(folder,item.Item1),item.Item2);
            var imageBytes=await File.ReadAllBytesAsync(Path.Combine(folder,"east.png"));
            await File.AppendAllTextAsync(Path.Combine(folder,"east.png"),"tampered");
            await Reject(()=>Compile(input),"CAPTURE_SOURCE_HASH_MISMATCH");
            await File.WriteAllBytesAsync(Path.Combine(folder,"east.png"),imageBytes);
            Console.WriteLine($"PASS: {name}; three source images, two detail scopes, one overview, {result.Footprints.Count} unique physical instances; ownership, overlaps, calibrated registration, replay, order invariance and missing/conflicting/stale refusals.");
        }
        Console.WriteLine("Synthetic annotations only. Source pixels establish hash/format provenance, not actual structure recognition or art approval.");
    }
}
