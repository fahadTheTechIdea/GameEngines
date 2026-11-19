using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Godot;

public sealed record GodotTerrainTopologyGroup(string Id,IReadOnlyList<string> ChannelIds);
public sealed record GodotTerrainTopologySpecification(int Version,string PlanSha256,IReadOnlyList<GodotTerrainTopologyGroup> Groups);
public sealed record GodotTerrainTopologyGroupPacket(string Id,IReadOnlyList<string> ChannelIds,IReadOnlyList<WorldTileCell> Cells);
public sealed record GodotTerrainTopologyPacket(int Version,string Status,string PlanSha256,
    WorldTileTopologyPlan Topology,IReadOnlyList<GodotTerrainTopologyGroupPacket> Groups,IReadOnlyList<string> RemainingGates,string Kind="TerrainProbe");

public sealed partial class GodotStructureLayerExporter
{
    /// <summary>Writes a native terrain-combination measurement project. Its temporary
    /// transparent tile sources are algorithm data, never production art.</summary>
    public async Task<GodotTerrainTopologyPacket> ExportTopologyProbeAsync(WorldMetadataDocument world,WorldTileTopologyPlan plan,
        GodotTerrainTopologySpecification specification,string newDirectory,ISettlementCatalogRegistry? catalogs=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(world);ArgumentNullException.ThrowIfNull(plan);
        world=JsonSerializer.Deserialize<WorldMetadataDocument>(JsonSerializer.Serialize(world,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        plan=JsonSerializer.Deserialize<WorldTileTopologyPlan>(JsonSerializer.Serialize(plan,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        new WorldTileTopologyPlanner().Validate(world,plan,catalogs,token);
        specification=JsonSerializer.Deserialize<GodotTerrainTopologySpecification>(JsonSerializer.Serialize(specification,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        var groups=TopologyGroups(plan,specification,token);
        var packet=new GodotTerrainTopologyPacket(1,"NativeTerrainMeasurementPending",specification.PlanSha256,plan,groups,
            ["Execute the native terrain probe and verify save/reload",
             "Produce and bind approved measured artwork for every required terrain combination",
             "Production TileMapLayer/structure compilation, collision/navigation, animation and visual acceptance"]);
        string target=Path.GetFullPath(newDirectory),parent=Path.GetDirectoryName(target)!;
        if(Directory.Exists(target)||File.Exists(target))throw new IOException("GODOT_TOPOLOGY_OUTPUT_EXISTS");
        Directory.CreateDirectory(parent);string staging=Path.Combine(parent,".tile-topology-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        try
        {
            await WriteNativeCompilerAsync(staging,packet,false,token);
            await File.WriteAllTextAsync(Path.Combine(staging,"STATUS.md"),"Technical terrain-combination measurement only. Temporary transparent native tile sources are algorithm data, not artwork. " +
                "Run the installed Godot headlessly with --path <this-directory> --script res://compile.gd. " +
                "A successful receipt requires exact occupancy and native PackedScene save/reload. No production/collision/navigation/visual acceptance is granted.\n",token);
            await PublishNativeDirectoryAsync(staging,target,token);return packet;
        }
        finally
        {
            if(Directory.Exists(staging)&&Path.GetDirectoryName(Path.GetFullPath(staging))==parent&&Path.GetFileName(staging).StartsWith(".tile-topology-",StringComparison.Ordinal))
                Directory.Delete(staging,true);
        }
    }
    private static IReadOnlyList<GodotTerrainTopologyGroupPacket> TopologyGroups(WorldTileTopologyPlan plan,
        GodotTerrainTopologySpecification specification,CancellationToken token)
    {
        if(specification.Version!=1||specification.PlanSha256!=WorldTileTopologyPlanner.Digest(plan)||specification.Groups is not {Count:>=1 and <=128})
            throw new ArgumentException("GODOT_TOPOLOGY_SPECIFICATION_INVALID_OR_STALE");
        var channels=plan.Channels.ToDictionary(c=>c.Source.Id,StringComparer.Ordinal);var owners=new HashSet<string>(StringComparer.Ordinal);
        var names=new HashSet<string>(StringComparer.Ordinal);var groups=new List<GodotTerrainTopologyGroupPacket>();
        foreach(var group in specification.Groups.OrderBy(g=>g?.Id,StringComparer.Ordinal))
        {
            if(group is null||group.Id is null||group.Id.Length is <1 or >80||!group.Id.All(c=>c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')||
                !names.Add(group.Id)||group.ChannelIds is not {Count:>=1 and <=10000})
                throw new ArgumentException("GODOT_TOPOLOGY_GROUP_INVALID");
            var cells=new HashSet<WorldTileCell>();
            foreach(string id in group.ChannelIds)
            {
                token.ThrowIfCancellationRequested();
                if(id is null||!owners.Add(id)||!channels.TryGetValue(id,out var channel))throw new ArgumentException("GODOT_TOPOLOGY_CHANNEL_COVERAGE");
                foreach(var cell in channel.Cells)
                {
                    if(cell.X is <-32768 or >32767||cell.Y is <-32768 or >32767)
                        throw new ArgumentException("GODOT_TOPOLOGY_SIGNED_COORDINATE_LIMIT");
                    cells.Add(cell);
                }
            }
            groups.Add(new(group.Id,group.ChannelIds.Order(StringComparer.Ordinal).ToArray(),cells.OrderBy(c=>c.Y).ThenBy(c=>c.X).ToArray()));
        }
        if(!owners.SetEquals(channels.Keys))throw new ArgumentException("GODOT_TOPOLOGY_CHANNEL_COVERAGE");
        return groups;
    }

    private static async Task WriteNativeCompilerAsync<T>(string directory,T packet,bool production,CancellationToken token)
    {
        string json=JsonSerializer.Serialize(packet,WorldJson.CreateOptions(true));
        await File.WriteAllTextAsync(Path.Combine(directory,"input.json"),json,token);
        await using var script=typeof(GodotStructureLayerExporter).Assembly.GetManifestResourceStream("WorldCreator.Godot.Native.WorldSceneCompiler.gd")!;
        using var reader=new StreamReader(script);
        string code=(await reader.ReadToEndAsync(token)).Replace("__PACKET_SHA256__",Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))),StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(directory,"compile.gd"),code,token);
        await using var movement=typeof(GodotStructureLayerExporter).Assembly.GetManifestResourceStream("WorldCreator.Godot.Native.WorldMovement.gd")!;
        using var movementReader=new StreamReader(movement);
        await File.WriteAllTextAsync(Path.Combine(directory,"movement.gd"),await movementReader.ReadToEndAsync(token),token);
        await using var component=typeof(GodotStructureLayerExporter).Assembly.GetManifestResourceStream("WorldCreator.Godot.Native.ComponentAnimation.gd")!;
        using var componentReader=new StreamReader(component);
        await File.WriteAllTextAsync(Path.Combine(directory,"component-animation.gd"),await componentReader.ReadToEndAsync(token),token);
        await File.WriteAllTextAsync(Path.Combine(directory,"project.godot"),"config_version=5\n[application]\nconfig/name=\"Framework native world compiler\"\n"+
            (production?"run/main_scene=\"res://World.tscn\"\n":"")+"[rendering]\nrenderer/rendering_method=\"gl_compatibility\"\n",token);
    }

    internal static async Task PublishNativeDirectoryAsync(string staging,string target,CancellationToken token)
    {
        var failures=new List<object>();
        int[] delays=[50,150,350];
        async Task Record(int attempt)=>await File.WriteAllTextAsync(Path.Combine(staging,"publication.json"),JsonSerializer.Serialize(new{
            CommitAttempt=attempt,EarlierFailures=failures},WorldJson.CreateOptions(true)),token);
        await Record(1);
        for(int attempt=1;;attempt++)
        {
            token.ThrowIfCancellationRequested();
            try { Directory.Move(staging,target);return; }
            catch(IOException error)when(OperatingSystem.IsWindows()&&(error.HResult&0xffff) is 5 or 32&&
                Directory.Exists(staging)&&!Directory.Exists(target)&&!File.Exists(target))
            {
                failures.Add(new{Attempt=attempt,Win32Code=error.HResult&0xffff});
                if(attempt>delays.Length)throw new IOException("GODOT_NATIVE_PUBLICATION_FAILED_AFTER_BOUNDED_RETRY:"+
                    JsonSerializer.Serialize(failures,WorldJson.CreateOptions()),error);
                // Record privately before waiting, so the next rename does not
                // immediately follow another change inside the staging directory.
                await Record(attempt+1);
                await Task.Delay(delays[attempt-1],token);
            }
        }
    }
}
