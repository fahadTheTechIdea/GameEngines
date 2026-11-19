using System.Text.Json;
using System.Globalization;
using System.Xml.Linq;
using System.Security.Cryptography;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Capture;

public sealed record MetadataGroundBinding(string NodeId, string FeatureId, CapturePixel OriginPixel, double RotationDegrees);
public sealed record MetadataGroundView(string Id, MockupCaptureDocument IdentityCapture,
    MetadataCaptureSpecification IdentitySpecification, MockupCaptureDocument GroundCapture,
    string GroundCaptureSha256, IReadOnlyList<MetadataGroundBinding> Bindings);
public sealed record MetadataGroundOwner(string NodeId,string ViewId);
public sealed record MetadataGroundRequest(int Version,IReadOnlyList<MetadataGroundView> Views,
    IReadOnlyList<MetadataGroundOwner> Owners,double ToleranceMeters,long MaximumBoundaryWork=2000000);
public sealed record MetadataGroundObservation(string ViewId,string FeatureId,WorldPoint Origin,double RotationDegrees,
    WorldPolygon Observed,double PairedBoundaryDeviationMeters,double UncertaintyMeters);
public sealed record MetadataGroundFootprint(string NodeId,string OwnerViewId,WorldPoint Origin,double RotationDegrees,
    WorldPolygon Compiled,IReadOnlyList<MetadataGroundObservation> Observations);
public sealed record MetadataGroundIdentity(string ViewId,MetadataCaptureReconciliation Reconciliation);
public sealed record MetadataGroundCompilation(int Version,string Status,MetadataArtHandoff Handoff,
    MetadataGroundRequest Request,IReadOnlyList<MetadataGroundIdentity> Identities,
    WorldMetadataRecipe Recipe,WorldMetadataDocument World,
    IReadOnlyList<MetadataGroundFootprint> Footprints,IReadOnlyList<string> RemainingGates);

public static partial class MockupCapturePipeline
{
    private sealed record GroundViewResult(MetadataGroundIdentity Identity,IReadOnlyList<MetadataGroundFootprint> Footprints,int SourceBytes);

    public static async Task ValidateMetadataGroundCompilationAsync(MetadataGroundCompilation compilation,
        string trustedImageRoot,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(compilation);
        var replay=await CompileMetadataGroundAsync(compilation.Handoff,compilation.Request,trustedImageRoot,generator,token);
        if(JsonSerializer.Serialize(replay,WorldJson.CreateOptions())!=JsonSerializer.Serialize(compilation,WorldJson.CreateOptions()))
            throw new ArgumentException("METADATA_GROUND_COMPILATION_REPLAY_MISMATCH");
    }

    public static string MetadataGroundOverlay(MetadataGroundCompilation compilation,string viewId,byte[] verifiedPng,CancellationToken token=default)
    {
        var view=compilation.Request.Views.SingleOrDefault(v=>v.Id==viewId)??throw new ArgumentException("METADATA_GROUND_VIEW_REQUIRED");
        var capture=view.GroundCapture;
        var document=XDocument.Parse(Overlay(capture,verifiedPng,token));var root=document.Root!;XNamespace svg="http://www.w3.org/2000/svg";
        root.Element(svg+"title")!.Value="Ground capture correspondence: "+viewId+"; cyan dashed outlines preserve owning-view source dimensions. Draft, not visual approval.";
        var c=capture.Calibration??throw new ArgumentException("CAPTURE_CALIBRATION_REQUIRED");double det=c.A*c.E-c.B*c.D;
        string Number(double value)=>value.ToString("R",CultureInfo.InvariantCulture);
        string Pixel(WorldPoint p)=>Number((c.E*(p.X-c.C)-c.B*(p.Y-c.F))/det)+","+Number((-c.D*(p.X-c.C)+c.A*(p.Y-c.F))/det);
        double width=Math.Max(capture.Source.Width,capture.Source.Height)/500d;
        foreach(var label in root.Descendants(svg+"text"))label.SetAttributeValue("font-size",Number(width*8));
        foreach(var footprint in compilation.Footprints.Where(f=>f.Observations.Any(o=>o.ViewId==viewId)))
        {
            token.ThrowIfCancellationRequested();var observation=footprint.Observations.Single(o=>o.ViewId==viewId);
            root.Add(new XElement(svg+"g",new XAttribute("data-node-id",footprint.NodeId),new XAttribute("data-owner-view",footprint.OwnerViewId),
                new XElement(svg+"title",$"{footprint.NodeId}: owner {footprint.OwnerViewId}; paired deviation {Number(observation.PairedBoundaryDeviationMeters)}m; uncertainty {Number(observation.UncertaintyMeters)}m"),
                new XElement(svg+"polygon",new XAttribute("class","compiled-footprint"),new XAttribute("points",string.Join(" ",footprint.Compiled.Vertices.Select(Pixel))),
                    new XAttribute("fill","none"),new XAttribute("stroke","#00ffff"),new XAttribute("stroke-width",Number(width)),new XAttribute("stroke-dasharray",Number(width*5)+" "+Number(width*3)))));
        }
        return document.ToString();
    }

    // Called inside the existing atomic metadata publisher. Images are read and written
    // sequentially, rather than retaining all source PNGs/embedded overlays in memory.
    internal static async Task WriteMetadataGroundOverlaysAsync(MetadataGroundCompilation compilation,string trustedImageRoot,
        string stagingDirectory,CancellationToken token)
    {
        long bytes=0;
        foreach(var view in compilation.Request.Views)
        {
            var image=await ReadSourceAsync(view.GroundCapture,trustedImageRoot,token);
            if((bytes+=image.Length)>268435456)throw new ArgumentException("METADATA_GROUND_SOURCE_BYTE_BUDGET");
            await File.WriteAllTextAsync(Path.Combine(stagingDirectory,"capture-overlay-"+view.Id+".svg"),MetadataGroundOverlay(compilation,view.Id,image,token),token);
        }
    }

    public static async Task<MetadataGroundCompilation> CompileMetadataGroundAsync(MetadataArtHandoff handoff,
        MetadataGroundRequest request,string trustedImageRoot,WorldMetadataGenerator? generator=null,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(handoff);ArgumentNullException.ThrowIfNull(request);
        T Copy<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        handoff=Copy(handoff);request=Copy(request);generator??=new();
        if(request.Version!=2||request.Views is null||request.Views.Count is <1 or >128||request.Owners is null||request.Owners.Count is <1 or >10000||
            !double.IsFinite(request.ToleranceMeters)||request.ToleranceMeters is <=0 or >100||request.MaximumBoundaryWork is <1 or >20000000)
            throw new ArgumentException("METADATA_GROUND_REQUEST_INVALID_OR_STALE");
        var viewIds=new HashSet<string>(StringComparer.Ordinal);long count=0;
        foreach(var view in request.Views)
            if(view is null||!SettlementCatalogRegistry.ValidId(view.Id)||view.Id.Length>80||
                !view.Id.All(c=>(c>='a'&&c<='z')||(c>='0'&&c<='9')||c is '-' or '_')||
                !viewIds.Add(view.Id)||view.GroundCapture is null||
                view.IdentityCapture is null||view.IdentitySpecification is null||
                view.GroundCaptureSha256!=CaptureDigest(view.GroundCapture)||view.Bindings is null||view.Bindings.Count is <1 or >2000||
                (count+=view.Bindings.Count)>32000)
                throw new ArgumentException("METADATA_GROUND_REQUEST_INVALID_OR_STALE");
        new StructureFirstPipeline().ValidateMetadataHandoff(handoff,generator,token);
        var representedJobs=request.Views.Select(v=>v.IdentitySpecification.JobId).ToHashSet(StringComparer.Ordinal);
        foreach(var detail in handoff.Jobs.Where(j=>j.Kind=="DetailMockup"))
            if(!representedJobs.Contains(detail.Id))throw new ArgumentException("METADATA_GROUND_DETAIL_SCOPE_REQUIRED:"+detail.Id);
        if(handoff.World.Space.Unit!="meters"||handoff.World.Space.Axes.Count!=2||handoff.World.PlacementPlan is not {} sourcePlan)
            throw new ArgumentException("METADATA_GROUND_PHYSICAL_PROGRAM_REQUIRED");
        var physical=sourcePlan.Footprints.Concat(sourcePlan.NestedStages.SelectMany(s=>s.Footprints)).ToDictionary(f=>f.NodeId,StringComparer.Ordinal);
        var owners=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var owner in request.Owners)
            if(owner is null||owner.NodeId is null||!owners.TryAdd(owner.NodeId,owner.ViewId)||owner.ViewId is null||!viewIds.Contains(owner.ViewId))
                throw new ArgumentException("METADATA_GROUND_OWNER_INVALID");
        if(!owners.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(physical.Keys))throw new ArgumentException("METADATA_GROUND_OWNER_COVERAGE_MISMATCH");
        // Canonical declarations make a harmless input reorder leave the receipt and pins unchanged.
        request=request with{Views=request.Views.OrderBy(v=>v.Id,StringComparer.Ordinal).Select(v=>v with
            {Bindings=v.Bindings.OrderBy(b=>b?.NodeId,StringComparer.Ordinal).ToArray(),
             IdentitySpecification=v.IdentitySpecification with{Bindings=v.IdentitySpecification.Bindings?.OrderBy(b=>b?.NodeId,StringComparer.Ordinal).ThenBy(b=>b?.FeatureId,StringComparer.Ordinal).ToArray()!}}).ToArray(),
             Owners=request.Owners.OrderBy(o=>o.NodeId,StringComparer.Ordinal).ToArray()};
        long work=0,sourceBytes=0;
        void Charge(){token.ThrowIfCancellationRequested();if(++work>request.MaximumBoundaryWork)throw new ArgumentException("METADATA_GROUND_BOUNDARY_WORK_BUDGET");}
        var views=new List<GroundViewResult>();
        foreach(var view in request.Views)
        {
            var result=await QualifyMetadataGroundViewAsync(handoff,view,request.ToleranceMeters,trustedImageRoot,physical,sourcePlan,Charge,token);
            if((sourceBytes+=result.SourceBytes)>268435456)throw new ArgumentException("METADATA_GROUND_SOURCE_BYTE_BUDGET");
            views.Add(result);
        }
        var all=views.SelectMany(v=>v.Footprints).ToLookup(f=>f.NodeId,StringComparer.Ordinal);
        if(!all.Select(g=>g.Key).ToHashSet(StringComparer.Ordinal).SetEquals(physical.Keys))throw new ArgumentException("METADATA_GROUND_PHYSICAL_COVERAGE_MISMATCH");
        var results=new List<MetadataGroundFootprint>();
        foreach(var (nodeId,ownerId) in owners.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            Charge();var owner=all[nodeId].SingleOrDefault(f=>f.OwnerViewId==ownerId)??throw new ArgumentException("METADATA_GROUND_OWNER_OBSERVATION_REQUIRED:"+nodeId);
            var observations=new List<MetadataGroundObservation>();
            foreach(var other in all[nodeId].OrderBy(f=>f.OwnerViewId,StringComparer.Ordinal))
            {
                var observation=other.Observations.Single();double available=request.ToleranceMeters-observation.UncertaintyMeters;
                double angle=Math.Abs(Math.IEEERemainder(owner.RotationDegrees-other.RotationDegrees,360));
                if(owner.Origin.DistanceTo(other.Origin)>available||angle>1e-7)
                    throw new ArgumentException("METADATA_GROUND_VIEW_POSE_CONFLICT:"+nodeId+":"+other.OwnerViewId);
                double deviation=PairedBoundaryDeviation(owner.Compiled,observation.Observed,available,Charge);
                if(!double.IsFinite(deviation)||deviation>available)
                    throw new ArgumentException("METADATA_GROUND_VIEW_GEOMETRY_CONFLICT:"+nodeId+":"+other.OwnerViewId);
                observations.Add(observation with{PairedBoundaryDeviationMeters=deviation});
            }
            results.Add(owner with{Observations=observations});
        }
        var selected=results.ToDictionary(f=>f.NodeId,StringComparer.Ordinal);
        string digest=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request,WorldJson.CreateOptions())));
        (WorldPlacementRequest Request,IReadOnlyList<MetadataPlacementBinding> Bindings) Pin(WorldPlacementRequest original,
            IReadOnlyList<MetadataPlacementBinding> originalBindings,string? parentId)
        {
            var anchors=original.Anchors.ToList();var relations=original.Relations.ToList();
            foreach(var binding in originalBindings.OrderBy(b=>b.NodeId,StringComparer.Ordinal))
            {
                Charge();var origin=selected[binding.NodeId].Origin;
                if(parentId is not null)origin=origin.Offset(-selected[parentId].Origin.X,-selected[parentId].Origin.Y);
                string id="capture-"+digest[..12]+"-"+anchors.Count;
                if(original.Demands.Any(d=>d.Id==id)||anchors.Any(a=>a.Id==id)||relations.Any(r=>r.Id==id))throw new ArgumentException("METADATA_GROUND_PIN_ID_CONFLICT");
                anchors.Add(new(id,origin));relations.Add(new(id,binding.DemandId,id,"Preserve owning capture view origin","capture:"+digest,0,0,0,1));
            }
            return(original with{Anchors=anchors,Relations=relations},originalBindings.Select(b=>b with{RotationDegrees=selected[b.NodeId].RotationDegrees}).ToArray());
        }
        var root=Pin(sourcePlan.Definition.Request,sourcePlan.Definition.Bindings,null);
        var nested=sourcePlan.Definition.NestedStages?.Select(stage=>{var pinned=Pin(stage.Request,stage.Bindings,stage.ParentNodeId);return stage with{Request=pinned.Request,Bindings=pinned.Bindings};}).ToArray();
        var recipe=handoff.Recipe with{Placement=sourcePlan.Definition with{Request=root.Request,Bindings=root.Bindings,NestedStages=nested}};
        var world=generator.Generate(recipe,handoff.Catalog,token);
        if(!world.Nodes.Select(n=>n.Id).ToHashSet(StringComparer.Ordinal).SetEquals(handoff.World.Nodes.Select(n=>n.Id)))throw new ArgumentException("METADATA_GROUND_GENERATED_INVENTORY_MISMATCH");
        var nodes=world.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);
        var generated=world.PlacementPlan!.Footprints.Concat(world.PlacementPlan.NestedStages.SelectMany(s=>s.Footprints)).ToDictionary(f=>f.NodeId,StringComparer.Ordinal);
        foreach(var item in results)
        {
            if(nodes[item.NodeId].Position is not {Count:2} p||new WorldPoint(p[0],p[1]).DistanceTo(item.Origin)>1e-7)throw new ArgumentException("METADATA_GROUND_GENERATED_ANCHOR_MISMATCH:"+item.NodeId);
            var actual=new WorldPolygon(generated[item.NodeId].Polygon.Vertices.Select(v=>v.Offset(p[0],p[1])).ToArray());
            if(PairedBoundaryDeviation(item.Compiled,actual,1e-7,Charge)>1e-7)throw new ArgumentException("METADATA_GROUND_GENERATED_FOOTPRINT_MISMATCH:"+item.NodeId);
        }
        return new(2,"GroundLayoutDraft",handoff,request,views.Select(v=>v.Identity).ToArray(),recipe,world,results,
            ["Identity and ground annotations remain author/reviewer evidence, not automatic vision or art approval.",
             "Every compiled instance uses its explicitly selected view; other observations are retained and checked, never averaged or duplicated.",
             "Existing terrain, routes, reservations and site constraints are retained/revalidated; they are not newly traced from the images.",
             "Complete terrain/route/detail capture, source overlay and lifecycle review are required before claiming a faithful full map.",
             "Production assets and native Godot TileMapLayer/runtime acceptance remain unfinished."]);
    }

    private static async Task<GroundViewResult> QualifyMetadataGroundViewAsync(MetadataArtHandoff handoff,MetadataGroundView view,double tolerance,
        string trustedImageRoot,IReadOnlyDictionary<string,MetadataResolvedFootprint> physical,MetadataPlacementPlan sourcePlan,Action charge,CancellationToken token)
    {
        // Handoff was snapshotted and replayed once by the owning assembly compiler.
        var identity=await ReconcileMetadataCoreAsync(handoff,view.IdentityCapture,view.IdentitySpecification,trustedImageRoot,null,token,false);
        if(identity.Status!="DeclaredInventoryMatched")throw new ArgumentException("METADATA_GROUND_IDENTITY_UNRESOLVED:"+view.Id+":"+string.Join(",",identity.Issues.Select(i=>i.FeatureId).Distinct()));
        var job=handoff.Jobs.Single(j=>j.Id==identity.Specification.JobId);var selected=job.NodeIds.ToHashSet(StringComparer.Ordinal);
        var expected=physical.Keys.Where(selected.Contains).ToHashSet(StringComparer.Ordinal);
        var capture=view.GroundCapture;
        if(JsonSerializer.Serialize(capture.Source,WorldJson.CreateOptions())!=JsonSerializer.Serialize(identity.Capture.Source,WorldJson.CreateOptions()))throw new ArgumentException("METADATA_GROUND_SOURCE_REVISION_MISMATCH");
        int bytes=(await ReadSourceAsync(capture,trustedImageRoot,token)).Length;
        if(capture.Coverage!=CaptureCoverage.Complete||capture.Unresolved.Count!=0||capture.CompilationScope is not null)throw new ArgumentException("METADATA_GROUND_CAPTURE_INCOMPLETE");
        var calibration=capture.Calibration??throw new ArgumentException("CAPTURE_CALIBRATION_REQUIRED");
        var bindings=new Dictionary<string,MetadataGroundBinding>(StringComparer.Ordinal);var featureIds=new HashSet<string>(StringComparer.Ordinal);
        var features=capture.Features.ToDictionary(f=>f.Id,StringComparer.Ordinal);
        foreach(var binding in view.Bindings)
            if(binding is null||binding.NodeId is null||!bindings.TryAdd(binding.NodeId,binding)||binding.FeatureId is null||!featureIds.Add(binding.FeatureId)||
                !double.IsFinite(binding.RotationDegrees)||Math.Abs(binding.RotationDegrees)>360||!double.IsFinite(binding.OriginPixel.X)||!double.IsFinite(binding.OriginPixel.Y)||
                binding.OriginPixel.X<0||binding.OriginPixel.X>capture.Source.Width||binding.OriginPixel.Y<0||binding.OriginPixel.Y>capture.Source.Height)
                throw new ArgumentException("METADATA_GROUND_BINDING_INVALID");
        if(!bindings.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(expected)||!featureIds.SetEquals(features.Keys)||identity.Members.Any(m=>!physical.ContainsKey(m.NodeId)))
            throw new ArgumentException("METADATA_GROUND_PHYSICAL_COVERAGE_MISMATCH:"+view.Id);
        var nodes=handoff.World.Nodes.ToDictionary(n=>n.Id,StringComparer.Ordinal);var featureNodes=bindings.Values.ToDictionary(b=>b.FeatureId,b=>b.NodeId,StringComparer.Ordinal);
        foreach(var binding in bindings.Values)
        {
            string? parent=nodes[binding.NodeId].ParentId;while(parent is not null&&!expected.Contains(parent))parent=nodes[parent].ParentId;
            string? capturedParent=features[binding.FeatureId].ParentId;
            if((capturedParent is null?null:featureNodes[capturedParent])!=parent)throw new ArgumentException("METADATA_GROUND_PARENT_MISMATCH:"+binding.NodeId);
        }
        var sourceBindings=sourcePlan.Definition.Bindings.Concat(sourcePlan.Definition.NestedStages?.SelectMany(s=>s.Bindings)??[]).ToDictionary(b=>b.NodeId,StringComparer.Ordinal);
        double trace=calibration.A*calibration.A+calibration.B*calibration.B+calibration.D*calibration.D+calibration.E*calibration.E;
        double determinant=calibration.A*calibration.E-calibration.B*calibration.D;
        double scale=Math.Sqrt((trace+Math.Sqrt(Math.Max(0,trace*trace-4*determinant*determinant)))/2);
        var results=new List<MetadataGroundFootprint>();
        foreach(var (id,binding) in bindings.OrderBy(p=>p.Key,StringComparer.Ordinal))
        {
            charge();var feature=features[binding.FeatureId];
            if(feature.Shape!=CaptureShape.Polygon||!feature.Semantic.EndsWith(".footprint",StringComparison.Ordinal)||feature.Review!=CaptureReview.Accepted||
                feature.Evidence is not(CaptureEvidence.Observed or CaptureEvidence.Authored)||feature.Occluded)throw new ArgumentException("METADATA_GROUND_REVIEWED_FOOTPRINT_REQUIRED:"+id);
            double uncertainty=feature.UncertaintyPixels*scale+calibration.ToleranceMeters;
            if(!double.IsFinite(uncertainty)||uncertainty>tolerance)throw new ArgumentException("METADATA_GROUND_UNCERTAINTY_EXCEEDS_TOLERANCE:"+id);
            var observed=new WorldPolygon(feature.Points.Select(p=>MockupCaptureValidator.Transform(calibration,p)).ToArray());observed.Validate();
            var origin=MockupCaptureValidator.Transform(calibration,binding.OriginPixel);double angle=(binding.RotationDegrees-sourceBindings[id].RotationDegrees)*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
            var compiled=new WorldPolygon(physical[id].Polygon.Vertices.Select(p=>new WorldPoint(p.X*c-p.Y*s+origin.X,p.X*s+p.Y*c+origin.Y)).ToArray());compiled.Validate();
            double deviation=PairedBoundaryDeviation(compiled,observed,tolerance-uncertainty,charge);
            if(!double.IsFinite(deviation)||deviation+uncertainty>tolerance)throw new ArgumentException("METADATA_GROUND_SOURCE_DIMENSIONS_MISMATCH:"+id);
            results.Add(new(id,view.Id,origin,binding.RotationDegrees,compiled,[new(view.Id,feature.Id,origin,binding.RotationDegrees,observed,deviation,uncertainty)]));
        }
        return new(new(view.Id,identity),results,bytes);
    }

    // A continuous, piecewise-linear boundary correspondence. Evaluate the union of
    // normalized arclength breakpoints: distance is convex between consecutive breaks,
    // so the maximum lies at an endpoint. This is a conservative matching certificate,
    // not a claim to compute the minimum Hausdorff distance of arbitrary polygons.
    private static double PairedBoundaryDeviation(WorldPolygon source, WorldPolygon observed, double tolerance, Action charge)
    {
        double[] Fractions(IReadOnlyList<WorldPoint> p)
        {
            var values = new double[p.Count + 1];
            for (int i=0;i<p.Count;i++) { charge(); values[i+1]=values[i]+p[i].DistanceTo(p[(i+1)%p.Count]); }
            double length=values[^1]; for(int i=1;i<values.Length;i++)values[i]/=length; return values;
        }
        WorldPoint At(IReadOnlyList<WorldPoint> p, double[] fractions, double t, ref int index)
        {
            while(index<p.Count-1 && fractions[index+1]<t) {charge();index++;}
            double k=(t-fractions[index])/(fractions[index+1]-fractions[index]);var a=p[index];var b=p[(index+1)%p.Count];
            return new(a.X+(b.X-a.X)*k,a.Y+(b.Y-a.Y)*k);
        }
        var sf=Fractions(source.Vertices); double best=double.PositiveInfinity;
        for(int start=0;start<observed.Vertices.Count;start++)
        {
            charge(); if(source.Vertices[0].DistanceTo(observed.Vertices[start])>tolerance)continue;
            foreach(int direction in new[]{1,-1})
            {
                var points=Enumerable.Range(0,observed.Vertices.Count).Select(i=>observed.Vertices[(start+direction*i+observed.Vertices.Count)%observed.Vertices.Count]).ToArray();
                var fractions=Fractions(points); int si=0,oi=0;double maximum=0;
                foreach(double t in sf.Concat(fractions).Distinct().Order())
                {
                    charge(); maximum=Math.Max(maximum,At(source.Vertices,sf,t,ref si).DistanceTo(At(points,fractions,t,ref oi)));
                    if(maximum>tolerance)break;
                }
                best=Math.Min(best,maximum);if(best<=tolerance)return best;
            }
        }
        return best;
    }
}
