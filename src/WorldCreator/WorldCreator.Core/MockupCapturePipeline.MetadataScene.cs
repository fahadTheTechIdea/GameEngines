using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Capture;

public enum MetadataSceneSurfaceKind { Domain, Patch, Exclusion }
public sealed record MetadataSceneSurfaceBinding(MetadataSceneSurfaceKind Kind,string TargetId,string FeatureId);
public sealed record MetadataScenePathBinding(string ConnectionId,string FeatureId,double? FullWidthPixels=null,IReadOnlyList<double>? SegmentWidthsPixels=null);
public sealed record MetadataSceneView(string ViewId,MockupCaptureDocument Capture,string CaptureSha256,
    IReadOnlyList<MetadataSceneSurfaceBinding> Surfaces,IReadOnlyList<MetadataScenePathBinding> Paths);
public sealed record MetadataSceneRequest(int Version,string GroundCompilationSha256,IReadOnlyList<MetadataSceneView> Views,
    double ToleranceMeters,long MaximumWork=2000000);
public sealed record MetadataSceneSurfaceTrace(string ViewId,MetadataSceneSurfaceBinding Binding,WorldPolygon Boundary,double UncertaintyMeters);
public sealed record MetadataScenePathTrace(string ViewId,MetadataScenePathBinding Binding,WorldPolyline Path,IReadOnlyList<double> WidthsMeters,double UncertaintyMeters);
public sealed record MetadataSceneCompilation(int Version,string Status,MetadataGroundCompilation Ground,MetadataSceneRequest Request,
    WorldMetadataRecipe Recipe,WorldMetadataDocument World,IReadOnlyList<MetadataSceneSurfaceTrace> Surfaces,
    IReadOnlyList<MetadataScenePathTrace> Paths,IReadOnlyList<string> RemainingGates);

public static partial class MockupCapturePipeline
{
    internal static async Task WriteMetadataSceneOverlaysAsync(MetadataSceneCompilation compilation,string trustedImageRoot,string directory,CancellationToken token)
    {
        long bytes=0;
        foreach(var view in compilation.Request.Views)
        {
            var png=await ReadSourceAsync(view.Capture,trustedImageRoot,token);
            if((bytes+=png.Length)>268435456)throw new ArgumentException("METADATA_SCENE_SOURCE_BYTE_BUDGET");
            var svg=XDocument.Parse(Overlay(view.Capture,png,token));XNamespace ns="http://www.w3.org/2000/svg";
            svg.Root!.Element(ns+"title")!.Value="Scene trace: "+view.ViewId+"; exact calibrated terrain and prescribed paths. Draft, not art approval.";
            foreach(var binding in view.Surfaces)
                svg.Root.Elements(ns+"g").Single(g=>(string?)g.Attribute("data-feature")==binding.FeatureId).SetAttributeValue("data-world-target",binding.Kind+":"+binding.TargetId);
            foreach(var binding in view.Paths)
                svg.Root.Elements(ns+"g").Single(g=>(string?)g.Attribute("data-feature")==binding.FeatureId).SetAttributeValue("data-world-target",binding.ConnectionId);
            await File.WriteAllTextAsync(Path.Combine(directory,"scene-overlay-"+view.ViewId+".svg"),svg.ToString(),token);
        }
    }

    public static string GroundCompilationDigest(MetadataGroundCompilation compilation)=>Convert.ToHexStringLower(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(compilation,WorldJson.CreateOptions())));

    public static async Task<MetadataSceneCompilation> CompileMetadataSceneAsync(MetadataGroundCompilation ground,MetadataSceneRequest request,
        string trustedImageRoot,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(ground);ArgumentNullException.ThrowIfNull(request);
        T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        ground=Copy(ground);request=Copy(request);generator??=new();
        if(request.Version!=1||request.GroundCompilationSha256!=GroundCompilationDigest(ground)||request.Views is null||request.Views.Count is <1 or >128||
            !double.IsFinite(request.ToleranceMeters)||request.ToleranceMeters is <=0 or >100||request.MaximumWork is <1 or >20000000)
            throw new ArgumentException("METADATA_SCENE_REQUEST_INVALID_OR_STALE");
        await ValidateMetadataGroundCompilationAsync(ground,trustedImageRoot,generator,token);
        if(ground.Recipe.NetworkRules is not null||ground.World.NetworkPlan is not null||ground.Recipe.PopulationSource is not null||ground.Recipe.Placement?.SourceWorld is not null)
            throw new ArgumentException("METADATA_SCENE_NETWORK_OR_PRESERVATION_UNSUPPORTED");
        var source=ground.Recipe.Surface??throw new ArgumentException("METADATA_SCENE_SURFACE_REQUIRED");
        var rules=ground.Recipe.RouteRules??throw new ArgumentException("METADATA_SCENE_ROUTE_RULES_REQUIRED");
        if(rules.Any(r=>r.Reuse is not null||r.Interfaces is not null||r.Alignments is not null))throw new ArgumentException("METADATA_SCENE_ROUTE_MODE_UNSUPPORTED");
        var sourceLinks=ground.World.Connections.Where(c=>rules.Any(r=>r.ConnectionKind==c.Kind)).ToDictionary(c=>c.Id,StringComparer.Ordinal);
        string Key(MetadataSceneSurfaceKind kind,string id)=>kind+":"+id;
        var expected=new HashSet<string>(StringComparer.Ordinal){Key(MetadataSceneSurfaceKind.Domain,source.Id)};
        foreach(var patch in source.Patches)expected.Add(Key(MetadataSceneSurfaceKind.Patch,patch.Id));
        foreach(var exclusion in source.Exclusions)expected.Add(Key(MetadataSceneSurfaceKind.Exclusion,exclusion.Id));
        var surfaces=new Dictionary<string,MetadataSceneSurfaceTrace>(StringComparer.Ordinal);var paths=new Dictionary<string,MetadataScenePathTrace>(StringComparer.Ordinal);
        var viewIds=new HashSet<string>(StringComparer.Ordinal);long work=0,bytes=0,observations=0;
        void Charge(long amount=1){token.ThrowIfCancellationRequested();if(amount<0||(work+=amount)>request.MaximumWork)throw new ArgumentException("METADATA_SCENE_WORK_BUDGET");}
        foreach(var view in request.Views.OrderBy(v=>v?.ViewId,StringComparer.Ordinal))
        {
            if(view is null||view.ViewId is null||!viewIds.Add(view.ViewId)||view.Capture is null||view.CaptureSha256!=CaptureDigest(view.Capture)||
                view.Surfaces is null||view.Paths is null||view.Surfaces.Count+view.Paths.Count is <1 or >2000||(observations+=view.Surfaces.Count+view.Paths.Count)>32000)
                throw new ArgumentException("METADATA_SCENE_VIEW_INVALID_OR_STALE");
            var original=ground.Request.Views.SingleOrDefault(v=>v.Id==view.ViewId)??throw new ArgumentException("METADATA_SCENE_GROUND_VIEW_REQUIRED");
            bool Equal<T>(T a,T b)=>JsonSerializer.Serialize(a,WorldJson.CreateOptions())==JsonSerializer.Serialize(b,WorldJson.CreateOptions());
            if(!Equal(original.GroundCapture.Source,view.Capture.Source)||!Equal(original.GroundCapture.Calibration,view.Capture.Calibration))
                throw new ArgumentException("METADATA_SCENE_REGISTRATION_MISMATCH");
            if((bytes+=(await ReadSourceAsync(view.Capture,trustedImageRoot,token)).Length)>268435456)throw new ArgumentException("METADATA_SCENE_SOURCE_BYTE_BUDGET");
            var capture=view.Capture;
            if(capture.Coverage!=CaptureCoverage.Complete||capture.Unresolved.Count!=0||capture.CompilationScope is not null)
                throw new ArgumentException("METADATA_SCENE_CAPTURE_INCOMPLETE");
            var report=MockupCaptureValidator.Validate(capture,token);
            if(report.Crossings.Any(c=>c.JunctionId is null||c.Kind!=CaptureJunctionKind.AtGrade)||capture.Junctions.Any(j=>j.Review!=CaptureReview.Accepted||j.Kind!=CaptureJunctionKind.AtGrade))
                throw new ArgumentException("METADATA_SCENE_JUNCTION_REVIEW_REQUIRED");
            var c=capture.Calibration??throw new ArgumentException("CAPTURE_CALIBRATION_REQUIRED");
            double determinant=c.A*c.E-c.B*c.D,trace=c.A*c.A+c.B*c.B+c.D*c.D+c.E*c.E;
            double scale=Math.Sqrt((trace+Math.Sqrt(Math.Max(0,trace*trace-4*determinant*determinant)))/2);
            var features=capture.Features.ToDictionary(f=>f.Id,StringComparer.Ordinal);var used=new HashSet<string>(StringComparer.Ordinal);
            (CaptureFeature Feature,double Uncertainty) Feature(string id,CaptureShape shape)
            {
                Charge();
                if(id is null||!used.Add(id)||!features.TryGetValue(id,out var f)||f.Shape!=shape||f.Review!=CaptureReview.Accepted||
                    f.Evidence is not(CaptureEvidence.Observed or CaptureEvidence.Authored)||f.Occluded||f.Semantic.EndsWith(".roof",StringComparison.Ordinal))
                    throw new ArgumentException("METADATA_SCENE_FEATURE_UNRESOLVED");
                double uncertainty=f.UncertaintyPixels*scale+c.ToleranceMeters;
                if(!double.IsFinite(uncertainty)||uncertainty>request.ToleranceMeters)throw new ArgumentException("METADATA_SCENE_UNCERTAINTY_EXCEEDS_TOLERANCE");
                Charge(f.Points.Count*2L);return(f,uncertainty);
            }
            foreach(var binding in view.Surfaces)
            {
                if(binding is null||!Enum.IsDefined(binding.Kind)||binding.TargetId is null||!expected.Contains(Key(binding.Kind,binding.TargetId)))
                    throw new ArgumentException("METADATA_SCENE_SURFACE_TARGET_INVALID");
                var (feature,uncertainty)=Feature(binding.FeatureId,CaptureShape.Polygon);
                var polygon=new WorldPolygon(feature.Points.Select(p=>MockupCaptureValidator.Transform(c,p)).ToArray());Charge(8L*polygon.Vertices.Count*polygon.Vertices.Count);polygon.Validate();
                if(!surfaces.TryAdd(Key(binding.Kind,binding.TargetId),new(view.ViewId,binding,polygon,uncertainty)))throw new ArgumentException("METADATA_SCENE_SURFACE_TARGET_DUPLICATE");
            }
            var localPaths=new Dictionary<string,MetadataScenePathTrace>(StringComparer.Ordinal);
            foreach(var binding in view.Paths)
            {
                if(binding is null||binding.ConnectionId is null||!sourceLinks.TryGetValue(binding.ConnectionId,out var link))throw new ArgumentException("METADATA_SCENE_PATH_TARGET_INVALID");
                var (feature,uncertainty)=Feature(binding.FeatureId,CaptureShape.Polyline);
                double[] pixelWidths;
                if(feature.Road is {} road)
                {
                    if(road.Level!=0||road.WidthKind!=CaptureWidthKind.FullCorridor||binding.FullWidthPixels is not null||binding.SegmentWidthsPixels is not null)
                        throw new ArgumentException("METADATA_SCENE_PATH_WIDTH_OR_LEVEL_INVALID");
                    pixelWidths=road.SegmentWidthsPixels?.ToArray()??Enumerable.Repeat(road.WidthPixels,feature.Points.Count-1).ToArray();
                }
                else
                {
                    if((binding.FullWidthPixels is null)==(binding.SegmentWidthsPixels is null))throw new ArgumentException("METADATA_SCENE_PATH_WIDTH_REQUIRED");
                    pixelWidths=binding.SegmentWidthsPixels?.ToArray()??Enumerable.Repeat(binding.FullWidthPixels!.Value,feature.Points.Count-1).ToArray();
                }
                if(pixelWidths.Length!=feature.Points.Count-1||pixelWidths.Any(w=>!double.IsFinite(w)||w<=0||w>Math.Max(capture.Source.Width,capture.Source.Height)))
                    throw new ArgumentException("METADATA_SCENE_PATH_WIDTH_OR_LEVEL_INVALID");
                var path=new WorldPolyline(feature.Points.Select(p=>MockupCaptureValidator.Transform(c,p)).ToArray());path.Validate();
                var rule=rules.Single(r=>r.ConnectionKind==link.Kind);var widths=new List<double>();
                for(int i=1;i<feature.Points.Count;i++)
                {
                    var a=feature.Points[i-1];var b=feature.Points[i];double length=Math.Sqrt((b.X-a.X)*(b.X-a.X)+(b.Y-a.Y)*(b.Y-a.Y));
                    double projected=pixelWidths[i-1]*Math.Abs(determinant)*length/path.Points[i-1].DistanceTo(path.Points[i]);widths.Add(projected);
                    if(Math.Abs(projected-2*rule.Profile.Radius)+uncertainty>request.ToleranceMeters)throw new ArgumentException("METADATA_SCENE_PATH_WIDTH_MISMATCH:"+link.Id);
                }
                var result=new MetadataScenePathTrace(view.ViewId,binding,path,widths,uncertainty);
                if(!paths.TryAdd(link.Id,result))throw new ArgumentException("METADATA_SCENE_PATH_TARGET_DUPLICATE");
                localPaths.Add(feature.Id,result);
            }
            if(!used.SetEquals(features.Keys))throw new ArgumentException("METADATA_SCENE_UNASSIGNED_FEATURES");
            foreach(var crossing in report.Crossings)
            {
                if(!localPaths.TryGetValue(crossing.FirstRoadId,out var first)||!localPaths.TryGetValue(crossing.SecondRoadId,out var second))
                    throw new ArgumentException("METADATA_SCENE_JUNCTION_PATH_BINDING_REQUIRED");
                var one=sourceLinks[first.Binding.ConnectionId];var two=sourceLinks[second.Binding.ConnectionId];
                var common=new[]{one.FromId,one.ToId}.Intersect([two.FromId,two.ToId],StringComparer.Ordinal);
                var point=MockupCaptureValidator.Transform(c,crossing.Position);
                WorldPoint Endpoint(MetadataScenePathTrace path,WorldMetadataConnection link,string id)=>path.Path.Points[id==link.FromId?0:^1];
                if(!common.Any(id=>Endpoint(first,one,id).DistanceTo(point)<1e-7&&Endpoint(second,two,id).DistanceTo(point)<1e-7))
                    throw new ArgumentException("METADATA_SCENE_JUNCTION_GRAPH_MISMATCH");
            }
        }
        if(!surfaces.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected)||!paths.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(sourceLinks.Keys))
            throw new ArgumentException("METADATA_SCENE_COVERAGE_MISMATCH");
        // Check all compiled paths, including crossings between different images and
        // non-road passage semantics. A geometric crossing never invents graph edges.
        var allPaths=paths.Values.OrderBy(p=>p.Binding.ConnectionId,StringComparer.Ordinal).ToArray();
        for(int a=0;a<allPaths.Length;a++)for(int b=a;b<allPaths.Length;b++)
        {
            var first=allPaths[a];var second=allPaths[b];
            for(int i=1;i<first.Path.Points.Count;i++)for(int j=1;j<second.Path.Points.Count;j++)
            {
                if(a==b&&j<=i+1)continue;Charge();
                var hit=PlanarIntersections.Intersect(first.Path.Points[i-1],first.Path.Points[i],second.Path.Points[j-1],second.Path.Points[j]);
                if(hit.Kind==IntersectionKind.None)continue;
                if(hit.Kind==IntersectionKind.CollinearOverlap||a==b)throw new ArgumentException("METADATA_SCENE_PATH_OVERLAP_OR_SELF_CROSSING");
                var one=sourceLinks[first.Binding.ConnectionId];var two=sourceLinks[second.Binding.ConnectionId];
                bool Matches(string id)
                {
                    var x=first.Path.Points[id==one.FromId?0:^1];var y=second.Path.Points[id==two.FromId?0:^1];
                    return x.DistanceTo(hit.Point!.Value)<1e-7&&y.DistanceTo(hit.Point.Value)<1e-7;
                }
                if(!new[]{one.FromId,one.ToId}.Intersect([two.FromId,two.ToId],StringComparer.Ordinal).Any(Matches))
                    throw new ArgumentException("METADATA_SCENE_JUNCTION_GRAPH_MISMATCH");
            }
        }
        WorldPolygon Boundary(MetadataSceneSurfaceKind kind,string id)=>surfaces[Key(kind,id)].Boundary;
        var surface=source with{Boundary=Boundary(MetadataSceneSurfaceKind.Domain,source.Id),
            Patches=source.Patches.Select(p=>p with{Boundary=Boundary(MetadataSceneSurfaceKind.Patch,p.Id)}).ToArray(),
            Exclusions=source.Exclusions.Select(e=>e with{Boundary=Boundary(MetadataSceneSurfaceKind.Exclusion,e.Id)}).ToArray()};
        var updated=rules.Select(r=>r with{Alignments=sourceLinks.Values.Where(c=>c.Kind==r.ConnectionKind).OrderBy(c=>c.Id,StringComparer.Ordinal)
            .Select(c=>new MetadataRouteAlignment(c.Id,paths[c.Id].Path,"capture:"+request.Views.Single(v=>v.ViewId==paths[c.Id].ViewId).CaptureSha256)).ToArray()}).ToArray();
        request=request with{Views=request.Views.OrderBy(v=>v.ViewId,StringComparer.Ordinal).Select(v=>v with
            {Surfaces=v.Surfaces.OrderBy(b=>b.Kind).ThenBy(b=>b.TargetId,StringComparer.Ordinal).ToArray(),Paths=v.Paths.OrderBy(b=>b.ConnectionId,StringComparer.Ordinal).ToArray()}).ToArray()};
        var recipe=ground.Recipe with{Surface=surface,RouteRules=updated};var world=generator.Generate(recipe,ground.Handoff.Catalog,token);
        var previous=ground.World.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        if(!world.Nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal).SetEquals(previous.Keys))
            throw new ArgumentException("METADATA_SCENE_GENERATED_CONTENT_MISMATCH");
        foreach(var node in world.Nodes)
            if(JsonSerializer.Serialize(node,WorldJson.CreateOptions())!=JsonSerializer.Serialize(previous[node.Id],WorldJson.CreateOptions()))
                throw new ArgumentException("METADATA_SCENE_GENERATED_MEMBER_CHANGED:"+node.Id);
        return new(1,"SceneLayoutDraft",ground,request,recipe,world,surfaces.Values.OrderBy(s=>s.Binding.Kind).ThenBy(s=>s.Binding.TargetId,StringComparer.Ordinal).ToArray(),
            paths.Values.OrderBy(p=>p.Binding.ConnectionId,StringComparer.Ordinal).ToArray(),
            ["Scene annotations are author/reviewer evidence; actual vision and image completeness remain unverified.",
             "Terrain kinds, elevations and policies remain source-authored; captured polygons do not infer physical height.",
             "Only explicit surface-level route links are supported here; network/interface/reuse trace modes reject.",
             "Exact artifact approvals, complete visual review and production assets remain required.",
             "Native Godot TileMapLayer, collision, navigation, animation and runtime acceptance remain unfinished."]);
    }

    public static async Task ValidateMetadataSceneCompilationAsync(MetadataSceneCompilation compilation,string trustedImageRoot,
        WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(compilation);token.ThrowIfCancellationRequested();
        var expected=await CompileMetadataSceneAsync(compilation.Ground,compilation.Request,trustedImageRoot,generator,token);
        if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(compilation,WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_SCENE_COMPILATION_REPLAY_MISMATCH");
    }
}
