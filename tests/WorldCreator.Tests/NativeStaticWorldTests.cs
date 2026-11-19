using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using WorldCreator.Core;
using WorldCreator.Core.Capture;
using WorldCreator.Core.Spatial;
using WorldCreator.Godot;

internal static class NativeStaticWorldTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    static string Hash(string file)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
    static void Check(bool value,string message){if(!value)throw new Exception("Native static world: "+message);}
    static async Task Reject(Func<Task> action,string code)
    {try{await action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}

    public static async Task RunAsync(string sceneFile,string imageRoot,string measuredReceipt,string directory,bool movement=false,string? componentDirectory=null)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Test output exists");
        string root=Path.GetFullPath(directory);Directory.CreateDirectory(root);
        var scene=await StructureFirstPipeline.ReadJsonAsync<MetadataSceneCompilation>(sceneFile);
        var pipeline=new StructureFirstPipeline();var inventory=await pipeline.PlanMetadataProductionAsync(scene,imageRoot);
        string assets=Path.Combine(root,"test-assets");Directory.CreateDirectory(assets);
        var png=await File.ReadAllBytesAsync("tests/fixtures/mockup-atlas/closed.png");
        var manifests=new Dictionary<string,ApprovedArtManifest>(StringComparer.Ordinal);
        foreach(string name in new[]{"body","tiles"})
        {
            await File.WriteAllBytesAsync(Path.Combine(assets,name+".png"),png);string hash=Hash(Path.Combine(assets,name+".png"));
            var frame=new JsonObject{["name"]=name,["rect"]=name=="body"?new JsonArray(0,0,16,16):new JsonArray(4,4,8,8),
                ["pivot"]=name=="body"?new JsonArray(8,8):new JsonArray(4,4),["source_sha256"]=hash};
            if(name=="body")frame["anchors"]=new JsonObject{["g0"]=new JsonArray(5,6),["g1"]=new JsonArray(11,6),["g2"]=new JsonArray(11,10),["g3"]=new JsonArray(5,10)};
            await File.WriteAllTextAsync(Path.Combine(assets,name+".atlas.json"),new JsonObject{
                ["schema_version"]="1.0",["image"]=name+".png",["image_size"]=new JsonArray(16,16),["mask_color"]="#FF00FF",["background_alpha"]=255,
                ["pixels_per_meter"]=name=="body"?1:16,["frames"]=new JsonArray(frame)}.ToJsonString());
            await File.WriteAllTextAsync(Path.Combine(assets,name+".bundle.json"),JsonSerializer.Serialize(new{
                schema_version="1.0",asset_id="native-test-"+name,directions=new[]{"S"},
                components=new[]{new{id="base",parent=(string?)null,atlases=new Dictionary<string,string>{{"S",name+".atlas.json"}}}},
                sampled_poses=new[]{new{direction="S",normalized_time=0,nodes=new Dictionary<string,object>{{"base",new{frame_index=0,world_matrix_2d=new[]{new[]{1,0,0},new[]{0,1,0},new[]{0,0,1}}}}}}}}));
            manifests["native-test-"+name]=new(2,"native-test-"+name,"native-test-"+name,"v001","synthetic-test-only",ArtApprovalState.Approved,
                [new("sheet",name+".png",name+".atlas.json",name=="body"?hash.ToUpperInvariant():hash,Hash(Path.Combine(assets,name+".atlas.json")),"base",["S"])],[]);
        }
        var animationRequests=new Dictionary<string,GameArtsAnimationRequest>();
        if(componentDirectory is not null)
        {
            foreach(string file in Directory.GetFiles(componentDirectory,"*",SearchOption.AllDirectories))
            {
                string target=Path.Combine(assets,"components",Path.GetRelativePath(componentDirectory,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);
            }
            var manifest=await StructureFirstPipeline.ReadJsonAsync<ApprovedArtManifest>(Path.Combine(componentDirectory,"manifest.json"));
            manifests[manifest.AssetId]=manifest with{Sheets=manifest.Sheets.Select(s=>s with{ImagePath="components/"+s.ImagePath,AtlasPath="components/"+s.AtlasPath}).ToArray()};
            foreach(string facing in new[]{"S","E"})
            {
                var request=await StructureFirstPipeline.ReadJsonAsync<GameArtsAnimationRequest>(Path.Combine(componentDirectory,"animation-"+facing+".json"));
                animationRequests[facing]=request with{BundlePath="components/"+request.BundlePath,Sources=request.Sources.Select(s=>s with{Master=s.Master with{File="components/"+s.Master.File}}).ToArray()};
            }
        }
        MetadataProductionFrame Reference(string name)=>new(new("native-test-"+name,"v001",name+".bundle.json","base","S","sheet",name),new(name+".png",Hash(Path.Combine(assets,name+".png"))));
        MetadataProductionBinding Member(MetadataProductionTarget target)
        {
            if(componentDirectory is null)return new(target.Id,[Reference("body")],["g0","g1","g2","g3"]);
            var animation=animationRequests[target.SourceId.EndsWith("-0",StringComparison.Ordinal)?"S":"E"];var source=animation.Sources.Single(s=>s.ComponentId=="base");
            return new(target.Id,[new(new(animation.AssetId,animation.Revision,animation.BundlePath,"base",animation.Facing,"base:"+animation.Facing,source.FrameName),source.Master)],
                ["g0","g1","g2","g3"],Animation:animation);
        }
        var binding=new MetadataProductionSpecification(1,WorldTileTopologyPlanner.Digest(inventory),inventory.Targets.Select(t=>t.Kind==MetadataProductionTargetKind.Member?
            Member(t):
            new MetadataProductionBinding(t.Id,[Reference("tiles")],TileWidthMeters:.5,TileHeightMeters:.5)).ToArray());
        var registry=new Registry(manifests);var allowed=new Authority(true);var denied=new Authority(false);
        var assembly=await pipeline.BindMetadataProductionAsync(scene,binding,imageRoot,assets,registry,allowed,allowed);
        var owner=new WorldTileTopologyPlanner();var plan=owner.Plan(scene.World,new(1,WorldTileTopologyPlanner.Digest(scene.World),new(0,0),.5,.5,.36));
        using var measured=JsonDocument.Parse(await File.ReadAllTextAsync(measuredReceipt));
        var materialGroups=measured.RootElement.GetProperty("Groups").EnumerateArray().Select(g=>new GodotStaticTerrainLayer(g.GetProperty("Id").GetString()!,
            g.GetProperty("ChannelIds").EnumerateArray().Select(x=>x.GetString()!).ToArray(),g.GetProperty("Id").GetString()=="passages"?20:g.GetProperty("Id").GetString()=="surface-0"?0:5,
            g.GetProperty("RequiredMasks").EnumerateArray().Select(mask=>new GodotTerrainPatternFrame(mask.GetInt32(),WorldTileTopologyPlanner.Digest(Reference("tiles").Production))).ToArray())).ToArray();
        var spec=new GodotStaticWorldSpecification(1,WorldTileTopologyPlanner.Digest(assembly),WorldTileTopologyPlanner.Digest(plan),32,30,materialGroups);
        if(movement)
        {
            var journeys=new GodotMovementJourney[]{new("between-entrances","Required exterior entrance journey","scene:front",
                scene.World.AccessPlan!.Sites[0].Ports[0].ApproachPosition,scene.World.AccessPlan.Sites[1].Ports[0].ApproachPosition),
                new("around-protected-ground","Route around declared protected contour","scene:protected",new(76,82),new(95,82))};
            spec=spec with{Movement=new(1,1,[new("small","Small actor exterior domain",.25,.05,.05,.005,journeys),new("large","Large actor exterior domain",1,.05,.05,.005,journeys)])};
        }
        var exporter=new GodotStructureLayerExporter();
        Task<GodotStaticWorldPacket> Export(GodotStaticWorldSpecification value,string folder,IGameArtsApprovalAuthority? authority=null)=>exporter.ExportMetadataAsync(scene,assembly,plan,value,imageRoot,assets,registry,authority??allowed,allowed,Path.Combine(root,folder));
        var result=await Export(spec,"world");
        File.Copy("tests/fixtures/verify-native-static.gd",Path.Combine(root,"world","verify.gd"));
        if(movement)
        {
            File.Copy("tests/fixtures/verify-native-movement.gd",Path.Combine(root,"world","verify-movement.gd"));
            Check(result.Movement is {Obstacles.Count:3}&&result.Movement.Specification.Profiles.Count==2,"two solid footprints/protected contour, separate explicit actor profiles");
            var profile=spec.Movement!.Profiles[0];var journey=profile.Journeys[0];
            await Reject(()=>Export(spec with{Movement=spec.Movement with{Profiles=[profile with{Journeys=[journey with{From=new(85,80)}]}]}},"refused-movement-source"),"MOVEMENT_JOURNEY_INVALID");
            await Reject(()=>Export(spec with{Movement=spec.Movement with{Profiles=[profile with{RadiusMeters=0}]}},"refused-movement-radius"),"MOVEMENT_PROFILE_INVALID");
            await Export(spec with{Movement=spec.Movement with{Profiles=[profile with{RadiusMeters=10}]}},"unreachable-movement");
        }
        Check(result.Structures.Count==2&&result.MemberIds.Count==6&&result.Groups.Count==3&&result.Groups.Sum(g=>g.Patterns.Count)==42,"all structures and identities, three native layers, 42 explicit source pattern bindings");
        Check(componentDirectory is null?result.Textures.Count==1:result.Structures.All(s=>s.Animation?.Components.Count==3),"static pixels deduplicate; animated members retain all nested parts");
        if(componentDirectory is not null)
        {
            File.Copy("tests/fixtures/verify-native-components.gd",Path.Combine(root,"world","verify-components.gd"));
            var gate=new GameArtsProductionGate(allowed);var request=animationRequests["S"];
            await Reject(()=>gate.ResolveAnimationAsync(request with{Sources=request.Sources.Skip(1).ToArray()},registry,assets),"FRAME_SOURCE_COVERAGE");
            await Reject(()=>gate.ResolveAnimationAsync(request with{MaximumPoseIntervalSeconds=request.MaximumPoseIntervalSeconds/2},registry,assets),"SAMPLE_INTERVAL_EXCEEDED");
            await Reject(()=>gate.ResolveAnimationAsync(request with{Loop=true},registry,assets),"LOOP_SEAM_MISMATCH");
            string bundleFile=Path.Combine(assets,request.BundlePath);var original=await File.ReadAllBytesAsync(bundleFile);
            try
            {
                var altered=JsonNode.Parse(original)!;
                var pose=altered["sampled_poses"]!.AsArray().Single(p=>p!["direction"]!.GetValue<string>()=="S"&&p["normalized_time"]!.GetValue<double>()==.25)!;
                var joint=pose["nodes"]!["joint"]!;
                var second=assembly.Targets.First(t=>t.Animation?.Request.Facing=="S").Animation!.Components.Single(c=>c.Id=="joint").Frames[1];
                joint["frame_index"]=1;joint["image"]=Path.GetFileName(second.ImagePath);
                joint["frame_rect"]=new JsonArray(second.X,second.Y,second.Width,second.Height);joint["pivot"]=new JsonArray(second.Pivot.X,second.Pivot.Y);
                await File.WriteAllTextAsync(bundleFile,altered.ToJsonString());
                await Reject(()=>gate.ResolveAnimationAsync(request,registry,assets),"FRAME_TIMING_MISMATCH");
                altered=JsonNode.Parse(original)!;
                var rows=altered["sampled_poses"]!.AsArray();
                rows.Remove(rows.Single(p=>p!["direction"]!.GetValue<string>()=="S"&&p["normalized_time"]!.GetValue<double>()==.5));
                await File.WriteAllTextAsync(bundleFile,altered.ToJsonString());
                await Reject(()=>gate.ResolveAnimationAsync(request with{MaximumPoseIntervalSeconds=1},registry,assets),"FRAME_BOUNDARY_POSE_REQUIRED");
                altered=JsonNode.Parse(original)!;
                pose=altered["sampled_poses"]!.AsArray().Single(p=>p!["direction"]!.GetValue<string>()=="S"&&p["normalized_time"]!.GetValue<double>()==.25)!;
                pose["nodes"]!["base"]!["world_matrix_2d"]![0]![2]=1;
                await File.WriteAllTextAsync(bundleFile,altered.ToJsonString());
                await Reject(()=>pipeline.BindMetadataProductionAsync(scene,binding,imageRoot,assets,registry,allowed,allowed),"ANIMATED_ROOT_MUST_REMAIN_FIXED");
            }
            finally{await File.WriteAllBytesAsync(bundleFile,original);}
        }
        await Reject(()=>Export(spec with{AssemblySha256=new string('0',64)},"refused-stale"),"SPECIFICATION_INVALID_OR_STALE");
        await Reject(()=>Export(spec with{Layers=spec.Layers.Skip(1).ToArray()},"refused-coverage"),"CHANNEL_COVERAGE");
        await Reject(()=>Export(spec with{Layers=spec.Layers.Select(l=>l with{Patterns=l.Patterns.Select(p=>p with{FrameReferenceSha256=new string('0',64)}).ToArray()}).ToArray()},"refused-frame"),"PATTERN_FRAME_NOT_BOUND");
        await Reject(()=>Export(spec,"refused-approval",denied),"LIFECYCLE_APPROVAL_NOT_VERIFIED");
        var changed=assembly with{Status="NativeReady"};
        await Reject(()=>exporter.ExportMetadataAsync(scene,changed,plan,spec with{AssemblySha256=WorldTileTopologyPlanner.Digest(changed)},imageRoot,assets,registry,allowed,allowed,Path.Combine(root,"refused-replay")),"ASSEMBLY_REPLAY_MISMATCH");
        string before=Hash(Path.Combine(root,"world","input.json"));
        try{await Export(spec,"world");throw new Exception("Overwrite accepted");}catch(IOException){}
        Check(before==Hash(Path.Combine(root,"world","input.json")),"existing project remains intact");
        await NativePublicationTests.VerifyAsync(root);
        var cts=new CancellationTokenSource();cts.Cancel();
        try{await exporter.ExportMetadataAsync(scene,assembly,plan,spec,imageRoot,assets,registry,allowed,allowed,Path.Combine(root,"refused-cancel"),token:cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
        await Export(spec with{Layers=spec.Layers.Select(l=>l with{Patterns=l.Patterns.Where(p=>p.Mask!=255).ToArray()}).ToArray()},"missing-pattern");
        var probeSpec=new GodotTerrainTopologySpecification(1,WorldTileTopologyPlanner.Digest(plan),materialGroups.Select(g=>new GodotTerrainTopologyGroup(g.Id,g.ChannelIds)).ToArray());
        await exporter.ExportTopologyProbeAsync(scene.World,plan,probeSpec,Path.Combine(root,"probe"));
        string corrupt=Path.Combine(root,"changed-image");Directory.CreateDirectory(Path.Combine(corrupt,"textures"));
        foreach(string name in new[]{"input.json","compile.gd","movement.gd","component-animation.gd","project.godot"})File.Copy(Path.Combine(root,"world",name),Path.Combine(corrupt,name));
        foreach(var texture in result.Textures)
        {File.Copy(Path.Combine(root,"world",texture.File),Path.Combine(corrupt,texture.File));await File.AppendAllTextAsync(Path.Combine(corrupt,texture.File),"changed");}
        foreach(var name in new[]{"assembly","bindings","specification","plan"})
            await File.WriteAllTextAsync(Path.Combine(root,name+".json"),name switch{"assembly"=>Json(assembly),"bindings"=>Json(binding),"specification"=>Json(spec),_=>Json(plan)});
        Check(!Directory.EnumerateDirectories(root,".native-world-*").Any(),"no staging leftovers");
        await File.WriteAllTextAsync(Path.Combine(root,"STATUS.md"),"Synthetic fixture and test approval authorities only. The same test image is deliberately reused for every terrain pattern; this is not real transition artwork or an approved city. Native executions and actual source-to-runtime art/physics/gameplay acceptance are separate gates.\n");
        Console.WriteLine("PASS: native static-world packet, all members/structures, exact bound frames and cell dimensions, 42 explicit pattern bindings, copied hashes, stale/missing/frame/approval/replay/cancellation/overwrite gates. Execute native compiler projects separately; actual artwork acceptance remains unfinished.");
    }
    private sealed class Registry(Dictionary<string,ApprovedArtManifest> manifests):IApprovedArtRegistry
    {public Task<ApprovedArtManifest?> ResolveAsync(string id,CancellationToken token=default){token.ThrowIfCancellationRequested();return Task.FromResult(manifests.GetValueOrDefault(id));}}
    private sealed class Authority(bool allowed):IGameArtsApprovalAuthority
    {public Task<bool> IsApprovedAsync(string id,string revision,CancellationToken token=default){token.ThrowIfCancellationRequested();return Task.FromResult(allowed);} public Task<bool> IsApprovedArtifactAsync(string id,string revision,string hash,CancellationToken token=default)=>IsApprovedAsync(id,revision,token); }
}
