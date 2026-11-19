using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Capture;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Godot;

public sealed record GodotTerrainPatternFrame(int Mask,string FrameReferenceSha256);
public sealed record GodotStaticTerrainLayer(string Id,IReadOnlyList<string> ChannelIds,int ZIndex,
    IReadOnlyList<GodotTerrainPatternFrame> Patterns);
public sealed record GodotStaticWorldSpecification(int Version,string AssemblySha256,string TopologySha256,
    double WorldPixelsPerMeter,int StructuresZIndex,IReadOnlyList<GodotStaticTerrainLayer> Layers,GodotMovementSpecification? Movement=null);
public sealed record GodotNativeTexture(string File,string Sha256,int Width,int Height);
public sealed record GodotNativePattern(int Mask,GameArtsMeasuredFrame Frame,string TextureFile);
public sealed record GodotNativeTerrainLayer(string Id,IReadOnlyList<string> ChannelIds,IReadOnlyList<WorldTileCell> Cells,
    int ZIndex,IReadOnlyList<GodotNativePattern> Patterns);
public sealed record GodotNativeStructure(MetadataProductionTarget Target,GameArtsMeasuredFrame Frame,string TextureFile,
    IReadOnlyList<PixelAnchor> GroundAnchors,GameArtsAnimationClip? Animation=null);
public sealed record GodotStaticWorldPacket(int Version,string Kind,string Status,string AssemblySha256,string WorldSha256,
    double WorldPixelsPerMeter,int StructuresZIndex,WorldTileTopologyPlan Topology,IReadOnlyList<GodotNativeTerrainLayer> Groups,
    IReadOnlyList<GodotNativeStructure> Structures,IReadOnlyList<string> MemberIds,IReadOnlyList<GodotNativeTexture> Textures,
    IReadOnlyList<string> RemainingGates,GodotMovementPacket? Movement=null);

public sealed partial class GodotStructureLayerExporter
{
    public async Task<GodotStaticWorldPacket> ExportMetadataAsync(MetadataSceneCompilation scene,MetadataProductionAssembly assembly,
        WorldTileTopologyPlan topology,GodotStaticWorldSpecification specification,string trustedImageRoot,string trustedAssetRoot,
        IApprovedArtRegistry registry,IGameArtsApprovalAuthority assetApprovals,IGameArtsApprovalAuthority conceptApprovals,
        string newDirectory,ISettlementCatalogRegistry? catalogs=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(scene);ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(topology);ArgumentNullException.ThrowIfNull(specification);
        T Copy<T>(T input)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(input,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        scene=Copy(scene);assembly=Copy(assembly);topology=Copy(topology);specification=Copy(specification);
        if(specification.Version!=1||specification.AssemblySha256!=WorldTileTopologyPlanner.Digest(assembly)||
            specification.TopologySha256!=WorldTileTopologyPlanner.Digest(topology)||specification.Layers is not {Count:>=1 and <=128}||
            !double.IsFinite(specification.WorldPixelsPerMeter)||specification.WorldPixelsPerMeter is <=0 or >1024||
            specification.StructuresZIndex is <-1024 or >1024)
            throw new ArgumentException("GODOT_STATIC_SPECIFICATION_INVALID_OR_STALE");
        var groups=TopologyGroups(topology,new(1,specification.TopologySha256,specification.Layers.Select(l=>
            l is null?null!:new GodotTerrainTopologyGroup(l.Id,l.ChannelIds)).ToArray()),token);
        await new StructureFirstPipeline().ValidateMetadataProductionAssemblyAsync(scene,assembly,trustedImageRoot,trustedAssetRoot,
            registry,assetApprovals,conceptApprovals,new WorldMetadataGenerator(catalogs),token);
        new WorldTileTopologyPlanner().Validate(scene.World,topology,catalogs,token);
        if(!assembly.Targets.Where(t=>t.Target.Kind!=MetadataProductionTargetKind.Member).Select(t=>t.Target.Id).ToHashSet(StringComparer.Ordinal)
            .SetEquals(topology.Channels.Select(c=>c.Source.Id)))throw new ArgumentException("GODOT_STATIC_TARGET_COVERAGE");
        var targets=assembly.Targets.ToDictionary(t=>t.Target.Id,StringComparer.Ordinal);var textures=new Dictionary<string,GodotNativeTexture>(StringComparer.Ordinal);
        long decodedPixels=0;
        string Texture(GameArtsMeasuredFrame frame)
        {
            if(frame.ImageWidth is <1 or >16384||frame.ImageHeight is <1 or >16384||(long)frame.ImageWidth*frame.ImageHeight>100000000)
                throw new ArgumentException("GODOT_STATIC_IMAGE_DIMENSION_LIMIT");
            string relative="textures/"+frame.ImageSha256.ToLowerInvariant()+".png";
            var texture=new GodotNativeTexture(relative,frame.ImageSha256.ToLowerInvariant(),frame.ImageWidth,frame.ImageHeight);
            if(textures.TryGetValue(relative,out var prior)&&prior!=texture)throw new ArgumentException("GODOT_STATIC_TEXTURE_CONFLICT");
            if(!textures.ContainsKey(relative)&&(decodedPixels+=(long)frame.ImageWidth*frame.ImageHeight)>100000000)
                throw new ArgumentException("GODOT_STATIC_DECODED_PIXEL_LIMIT");
            textures[relative]=texture;return relative;
        }
        var layers=new List<GodotNativeTerrainLayer>();
        foreach(var group in groups)
        {
            var rule=specification.Layers.Single(l=>l.Id==group.Id);
            if(rule.ZIndex is <-1024 or >1024||rule.Patterns is not {Count:>=1 and <=256}||rule.Patterns.Any(p=>p is null||p.Mask is <0 or >255||p.FrameReferenceSha256 is null)||
                rule.Patterns.Select(p=>p.Mask).Distinct().Count()!=rule.Patterns.Count)
                throw new ArgumentException("GODOT_STATIC_PATTERNS_INVALID:"+group.Id);
            var common=group.ChannelIds.Select(id=>targets[id].Frames.Select(f=>WorldTileTopologyPlanner.Digest(f.Reference)).ToHashSet(StringComparer.Ordinal))
                .Aggregate((a,b)=>{a.IntersectWith(b);return a;});
            var patterns=new List<GodotNativePattern>();
            foreach(var declared in rule.Patterns.OrderBy(p=>p.Mask))
            {
                if(!common.Contains(declared.FrameReferenceSha256))throw new ArgumentException("GODOT_STATIC_PATTERN_FRAME_NOT_BOUND:"+group.Id);
                var frame=targets[group.ChannelIds[0]].Frames.Single(f=>WorldTileTopologyPlanner.Digest(f.Reference)==declared.FrameReferenceSha256);
                if(Math.Abs(frame.Width/frame.PixelsPerMeter-topology.Request.CellWidthMeters)>1e-7||
                    Math.Abs(frame.Height/frame.PixelsPerMeter-topology.Request.CellHeightMeters)>1e-7)
                    throw new ArgumentException("GODOT_STATIC_TILE_RESOLUTION_MISMATCH:"+group.Id);
                if(patterns.Count>0&&(frame.Width!=patterns[0].Frame.Width||frame.Height!=patterns[0].Frame.Height))
                    throw new ArgumentException("GODOT_STATIC_LAYER_PIXEL_SIZE_MISMATCH:"+group.Id);
                patterns.Add(new(declared.Mask,frame,Texture(frame)));
            }
            layers.Add(new(group.Id,group.ChannelIds,group.Cells,rule.ZIndex,patterns));
        }
        var structures=new List<GodotNativeStructure>();
        foreach(var target in assembly.Targets.Where(t=>t.Target.Kind==MetadataProductionTargetKind.Member).OrderBy(t=>t.Target.Id,StringComparer.Ordinal))
        {
            var frame=target.Frames.Single();var names=assembly.Specification.Bindings.Single(b=>b.TargetId==target.Target.Id).GroundAnchorNames!;
            if(target.Animation is {} animation)foreach(var animatedFrame in animation.Components.SelectMany(c=>c.Frames))Texture(animatedFrame);
            structures.Add(new(target.Target,frame,Texture(frame),names.Select(n=>frame.Anchors[n]).ToArray(),target.Animation));
        }
        var packet=new GodotStaticWorldPacket(1,"StaticWorld","NativeProductionCompilationPending",specification.AssemblySha256,assembly.Inventory.WorldSha256,
            specification.WorldPixelsPerMeter,specification.StructuresZIndex,topology,layers,structures,
            assembly.Inventory.Members.Select(m=>m.NodeId).Order(StringComparer.Ordinal).ToArray(),textures.Values.OrderBy(t=>t.File,StringComparer.Ordinal).ToArray(),
            ["Actual canonical GameArts lifecycle/artwork and source-to-runtime visual acceptance",
             "Configured site-feature contents and moving-component discovery/animation",
             specification.Movement is null?"Native collision/navigation not requested or verified":
                "Declared exterior actor journeys require native verification; interiors, doors and other gameplay journeys remain unverified",
             "Whole-city/game acceptance; static compilation is not playable-world completion"],MovementPacket(scene.World,assembly,specification.Movement,token));
        string targetDirectory=Path.GetFullPath(newDirectory),parent=Path.GetDirectoryName(targetDirectory)!;
        if(Directory.Exists(targetDirectory)||File.Exists(targetDirectory))throw new IOException("GODOT_STATIC_OUTPUT_EXISTS");
        Directory.CreateDirectory(parent);string staging=Path.Combine(parent,".native-world-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        try
        {
            Directory.CreateDirectory(Path.Combine(staging,"textures"));long bytes=0;
            foreach(var texture in packet.Textures)
            {
                var source=assembly.Targets.SelectMany(t=>t.Frames.Concat(t.Animation?.Components.SelectMany(c=>c.Frames)??[])).First(f=>string.Equals(f.ImageSha256,texture.Sha256,StringComparison.OrdinalIgnoreCase));
                string path=MockupCapturePipeline.ResolveSourcePath(trustedAssetRoot,source.ImagePath);
                byte[] data;
                await using(var stream=File.OpenRead(path))
                {
                    if(stream.Length is <=0 or >67108864||(bytes+=stream.Length)>268435456)
                        throw new ArgumentException("GODOT_STATIC_IMAGE_BYTE_LIMIT");
                    data=new byte[(int)stream.Length];await stream.ReadExactlyAsync(data,token);
                }
                if(!Convert.ToHexStringLower(SHA256.HashData(data)).Equals(texture.Sha256,StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("GODOT_STATIC_IMAGE_CHANGED");
                await File.WriteAllBytesAsync(Path.Combine(staging,texture.File),data,token);
            }
            await WriteNativeCompilerAsync(staging,packet,true,token);
            await File.WriteAllTextAsync(Path.Combine(staging,"world-metadata.json"),JsonSerializer.Serialize(scene.World,WorldJson.CreateOptions(true)),token);
            await File.WriteAllTextAsync(Path.Combine(staging,"production-assembly.json"),JsonSerializer.Serialize(assembly,WorldJson.CreateOptions(true)),token);
            await File.WriteAllTextAsync(Path.Combine(staging,"STATUS.md"),"Native static compilation pending. Run the installed Godot with --headless --path <this-project> --script res://compile.gd. " +
                "Every supplied image is copied byte-for-byte after external approval/file validation. The compiler uses native TileMapLayer/TileSet/Sprite2D/AtlasTexture and checks native save/reload. " +
                "Requested exterior movement remains pending until native physics/navigation verification; interiors, animation, actual artwork quality and whole-city acceptance remain separate gates. Lifecycle approval is a point-in-time export gate; revalidate before final use/publication.\n",token);
            await PublishNativeDirectoryAsync(staging,targetDirectory,token);return packet;
        }
        finally
        {
            if(Directory.Exists(staging)&&Path.GetDirectoryName(Path.GetFullPath(staging))==parent&&Path.GetFileName(staging).StartsWith(".native-world-",StringComparison.Ordinal))
                Directory.Delete(staging,true);
        }
    }
}
