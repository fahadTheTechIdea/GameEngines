using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;
using static WorldCreator.Core.Metadata.MetadataInputValues;

namespace WorldCreator.Core.Metadata;

public enum MetadataRegionalInputSource { World, Recipe, Catalog }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalInputProtection(string Id,MetadataRegionalInputSource Input,string? RegionId,string Pointer,string Purpose,string SourceRuleId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalAnchorProtection(string Id,string RegionId,string Purpose,string SourceRuleId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalSessionControls(IReadOnlyList<MetadataRegionalInputProtection> Inputs,IReadOnlyList<MetadataRegionalAnchorProtection> Anchors);
public sealed record MetadataRegionalProtectedInput(MetadataRegionalInputProtection Definition,string ValueSha256);
public sealed record MetadataRegionalProtectedAnchor(MetadataRegionalAnchorProtection Definition,WorldPoint Position);
public sealed record MetadataRegionalProtectionPlan(IReadOnlyList<MetadataRegionalProtectedInput> Inputs,IReadOnlyList<MetadataRegionalProtectedAnchor> Anchors);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MetadataRegionalProtectionChange(string ExpectedEditId,IReadOnlyList<string> ReleaseIds,
    IReadOnlyList<MetadataRegionalInputProtection> Inputs,IReadOnlyList<MetadataRegionalAnchorProtection> Anchors);

public sealed partial class MetadataRegionalGenerationSession
{
    private static IReadOnlyList<string> ProtectedIds(MetadataRegionalProtectionPlan? protections)=>protections is null?[]:protections.Inputs.Select(p=>p.Definition.Id).Concat(protections.Anchors.Select(p=>p.Definition.Id)).Order(StringComparer.Ordinal).ToArray();
    private static MetadataRegionalProtectionPlan? ChangeProtections(MetadataRegionalProtectionChange? change,State before)
    {
        var current=before.Report.Protections;if(change is null)return current;
        if(change.ExpectedEditId!=before.Report.EditId)throw new ArgumentException("METADATA_REGIONAL_PROTECTION_CHANGE_STALE_BASE");
        if(change.ReleaseIds is null||change.Inputs is null||change.Anchors is null||
            change.ReleaseIds.Count>64||change.Inputs.Count+change.Anchors.Count>64||
            change.ReleaseIds.Count+change.Inputs.Count+change.Anchors.Count==0||
            change.ReleaseIds.Any(id=>!WorldEnvironmentQuery.Id(id))||change.ReleaseIds.Distinct(StringComparer.Ordinal).Count()!=change.ReleaseIds.Count)
            throw new ArgumentException("METADATA_REGIONAL_PROTECTION_CHANGE_INVALID_OR_LIMIT");
        var existing=ProtectedIds(current).ToHashSet(StringComparer.Ordinal);
        foreach(string id in change.ReleaseIds)
            if(!existing.Contains(id))throw new ArgumentException("METADATA_REGIONAL_PROTECTION_RELEASE_UNKNOWN:"+id);
        // A simultaneous replacement needs a fresh identity, even if released here.
        if(change.Inputs.Any(p=>p is not null&&existing.Contains(p.Id))||change.Anchors.Any(p=>p is not null&&existing.Contains(p.Id)))
            throw new ArgumentException("METADATA_REGIONAL_PROTECTION_ID_ALREADY_USED");
        var additions=change.Inputs.Count+change.Anchors.Count==0?null:CaptureProtections(before.World.Definition,new(change.Inputs,change.Anchors));
        var released=change.ReleaseIds.ToHashSet(StringComparer.Ordinal);
        var inputs=(current?.Inputs??[]).Where(p=>!released.Contains(p.Definition.Id)).Concat(additions?.Inputs??[]).OrderBy(p=>p.Definition.Id,StringComparer.Ordinal).ToArray();
        var anchors=(current?.Anchors??[]).Where(p=>!released.Contains(p.Definition.Id)).Concat(additions?.Anchors??[]).OrderBy(p=>p.Definition.Id,StringComparer.Ordinal).ToArray();
        if(inputs.Length+anchors.Length==0)return null;
        ValidateControls(new(inputs.Select(p=>p.Definition).ToArray(),anchors.Select(p=>p.Definition).ToArray()));
        return new(inputs,anchors);
    }
    private static void ValidateControls(MetadataRegionalSessionControls controls)
    {
        if(controls.Inputs is null||controls.Anchors is null||controls.Inputs.Count+controls.Anchors.Count is <1 or >64)
            throw new ArgumentException("METADATA_REGIONAL_CONTROLS_INVALID_OR_LIMIT");
        var ids=new HashSet<string>(StringComparer.Ordinal);var pointers=new HashSet<(MetadataRegionalInputSource,string?,string)>();var anchors=new HashSet<string>(StringComparer.Ordinal);
        bool Identity(string id,string purpose,string rule)=>WorldEnvironmentQuery.Id(id)&&ids.Add(id)&&WorldEnvironmentQuery.Text(purpose)&&WorldEnvironmentQuery.Text(rule);
        foreach(var item in controls.Inputs)
            if(item is null||!Identity(item.Id,item.Purpose,item.SourceRuleId)||!Enum.IsDefined(item.Input)||
                (item.Input==MetadataRegionalInputSource.World?item.RegionId is not null:!WorldEnvironmentQuery.Id(item.RegionId))||
                !pointers.Add((item.Input,item.RegionId,item.Pointer)))throw new ArgumentException("METADATA_REGIONAL_INPUT_PROTECTION_INVALID");
        foreach(var item in controls.Anchors)
            if(item is null||!Identity(item.Id,item.Purpose,item.SourceRuleId)||!WorldEnvironmentQuery.Id(item.RegionId)||!anchors.Add(item.RegionId))
                throw new ArgumentException("METADATA_REGIONAL_ANCHOR_PROTECTION_INVALID");
    }
    private static JsonElement? ProtectedValue(MetadataRegionalWorldDefinition definition,MetadataRegionalInputProtection item)
        =>definition.OwnershipPlan is {} derived?derived.Source is null?null:ProtectedValue(derived.Source,item):
            item.Input==MetadataRegionalInputSource.World?Resolve(Element(definition),item.Pointer):
            Unique(definition.Regions,item.RegionId) is {} region?
                Resolve(item.Input==MetadataRegionalInputSource.Recipe?Element(region.Recipe):Element(region.Catalog),item.Pointer):null;
    private static JsonElement? ProtectedValue(MetadataRegionalOwnershipRequest source,MetadataRegionalInputProtection item)
        =>item.Input==MetadataRegionalInputSource.World?Resolve(Element(source),item.Pointer):
            Unique(source.Regions,item.RegionId) is {} region?
                Resolve(item.Input==MetadataRegionalInputSource.Recipe?Element(region.Recipe):Element(region.Catalog),item.Pointer):null;
    private static T? Unique<T>(IReadOnlyList<T>? regions,string? id)where T:class
    {
        // Input checks precede generation. Malformed/missing/ambiguous rows must
        // produce an attributed conflict, not a null or Single() exception.
        var matches=regions?.Where(r=>r switch{MetadataGenerationRegion region=>region.Id==id,MetadataRegionalProgramRegion region=>region.Id==id,_=>false}).Take(2).ToArray();
        return matches is {Length:1}?matches[0]:null;
    }
    private static MetadataRegionalProtectionPlan? CaptureProtections(MetadataRegionalWorldDefinition definition,MetadataRegionalSessionControls? controls)
    {
        if(controls is null)return null;ValidateControls(controls);
        var inputs=controls.Inputs.OrderBy(p=>p.Id,StringComparer.Ordinal).Select(item=>new MetadataRegionalProtectedInput(item,
            MetadataExecutionCache.Hash(ProtectedValue(definition,item)??throw new ArgumentException("METADATA_REGIONAL_PROTECTED_INPUT_MISSING:"+item.Id)))).ToArray();
        var anchors=controls.Anchors.OrderBy(p=>p.Id,StringComparer.Ordinal).Select(item=>{
            if(definition.OwnershipPlan is null)throw new ArgumentException("METADATA_REGIONAL_ANCHOR_PROTECTION_DERIVED_SOURCE_REQUIRED:"+item.Id);
            var region=definition.Regions.SingleOrDefault(r=>r.Id==item.RegionId);
            if(region?.Recipe.Origin is not {Count:2} origin)throw new ArgumentException("METADATA_REGIONAL_PROTECTED_REGION_MISSING:"+item.Id+":"+item.RegionId);
            return new MetadataRegionalProtectedAnchor(item,new(origin[0],origin[1]));
        }).ToArray();
        return new(inputs,anchors);
    }
    private static MetadataRegionalOwnershipRequest BindAnchors(MetadataRegionalOwnershipRequest source,MetadataRegionalProtectionPlan? plan)
    {
        if(plan is null||plan.Anchors.Count==0)return source;
        if(source is null||source.Regions is null||source.Regions.Any(r=>r is null))throw new ArgumentException("METADATA_REGIONAL_OWNERSHIP_REQUEST_INVALID");
        var regions=source.Regions.ToArray();
        foreach(var locked in plan.Anchors)
        {
            var item=locked.Definition;int index=Array.FindIndex(regions,r=>r.Id==item.RegionId);
            if(index<0)throw new ArgumentException("METADATA_REGIONAL_PROTECTED_REGION_MISSING:"+item.Id+":"+item.RegionId);
            var region=regions[index];
            if(region.PinnedAnchor is {} pin&&pin.Position!=locked.Position)
                throw new ArgumentException("METADATA_REGIONAL_ANCHOR_PROTECTION_CONFLICT:"+item.Id+":"+item.RegionId);
            regions[index]=region with{PinnedAnchor=region.PinnedAnchor??new(item.Purpose,item.SourceRuleId,locked.Position)};
        }
        return source with{Regions=regions};
    }
    private static void CheckProtections(MetadataRegionalWorldDefinition definition,MetadataRegionalProtectionPlan? protections)
    {
        if(protections is null)return;
        CheckInputs(item=>ProtectedValue(definition,item),protections);
        foreach(var item in protections.Anchors)
        {
            var origin=Unique(definition.Regions,item.Definition.RegionId)?.Recipe?.Origin;
            if(origin is not {Count:2}||new WorldPoint(origin[0],origin[1])!=item.Position)
                throw new ArgumentException("METADATA_REGIONAL_ANCHOR_PROTECTION_CONFLICT:"+item.Definition.Id+":"+item.Definition.RegionId);
        }
    }
    private static void CheckInputs(Func<MetadataRegionalInputProtection,JsonElement?> resolve,MetadataRegionalProtectionPlan? protections)
    {
        foreach(var item in protections?.Inputs??[])
        {
            var actual=resolve(item.Definition);
            if(actual is null||MetadataExecutionCache.Hash(actual.Value)!=item.ValueSha256)
                throw new ArgumentException("METADATA_REGIONAL_PROTECTED_INPUT_CONFLICT:"+item.Definition.Id+":"+item.Definition.Pointer);
        }
    }
}
