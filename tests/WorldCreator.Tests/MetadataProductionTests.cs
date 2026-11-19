using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldCreator.Core;
using WorldCreator.Core.Capture;

internal static class MetadataProductionTests
{
    static void Check(bool value,string message){if(!value)throw new Exception("Metadata production: "+message);}
    static string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    static async Task Reject(Func<Task> action,string code)
    {try{await action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected refusal "+code);}

    public static async Task RunAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Test output exists");
        string root=Path.GetFullPath(directory),images=Path.Combine(root,"scene");Directory.CreateDirectory(root);
        await MetadataSceneTests.RunAsync(images);
        var scene=await StructureFirstPipeline.ReadJsonAsync<MetadataSceneCompilation>(Path.Combine(images,"world","scene-compilation.json"));
        var pipeline=new StructureFirstPipeline();var inventory=await pipeline.ExportMetadataProductionInventoryAsync(scene,images,Path.Combine(root,"inventory.json"));
        Check(inventory.Targets.Count==10&&inventory.Members.Count==6,"all two structures, two surface patches, two entrance approaches and four unduplicated passages, including all six metadata identities");
        Check(inventory.Targets.Count(t=>t.Kind==MetadataProductionTargetKind.Access)==2&&inventory.Targets.Where(t=>t.Kind==MetadataProductionTargetKind.Access).All(t=>t.WidthMeters==2&&t.Boundary?.Vertices.Count==4),"source-width entrance strips are explicit production targets");
        Check(inventory.Targets.Count(t=>t.Kind==MetadataProductionTargetKind.Member)==2&&inventory.Members.Count(m=>m.Role==MetadataArtRole.Logical)==3,"logical junctions never require an image");
        Check(inventory.Domain.Vertices.SequenceEqual(scene.World.Surface!.Boundary!.Vertices)&&inventory.Exclusions.Single().Boundary.Vertices.SequenceEqual(scene.World.Surface.Exclusions.Single().Boundary.Vertices),"exact irregular domain and protected contour");
        foreach(var target in inventory.Targets.Where(t=>t.Kind==MetadataProductionTargetKind.Passage))
            Check(target.Path!.Points.SequenceEqual(scene.Paths.Single(p=>p.Binding.ConnectionId==target.SourceId).Path.Points)&&target.WidthMeters==1,"actual final source curves and full widths, one target per bidirectional connection");
        foreach(var target in inventory.Targets.Where(t=>t.Kind==MetadataProductionTargetKind.Member))
            Check(target.Boundary!.Vertices.SequenceEqual(scene.Ground.Footprints.Single(f=>f.NodeId==target.SourceId).Compiled.Vertices),"exact ground footprint");

        string assets=Path.Combine(root,"test-assets");Directory.CreateDirectory(assets);
        var png=await File.ReadAllBytesAsync("tests/fixtures/mockup-atlas/closed.png");
        var manifests=new Dictionary<string,ApprovedArtManifest>(StringComparer.Ordinal);
        foreach(string id in new[]{"test-body","test-tiles"})
        {
            string image=id+".png",atlas=id+".atlas.json",bundle=id+".bundle.json";
            await File.WriteAllBytesAsync(Path.Combine(assets,image),png);string sha=Hash(Path.Combine(assets,image));
            JsonObject Frame(string name,int y,int h)=>new(){["name"]=name,["rect"]=new JsonArray(0,y,16,h),["pivot"]=new JsonArray(8,h/2),["source_sha256"]=sha};
            JsonArray frames;
            if(id=="test-body")
            {
                var frame=Frame("body",0,16);frame["anchors"]=new JsonObject{
                    ["g0"]=new JsonArray(5,6),["g1"]=new JsonArray(11,6),["g2"]=new JsonArray(11,10),["g3"]=new JsonArray(5,10)};
                frames=new(frame);
            }
            else frames=new(Frame("tile-a",0,8),Frame("tile-b",8,8));
            await File.WriteAllTextAsync(Path.Combine(assets,atlas),new JsonObject{
                ["schema_version"]="1.0",["image"]=image,["image_size"]=new JsonArray(16,16),["mask_color"]="#FF00FF",["background_alpha"]=255,
                ["pixels_per_meter"]=id=="test-body"?1:4,["frames"]=frames}.ToJsonString());
            await File.WriteAllTextAsync(Path.Combine(assets,bundle),JsonSerializer.Serialize(new{
                schema_version="1.0",asset_id=id,directions=new[]{"S"},
                components=new[]{new{id="base",parent=(string?)null,atlases=new Dictionary<string,string>{{"S",atlas}}}},
                sampled_poses=new[]{new{direction="S",normalized_time=0,nodes=new Dictionary<string,object>{{"base",new{frame_index=0,world_matrix_2d=new[]{new[]{1,0,0},new[]{0,1,0},new[]{0,0,1}}}}}}}}));
            manifests[id]=new(2,id,id,"v001","test-only-not-production-art",ArtApprovalState.Approved,
                [new("sheet",image,atlas,sha,Hash(Path.Combine(assets,atlas)),"base",["S"])],[]);
        }
        MetadataProductionFrame Ref(string id,string frame)=>new(new(id,"v001",id+".bundle.json","base","S","sheet",frame),new(id+".png",Hash(Path.Combine(assets,id+".png"))));
        var spec=new MetadataProductionSpecification(1,StructureFirstPipeline.MetadataProductionDigest(inventory),inventory.Targets.Select(t=>
            t.Kind==MetadataProductionTargetKind.Member?new MetadataProductionBinding(t.Id,[Ref("test-body","body")],["g0","g1","g2","g3"]):
            new MetadataProductionBinding(t.Id,[Ref("test-tiles","tile-a"),Ref("test-tiles","tile-b")],TileWidthMeters:4,TileHeightMeters:2)).ToArray());
        await File.WriteAllTextAsync(Path.Combine(root,"bindings.json"),Json(spec));
        var registry=new Registry(manifests);var allowed=new Authority(true);var denied=new Authority(false);
        Task<MetadataProductionAssembly> Bind(MetadataProductionSpecification value,IGameArtsApprovalAuthority? authority=null,IGameArtsApprovalAuthority? concepts=null)=>
            pipeline.BindMetadataProductionAsync(scene,value,images,assets,registry,authority??allowed,concepts??allowed);
        var result=await pipeline.ExportMetadataProductionAsync(scene,spec,images,assets,registry,allowed,allowed,Path.Combine(root,"assembly.json"));
        Check(result.Status=="StaticBindingsVerified"&&result.Targets.Count==10&&result.Targets.Sum(t=>t.Frames.Count)==18,"complete static/tile-library bindings, not a rasterized native world");
        Check(result.RemainingGates.Any(g=>g.Contains("transition"))&&result.RemainingGates.Any(g=>g.Contains("Moving")),"unverified native topology and moving discovery remain gates");
        await pipeline.ValidateMetadataProductionAssemblyAsync(scene,result,images,assets,registry,allowed,allowed);
        var reversed=await Bind(spec with{Bindings=spec.Bindings.Reverse().Select(b=>b with{Frames=b.Frames.Reverse().ToArray()}).ToArray()});
        Check(Json(result)==Json(reversed),"binding/frame declaration order invariant");
        await Reject(()=>Bind(spec with{InventorySha256=new string('0',64)}),"BINDING_COVERAGE_OR_STALE");
        await Reject(()=>Bind(spec with{Bindings=spec.Bindings.Skip(1).ToArray()}),"BINDING_COVERAGE_OR_STALE");
        await Reject(()=>Bind(spec with{Bindings=spec.Bindings.Skip(1).Append(spec.Bindings[1]).ToArray()}),"BINDING_COVERAGE_OR_STALE");
        await Reject(()=>Bind(spec,concepts:denied),"CONCEPT_NOT_APPROVED");
        await Reject(()=>Bind(spec,authority:denied),"LIFECYCLE_APPROVAL_NOT_VERIFIED");
        await Reject(()=>Bind(spec with{Bindings=spec.Bindings.Select(b=>b.GroundAnchorNames is null?b:b with{GroundAnchorNames=["g1","g0","g2","g3"]}).ToArray()}),"GROUND_CALIBRATION_MISMATCH");
        await Reject(()=>Bind(spec with{Bindings=spec.Bindings.Select(b=>b.TileWidthMeters is null?b:b with{TileWidthMeters=2}).ToArray()}),"TILE_SCALE_MISMATCH");
        await Reject(()=>Bind(spec with{Bindings=spec.Bindings.Select(b=>b with{Frames=b.Frames.Select(f=>f with{Master=f.Master with{Sha256=new string('0',64)}}).ToArray()}).ToArray()}),"MASTER_LINEAGE_MISMATCH");
        await Reject(()=>pipeline.ValidateMetadataProductionAssemblyAsync(scene,result with{Status="NativeReady"},images,assets,registry,allowed,allowed),"ASSEMBLY_REPLAY_MISMATCH");
        manifests["test-body"]=manifests["test-body"] with{ApprovalState=ArtApprovalState.Revoked};
        await Reject(()=>Bind(spec),"MANIFEST_NOT_APPROVED");manifests["test-body"]=manifests["test-body"] with{ApprovalState=ArtApprovalState.Approved};
        string body=Path.Combine(assets,"test-body.png");await File.AppendAllTextAsync(body,"changed");await Reject(()=>Bind(spec),"ASSET_HASH_MISMATCH");await File.WriteAllBytesAsync(body,png);
        var cts=new CancellationTokenSource();cts.Cancel();
        try{await pipeline.PlanMetadataProductionAsync(scene,images,token:cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        string saved=Hash(Path.Combine(root,"assembly.json"));
        try{await pipeline.ExportMetadataProductionAsync(scene,spec,images,assets,registry,allowed,allowed,Path.Combine(root,"assembly.json"));throw new Exception("Overwrite accepted");}catch(IOException){}
        Check(saved==Hash(Path.Combine(root,"assembly.json"))&&!Directory.GetFiles(root,"*.tmp").Any(),"existing output preserved and no staging leftovers");
        await File.WriteAllTextAsync(Path.Combine(root,"STATUS.md"),"Synthetic technical fixture only. Test approval authorities do not approve actual art. No native world, tile transition, animation or visual acceptance.\n");
        Console.WriteLine("PASS: final-map production inventory; all member/surface/passage targets, exact contours/widths, measured static/tile frames, source/hash/lifecycle/scale/anchor rejection, replay, ordering, cancellation and preserved output. Synthetic assets only; native runtime remains unverified.");
    }
    private sealed class Authority(bool allow):IGameArtsApprovalAuthority
    {public Task<bool> IsApprovedAsync(string id,string revision,CancellationToken token=default){token.ThrowIfCancellationRequested();return Task.FromResult(allow);} public Task<bool> IsApprovedArtifactAsync(string id,string revision,string hash,CancellationToken token=default)=>IsApprovedAsync(id,revision,token); }
    private sealed class Registry(Dictionary<string,ApprovedArtManifest> manifests):IApprovedArtRegistry
    {public Task<ApprovedArtManifest?> ResolveAsync(string id,CancellationToken token=default){token.ThrowIfCancellationRequested();return Task.FromResult(manifests.GetValueOrDefault(id));}}
}
