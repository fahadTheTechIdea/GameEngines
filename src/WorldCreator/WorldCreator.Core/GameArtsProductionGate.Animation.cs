using System.Text.Json;
using WorldCreator.Core.Capture;

namespace WorldCreator.Core;

public sealed record GameArtsAnimationFrameSource(string ComponentId,string FrameName,StructureImageReference Master);
/// <summary>Native pose baking consumes the approved sampled timeline, not unsampled curves.</summary>
public sealed record GameArtsAnimationRequest(int Version,string AssetId,string Revision,string BundlePath,string Facing,
    double DurationSeconds,bool Loop,double MaximumPoseIntervalSeconds,IReadOnlyList<GameArtsAnimationFrameSource> Sources);
public sealed record GameArtsAnimationComponent(string Id,string? Parent,string? Mount,int DrawOrder,
    IReadOnlyList<GameArtsMeasuredFrame> Frames,IReadOnlyList<double> DurationMilliseconds);
public sealed record GameArtsAnimationNode(string Id,int FrameIndex,IReadOnlyList<double> WorldMatrix);
public sealed record GameArtsAnimationPose(double NormalizedTime,IReadOnlyList<GameArtsAnimationNode> Nodes);
public sealed record GameArtsAnimationClip(int Version,string Status,GameArtsAnimationRequest Request,string BundleAssetId,
    string BundleSha256,double PixelsPerMeter,IReadOnlyList<GameArtsAnimationComponent> Components,IReadOnlyList<GameArtsAnimationPose> Poses,
    IReadOnlyList<string> MovingComponentIds);

public sealed partial class GameArtsProductionGate
{
    public async Task<GameArtsAnimationClip> ResolveAnimationAsync(GameArtsAnimationRequest request,IApprovedArtRegistry registry,
        string trustedRoot,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(registry);
        request=JsonSerializer.Deserialize<GameArtsAnimationRequest>(JsonSerializer.Serialize(request,WorldJson.CreateOptions()),WorldJson.CreateOptions())!;
        if(request.Version!=1||new[]{request.AssetId,request.Revision,request.Facing,request.BundlePath}.Any(string.IsNullOrWhiteSpace)||
            !double.IsFinite(request.DurationSeconds)||request.DurationSeconds is <=0 or >3600||
            !double.IsFinite(request.MaximumPoseIntervalSeconds)||request.MaximumPoseIntervalSeconds is <.001 or >1||request.Sources is not {Count:>=1 and <=2048}||
            request.Sources.Any(s=>s is null||s.Master is null)||request.Sources.Select(s=>(s.ComponentId,s.FrameName)).Distinct().Count()!=request.Sources.Count)
            throw new ArgumentException("GAMEARTS_ANIMATION_REQUEST_INVALID");
        string file=MockupCapturePipeline.ResolveSourcePath(trustedRoot,request.BundlePath);RequireSize(file,16777216);
        string hash=await FrameFileHashAsync(file,token);
        var manifest=await registry.ResolveAsync(request.AssetId,token);
        if(!WorldAssetDependencyManifestBuilder.IsComplete(manifest,request.AssetId)||manifest!.GameArtsRevision!=request.Revision)
            throw new ArgumentException("GAMEARTS_ANIMATION_NOT_APPROVED");
        var parsed=await new GameArtsBundleReader().ReadAsync(file,token);
        if(!parsed.IsValid||parsed.Bundle!.AssetId!=manifest.BundleAssetId||parsed.Bundle.Components.Count>64||!parsed.Bundle.Directions.Contains(request.Facing,StringComparer.Ordinal))
            throw new ArgumentException("GAMEARTS_ANIMATION_BUNDLE_INVALID");
        using var document=JsonDocument.Parse(await File.ReadAllTextAsync(file,token));var root=document.RootElement;
        var order=root.GetProperty("component_order").EnumerateArray().Select(v=>v.GetString()!).ToArray();
        if(!order.SequenceEqual(parsed.Bundle.Components.Select(c=>c.Id))||order.Length!=order.Distinct(StringComparer.Ordinal).Count())
            throw new ArgumentException("GAMEARTS_ANIMATION_COMPONENT_ORDER_INVALID");
        var parts=new List<GameArtsAnimationComponent>();var covered=new HashSet<(string,string)>();double? ppm=null;
        foreach(var part in parsed.Bundle.Components)
        {
            token.ThrowIfCancellationRequested();
            if(part.Id.Length>128||!part.Id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))throw new ArgumentException("GAMEARTS_ANIMATION_COMPONENT_ID_INVALID");
            if(part.Parent is not null&&!parts.Any(p=>p.Id==part.Parent))throw new ArgumentException("GAMEARTS_ANIMATION_PARENT_ORDER_INVALID");
            var sheets=manifest.Sheets.Where(s=>s.ComponentId==part.Id&&s.Directions.Contains(request.Facing,StringComparer.Ordinal)).ToArray();
            if(sheets.Length!=1)throw new ArgumentException("GAMEARTS_ANIMATION_SHEET_AMBIGUOUS");
            var sheet=sheets[0];string atlas=MockupCapturePipeline.ResolveSourcePath(trustedRoot,sheet.AtlasPath);RequireSize(atlas,16777216);
            using var atlasJson=JsonDocument.Parse(await File.ReadAllTextAsync(atlas,token));var frames=new List<GameArtsMeasuredFrame>();var durations=new List<double>();
            foreach(var frame in atlasJson.RootElement.GetProperty("frames").EnumerateArray())
            {
                string name=frame.GetProperty("name").GetString()!;
                var source=request.Sources.SingleOrDefault(s=>s.ComponentId==part.Id&&s.FrameName==name);
                if(source is null||!covered.Add((part.Id,name)))throw new ArgumentException("GAMEARTS_ANIMATION_FRAME_SOURCE_COVERAGE");
                double duration=frame.GetProperty("duration_ms").GetDouble();
                if(!double.IsFinite(duration)||duration is <=0 or >3600000)throw new ArgumentException("GAMEARTS_ANIMATION_FRAME_DURATION_INVALID");
                var measured=await ResolveMeasuredFrameAsync(new(request.AssetId,request.Revision,request.BundlePath,part.Id,request.Facing,sheet.Id,name),source.Master,registry,trustedRoot,false,token);
                ppm??=measured.PixelsPerMeter;
                if(ppm!=measured.PixelsPerMeter)throw new ArgumentException("GAMEARTS_ANIMATION_MIXED_SCALE");
                frames.Add(measured);durations.Add(duration);
            }
            var metadata=root.GetProperty("components").EnumerateArray().Single(c=>c.GetProperty("id").GetString()==part.Id);
            int draw=metadata.GetProperty("draw_order").GetProperty(request.Facing).GetInt32();
            if(draw is <-1024 or >1024)throw new ArgumentException("GAMEARTS_ANIMATION_DRAW_ORDER_LIMIT");
            parts.Add(new(part.Id,part.Parent,part.Mount,draw,frames,durations));
        }
        if(covered.Count!=request.Sources.Count)throw new ArgumentException("GAMEARTS_ANIMATION_FRAME_SOURCE_COVERAGE");
        var poses=new List<GameArtsAnimationPose>();
        foreach(var pose in root.GetProperty("sampled_poses").EnumerateArray().Where(p=>p.GetProperty("direction").GetString()==request.Facing).OrderBy(p=>p.GetProperty("normalized_time").GetDouble()))
        {
            token.ThrowIfCancellationRequested();double time=pose.GetProperty("normalized_time").GetDouble();
            if(poses.Count>=8192||!double.IsFinite(time)||time is <0 or >1||poses.Count>0&&time<=poses[^1].NormalizedTime)
                throw new ArgumentException("GAMEARTS_ANIMATION_POSE_TIME_INVALID");
            var nodes=new List<GameArtsAnimationNode>();
            if(pose.GetProperty("nodes").EnumerateObject().Count()!=parts.Count)throw new ArgumentException("GAMEARTS_ANIMATION_POSE_COVERAGE");
            foreach(var part in parts)
            {
                var node=pose.GetProperty("nodes").GetProperty(part.Id);int index=node.GetProperty("frame_index").GetInt32();
                if(index<0||index>=part.Frames.Count)throw new ArgumentException("GAMEARTS_ANIMATION_FRAME_INDEX_INVALID");
                double elapsed=0,targetTime=time*part.DurationMilliseconds.Sum();int expectedIndex=part.Frames.Count-1;
                for(int i=0;i<part.Frames.Count;i++)
                {
                    elapsed+=part.DurationMilliseconds[i];
                    if(targetTime<elapsed){expectedIndex=i;break;}
                }
                if(index!=expectedIndex)throw new ArgumentException("GAMEARTS_ANIMATION_FRAME_TIMING_MISMATCH");
                var frame=part.Frames[index];var rect=node.GetProperty("frame_rect");var pivot=node.GetProperty("pivot");
                if(node.GetProperty("image").GetString()!=Path.GetFileName(frame.ImagePath)||
                    !rect.EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual(new[]{frame.X,frame.Y,frame.Width,frame.Height})||
                    pivot[0].GetInt32()!=frame.Pivot.X||pivot[1].GetInt32()!=frame.Pivot.Y||node.GetProperty("draw_order").GetInt32()!=part.DrawOrder)
                    throw new ArgumentException("GAMEARTS_ANIMATION_POSE_FRAME_MISMATCH");
                var rows=node.GetProperty("world_matrix_2d");
                if(rows.GetArrayLength()!=3||rows.EnumerateArray().Any(r=>r.GetArrayLength()!=3))throw new ArgumentException("GAMEARTS_ANIMATION_MATRIX_INVALID");
                var matrix=rows.EnumerateArray().SelectMany(r=>r.EnumerateArray()).Select(v=>v.GetDouble()).ToArray();
                if(matrix.Any(v=>!double.IsFinite(v)||Math.Abs(v)>1e7)||matrix[6]!=0||matrix[7]!=0||matrix[8]!=1||Math.Abs(matrix[0]*matrix[4]-matrix[1]*matrix[3])<1e-8)
                    throw new ArgumentException("GAMEARTS_ANIMATION_MATRIX_INVALID");
                nodes.Add(new(part.Id,index,matrix));
            }
            poses.Add(new(time,nodes));
        }
        if(poses.Count<2||poses[0].NormalizedTime!=0||poses[^1].NormalizedTime!=1)throw new ArgumentException("GAMEARTS_ANIMATION_ENDPOINT_POSES_REQUIRED");
        if(poses.Zip(poses.Skip(1)).Any(pair=>(pair.Second.NormalizedTime-pair.First.NormalizedTime)*request.DurationSeconds>request.MaximumPoseIntervalSeconds+1e-10))
            throw new ArgumentException("GAMEARTS_ANIMATION_SAMPLE_INTERVAL_EXCEEDED");
        foreach(var part in parts)
        {
            double total=part.DurationMilliseconds.Sum(),elapsed=0;
            for(int i=0;i<part.Frames.Count;i++)
            {
                double boundary=elapsed/total;
                if(!poses.Any(p=>Math.Abs(p.NormalizedTime-boundary)<1e-10&&p.Nodes.Single(n=>n.Id==part.Id).FrameIndex==i))
                    throw new ArgumentException("GAMEARTS_ANIMATION_FRAME_BOUNDARY_POSE_REQUIRED:"+part.Id);
                elapsed+=part.DurationMilliseconds[i];
            }
        }
        if(request.Loop&&parts.Any(part=>
        {
            var first=poses[0].Nodes.Single(n=>n.Id==part.Id);var last=poses[^1].Nodes.Single(n=>n.Id==part.Id);
            return !first.WorldMatrix.Zip(last.WorldMatrix).All(v=>Math.Abs(v.First-v.Second)<1e-6);
        }))throw new ArgumentException("GAMEARTS_ANIMATION_LOOP_SEAM_MISMATCH");
        if(hash!=await FrameFileHashAsync(file,token)||!await approvals.IsApprovedArtifactAsync(request.AssetId,request.Revision,hash,token))
            throw new ArgumentException("GAMEARTS_ANIMATION_CHANGED_DURING_RESOLUTION");
        var moving=parts.Where(part=>part.Frames.Count>1||poses.Skip(1).Any(p=>
            !p.Nodes.Single(n=>n.Id==part.Id).WorldMatrix.Zip(poses[0].Nodes.Single(n=>n.Id==part.Id).WorldMatrix).All(v=>Math.Abs(v.First-v.Second)<1e-8)))
            .Select(p=>p.Id).ToArray();
        return new(1,"SampledPoseClipVerified",request,manifest.BundleAssetId,hash,ppm!.Value,parts,poses,moving);
    }
}
