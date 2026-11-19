using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Planning;
using WorldCreator.Core.Capture;

namespace WorldCreator.Core;

public enum MetadataArtRole { Group, Structure, Ground, Object, Logical }
public sealed record MetadataArtBinding(string ArchetypeId,MetadataArtRole Role);
public sealed record MetadataArtScope(string Id,string? ParentId,string Purpose,IReadOnlyList<string> MemberRootIds);
public sealed record MetadataArtSpecification(int Version,string Id,string WorldSha256,
    IReadOnlyList<MetadataArtBinding> Bindings,IReadOnlyList<MetadataArtScope> Scopes);
public sealed record MetadataArtMember(string NodeId,MetadataArtRole Role,string? ScopeId,string? ReferenceJobId);
public sealed record MetadataArtJob(string Id,string Kind,string Purpose,IReadOnlyList<string> NodeIds,IReadOnlyList<string> DependsOn,
    string? ArchetypeId=null,string? StructureKey=null);
public sealed record MetadataArtHandoff(int Version,string Status,MetadataArtSpecification Specification,
    WorldMetadataDocument World,WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog,
    IReadOnlyList<MetadataArtMember> Members,IReadOnlyList<MetadataArtJob> Jobs,IReadOnlyList<string> RequiredGates);

public sealed partial class StructureFirstPipeline
{
    public async Task<MetadataSceneCompilation> ExportMetadataSceneAsync(MetadataGroundCompilation ground,MetadataSceneRequest request,
        string trustedImageRoot,string newOutputDirectory,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        generator??=new();
        var result=await MockupCapturePipeline.CompileMetadataSceneAsync(ground,request,trustedImageRoot,generator,token);
        await generator.PublishAsync(result.Recipe,result.Ground.Handoff.Catalog,result.World,newOutputDirectory,null,token,
            result.Ground,trustedImageRoot,result);
        return result;
    }

    public async Task<MetadataGroundCompilation> ExportMetadataGroundAsync(MetadataArtHandoff handoff,
        MetadataGroundRequest request, string trustedImageRoot, string newOutputDirectory,
        WorldMetadataGenerator? generator = null, CancellationToken token = default)
    {
        generator ??= new();
        var result = await MockupCapturePipeline.CompileMetadataGroundAsync(handoff, request, trustedImageRoot, generator, token);
        await generator.PublishAsync(result.Recipe,result.Handoff.Catalog,result.World,newOutputDirectory,null,token,result,trustedImageRoot);
        return result;
    }

    public async Task<MetadataCaptureReconciliation> ExportMetadataCaptureAsync(MetadataArtHandoff handoff,
        MockupCaptureDocument capture, MetadataCaptureSpecification specification, string trustedImageRoot,
        string outputPath, WorldMetadataGenerator? generator = null, CancellationToken token = default)
    {
        var report = await MockupCapturePipeline.ReconcileMetadataAsync(handoff, capture, specification, trustedImageRoot, generator, token);
        await SaveNewJsonAsync(report, outputPath, token);
        return report;
    }

    public static string WorldDigest(WorldMetadataDocument world)=>Convert.ToHexStringLower(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(world,WorldJson.CreateOptions())));

    /// <summary>Reconciles the complete reusable metadata program into reference and
    /// scope jobs. This is inventory planning; canonical GameArts owns execution and approval.</summary>
    public MetadataArtHandoff PlanMetadata(WorldMetadataDocument world,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,
        MetadataArtSpecification specification,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(recipe);ArgumentNullException.ThrowIfNull(catalog);
        T Snapshot<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        world=Snapshot(world);recipe=Snapshot(recipe);catalog=Snapshot(catalog);specification=Snapshot(specification);
        if(specification is null||specification.Version!=1||!SettlementCatalogRegistry.ValidId(specification.Id)||
            specification.WorldSha256!=WorldDigest(world)||specification.Bindings is null||specification.Bindings.Count is <1 or >10000||
            specification.Scopes is null||specification.Scopes.Count is <1 or >512)
            throw new ArgumentException("METADATA_ART_SPECIFICATION_INVALID_OR_STALE");
        var replay=(generator??new()).Generate(recipe,catalog,token);
        if(WorldDigest(replay)!=specification.WorldSha256)throw new ArgumentException("METADATA_ART_SOURCE_REPLAY_MISMATCH");
        var nodes=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var roles=new Dictionary<string,MetadataArtRole>(StringComparer.Ordinal);
        foreach(var binding in specification.Bindings)
            if(binding is null||!SettlementCatalogRegistry.ValidId(binding.ArchetypeId)||!Enum.IsDefined(binding.Role)||!roles.TryAdd(binding.ArchetypeId,binding.Role))
                throw new ArgumentException("METADATA_ART_BINDING_INVALID");
        if(!roles.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(nodes.Values.Select(n=>n.ArchetypeId)))
            throw new ArgumentException("METADATA_ART_BINDING_COVERAGE");
        var children=nodes.Values.ToLookup(n=>n.ParentId??"",StringComparer.Ordinal);
        var scopes=new Dictionary<string,MetadataArtScope>(StringComparer.Ordinal);
        foreach(var scope in specification.Scopes)
        {
            if(scope is null||!SettlementCatalogRegistry.ValidId(scope.Id)||!scopes.TryAdd(scope.Id,scope)||
                string.IsNullOrWhiteSpace(scope.Purpose)||scope.Purpose.Length>2048||scope.MemberRootIds is null||scope.MemberRootIds.Count>10000||
                scope.MemberRootIds.Distinct(StringComparer.Ordinal).Count()!=scope.MemberRootIds.Count||scope.MemberRootIds.Any(id=>id is null||!nodes.ContainsKey(id)))
                throw new ArgumentException("METADATA_ART_SCOPE_INVALID");
        }
        if(scopes.Values.Count(s=>s.ParentId is null)!=1)throw new ArgumentException("METADATA_ART_ROOT_REQUIRED");
        var scopeChildren=scopes.Values.ToLookup(s=>s.ParentId??"",StringComparer.Ordinal);
        var depth=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var scope in scopes.Values)
        {
            token.ThrowIfCancellationRequested();var seen=new HashSet<string>(StringComparer.Ordinal);var cursor=scope;
            while(true)
            {
                if(!seen.Add(cursor.Id))throw new ArgumentException("METADATA_ART_SCOPE_CYCLE");
                if(cursor.ParentId is null)break;
                if(!scopes.TryGetValue(cursor.ParentId,out cursor!))throw new ArgumentException("METADATA_ART_PARENT_REQUIRED");
            }
            depth[scope.Id]=seen.Count-1;
            if(scopeChildren[scope.Id].Any()?scope.MemberRootIds.Count!=0:scope.MemberRootIds.Count==0)
                throw new ArgumentException("METADATA_ART_SCOPE_ROOTS_REQUIRED");
        }
        var owners=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var leaf in scopes.Values.Where(s=>!scopeChildren[s.Id].Any()).OrderBy(s=>s.Id,StringComparer.Ordinal))
        foreach(var root in leaf.MemberRootIds.Order(StringComparer.Ordinal))
        {
            var pending=new Stack<string>();pending.Push(root);
            while(pending.TryPop(out var id))
            {
                token.ThrowIfCancellationRequested();
                if(!owners.TryAdd(id,leaf.Id))throw new ArgumentException("METADATA_ART_SCOPE_OVERLAP:"+id);
                foreach(var child in children[id])pending.Push(child.Id);
            }
        }
        var members=new List<MetadataArtMember>();
        var physical=(world.PlacementPlan?.Footprints??[]).Concat(world.PlacementPlan?.NestedStages.SelectMany(s=>s.Footprints)??[])
            .ToDictionary(f=>f.NodeId,StringComparer.Ordinal);
        string rootScope=scopes.Values.Single(s=>s.ParentId is null).Id;
        foreach(var node in nodes.Values.OrderBy(n=>n.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();var role=roles[node.ArchetypeId];
            if(role==MetadataArtRole.Group&&!children[node.Id].Any())throw new ArgumentException("METADATA_ART_GROUP_CONTENT_REQUIRED:"+node.Id);
            if(role==MetadataArtRole.Logical&&(physical.ContainsKey(node.Id)||node.StructureKey is not null||world.FootprintDefinitions?.ContainsKey(node.ArchetypeId)==true))
                throw new ArgumentException("METADATA_ART_LOGICAL_PHYSICAL_CONFLICT:"+node.Id);
            if(role is not (MetadataArtRole.Group or MetadataArtRole.Logical)&&!owners.ContainsKey(node.Id))throw new ArgumentException("METADATA_ART_MEMBER_SCOPE_REQUIRED:"+node.Id);
            // Catalog and registered variants own their canonical source shape.
            // For demand-authored shapes, differing exact local geometry needs
            // a distinct reference even when the archetype name is shared.
            var custom=world.FootprintDefinitions?.ContainsKey(node.ArchetypeId)!=true&&node.StructureKey is null?
                physical.GetValueOrDefault(node.Id):null;
            string? job=role is MetadataArtRole.Group or MetadataArtRole.Logical?null:"reference-"+Convert.ToHexStringLower(SHA256.HashData(
                JsonSerializer.SerializeToUtf8Bytes(new{node.ArchetypeId,node.StructureKey,custom?.Polygon,custom?.Reservation},WorldJson.CreateOptions())));
            members.Add(new(node.Id,role,owners.GetValueOrDefault(node.Id,rootScope),job));
        }
        var jobs=new List<MetadataArtJob>();
        foreach(var group in members.Where(m=>m.ReferenceJobId is not null).GroupBy(m=>m.ReferenceJobId!).OrderBy(g=>g.Key,StringComparer.Ordinal))
        {
            var node=nodes[group.First().NodeId];var type=catalog.Archetypes.Single(a=>a.Id==node.ArchetypeId);
            jobs.Add(new(group.Key,"ReferenceConcept",type.Purpose,group.Select(m=>m.NodeId).ToArray(),[],node.ArchetypeId,node.StructureKey));
        }
        bool Under(string leaf,string ancestor)
        {for(var s=scopes[leaf];;s=scopes[s.ParentId!]){if(s.Id==ancestor)return true;if(s.ParentId is null)return false;}}
        foreach(var scope in scopes.Values.OrderByDescending(s=>depth[s.Id]).ThenBy(s=>s.Id,StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();var included=members.Where(m=>m.ScopeId is {} owner&&Under(owner,scope.Id)).ToArray();
            if(!included.Any(m=>m.Role is not (MetadataArtRole.Group or MetadataArtRole.Logical)))throw new ArgumentException("METADATA_ART_SCOPE_EMPTY:"+scope.Id);
            var descendants=scopeChildren[scope.Id].OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray();
            var dependencies=descendants.Length>0?descendants.Select(s=>"scope:"+s.Id).ToArray():
                included.Where(m=>m.ReferenceJobId is not null).Select(m=>m.ReferenceJobId!).Distinct().Order(StringComparer.Ordinal).ToArray();
            jobs.Add(new("scope:"+scope.Id,descendants.Length>0?"OverviewMockup":"DetailMockup",scope.Purpose,included.Select(m=>m.NodeId).ToArray(),dependencies));
        }
        specification=specification with{Bindings=specification.Bindings.OrderBy(b=>b.ArchetypeId,StringComparer.Ordinal).ToArray(),
            Scopes=specification.Scopes.OrderBy(s=>s.Id,StringComparer.Ordinal).Select(s=>s with{MemberRootIds=s.MemberRootIds.Order(StringComparer.Ordinal).ToArray()}).ToArray()};
        return new(1,"DraftInventoryOnly",specification,world,recipe,catalog,members,jobs,
            ["Core capability/design gate for the requested program", "Canonical owner-confirmed GameArts project settings",
             "Canonical approved storyboard when applicable", "Per-reference and per-mockup review and approval",
             "Complete visual instance reconciliation before capture", "Separate production art and native runtime acceptance"]);
    }

    public async Task<MetadataArtHandoff> ExportMetadataAsync(WorldMetadataDocument world,WorldMetadataRecipe recipe,WorldMetadataCatalog catalog,
        MetadataArtSpecification specification,string newFile,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        var result=PlanMetadata(world,recipe,catalog,specification,generator,token);
        await SaveNewJsonAsync(result,newFile,token);return result;
    }

    public void ValidateMetadataHandoff(MetadataArtHandoff handoff,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(handoff);token.ThrowIfCancellationRequested();
        var replay=PlanMetadata(handoff.World,handoff.Recipe,handoff.Catalog,handoff.Specification,generator,token);
        if(JsonSerializer.Serialize(replay,WorldJson.CreateOptions())!=JsonSerializer.Serialize(handoff,WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_ART_HANDOFF_REPLAY_MISMATCH");
    }
}
