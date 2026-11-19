using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Capture;

namespace WorldCreator.Core;

public sealed record GameArtsMeasuredFrame(StructureProductionReference Reference,string BundleAssetId,
    string ImagePath,string ImageSha256,string AtlasSha256,string BundleSha256,string MasterSha256,
    int ImageWidth,int ImageHeight,int X,int Y,int Width,int Height,int AtlasFrameCount,double PixelsPerMeter,
    PixelAnchor Pivot,IReadOnlyDictionary<string,PixelAnchor> Anchors);

public sealed partial class GameArtsProductionGate
{
    /// <summary>Resolves one measured frame through the existing native bundle,
    /// file-integrity and external lifecycle gates. Does not grant visual approval.</summary>
    public async Task<GameArtsMeasuredFrame> ResolveFrameAsync(StructureProductionReference reference,
        StructureImageReference master,IApprovedArtRegistry registry,string trustedRoot,
        CancellationToken token=default)
        =>await ResolveMeasuredFrameAsync(reference,master,registry,trustedRoot,true,token);

    private async Task<GameArtsMeasuredFrame> ResolveMeasuredFrameAsync(StructureProductionReference reference,
        StructureImageReference master,IApprovedArtRegistry registry,string trustedRoot,bool singleComponent,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(reference);ArgumentNullException.ThrowIfNull(master);
        if(new[]{reference.AssetId,reference.Revision,reference.ComponentId,reference.Facing,reference.SheetId,reference.FrameName}.Any(string.IsNullOrWhiteSpace)||
            master.Sha256 is not {Length:64}||!master.Sha256.All(Uri.IsHexDigit))
            throw new ArgumentException("GAMEARTS_FRAME_REFERENCE_INVALID");
        var manifest=await registry.ResolveAsync(reference.AssetId,token);
        if(!WorldAssetDependencyManifestBuilder.IsComplete(manifest,reference.AssetId)||manifest!.GameArtsRevision!=reference.Revision)
            throw new ArgumentException("GAMEARTS_FRAME_MANIFEST_NOT_APPROVED");
        string bundle=MockupCapturePipeline.ResolveSourcePath(trustedRoot,reference.BundlePath);
        RequireSize(bundle,16777216);
        string bundleHash=await FrameFileHashAsync(bundle,token);
        // Validate safe paths and sizes for every referenced sheet before the
        // shared verifier or native gate reads any of those files.
        if(manifest!.Sheets.Count>256)throw new ArgumentException("GAMEARTS_FRAME_SHEET_LIMIT");
        long bytes=0;
        foreach(var sheet in manifest.Sheets)
        {
            string pngPath=MockupCapturePipeline.ResolveSourcePath(trustedRoot,sheet.ImagePath);
            string atlasPath=MockupCapturePipeline.ResolveSourcePath(trustedRoot,sheet.AtlasPath);
            RequireSize(pngPath,67108864);RequireSize(atlasPath,16777216);
            if((bytes+=new FileInfo(pngPath).Length+new FileInfo(atlasPath).Length)>268435456)
                throw new ArgumentException("GAMEARTS_FRAME_SOURCE_BYTE_LIMIT");
            using var pinned=JsonDocument.Parse(await File.ReadAllTextAsync(atlasPath,token));
            string sibling=pinned.RootElement.GetProperty("image").GetString()!;
            if(string.IsNullOrWhiteSpace(sibling)||sibling.Contains('/')||sibling.Contains('\\')||sibling.Contains(':')||
                !SamePath(pngPath,Path.Combine(Path.GetDirectoryName(atlasPath)!,sibling)))
                throw new ArgumentException("GAMEARTS_FRAME_ATLAS_IMAGE_MISMATCH");
        }
        var files=await new GameArtsFileVerifier(trustedRoot).VerifyAsync(manifest,token);
        if(!files.IsValid)throw new ArgumentException("GAMEARTS_FRAME_FILES_INVALID:"+string.Join(",",files.Issues.Select(i=>i.Code)));
        var parsed=await new GameArtsBundleReader().ReadAsync(bundle,token);
        if(!parsed.IsValid||parsed.Bundle!.AssetId!=manifest.BundleAssetId||singleComponent&&parsed.Bundle.Components.Count!=1||
            !parsed.Bundle.Components.Any(c=>c.Id==reference.ComponentId)||!parsed.Bundle.Directions.Contains(reference.Facing,StringComparer.Ordinal))
            throw new ArgumentException("GAMEARTS_FRAME_STATIC_COMPONENT_REQUIRED");
        var component=parsed.Bundle.Components.Single(c=>c.Id==reference.ComponentId);
        // Native bundle references must resolve to precisely the pinned manifest
        // sheets; a valid but unrelated sibling atlas is insufficient.
        foreach(var direction in parsed.Bundle.Directions)
        {
            string relative=component.Atlases[direction];
            if(string.IsNullOrWhiteSpace(relative)||Path.GetFileName(relative)!=relative||relative.Contains('\\')||relative.Contains('/')||relative.Contains(':'))
                throw new ArgumentException("GAMEARTS_FRAME_BUNDLE_PATH_INVALID");
            string full=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(bundle)!,relative));
            if(!manifest.Sheets.Any(s=>s.ComponentId==reference.ComponentId&&s.Directions.Contains(direction,StringComparer.Ordinal)&&
                SamePath(full,MockupCapturePipeline.ResolveSourcePath(trustedRoot,s.AtlasPath))))
                throw new ArgumentException("GAMEARTS_FRAME_BUNDLE_SHEET_MISMATCH");
        }
        var gate=await CheckAsync(bundle,reference.Revision,reference.AssetId,manifest.BundleAssetId,token);
        if(!gate.Ready)throw new ArgumentException("GAMEARTS_FRAME_PRODUCTION_GATE:"+string.Join(",",gate.Issues));
        var selected=manifest.Sheets.Where(s=>s.Id==reference.SheetId&&s.ComponentId==reference.ComponentId&&s.Directions.Contains(reference.Facing,StringComparer.Ordinal)).ToArray();
        if(selected.Length!=1)throw new ArgumentException("GAMEARTS_FRAME_SHEET_MISSING");
        var chosen=selected[0];string atlas=MockupCapturePipeline.ResolveSourcePath(trustedRoot,chosen.AtlasPath);
        if(!SamePath(atlas,Path.Combine(Path.GetDirectoryName(bundle)!,component.Atlases[reference.Facing])))
            throw new ArgumentException("GAMEARTS_FRAME_BUNDLE_SHEET_MISMATCH");
        using var json=JsonDocument.Parse(await File.ReadAllTextAsync(atlas,token));var root=json.RootElement;
        string image=MockupCapturePipeline.ResolveSourcePath(trustedRoot,chosen.ImagePath);
        if(!SamePath(image,Path.Combine(Path.GetDirectoryName(atlas)!,root.GetProperty("image").GetString()!)))
            throw new ArgumentException("GAMEARTS_FRAME_ATLAS_IMAGE_MISMATCH");
        if(root.TryGetProperty("allow_near_key",out var relaxed)&&relaxed.GetBoolean())
            throw new ArgumentException("GAMEARTS_FRAME_DIAGNOSTIC_REJECTED");
        if(!root.TryGetProperty("pixels_per_meter",out var scale)||!scale.TryGetDouble(out double ppm)||!double.IsFinite(ppm)||ppm is <=0 or >16384)
            throw new ArgumentException("GAMEARTS_FRAME_SCALE_REQUIRED");
        var frames=root.GetProperty("frames").EnumerateArray().Where(f=>f.TryGetProperty("name",out var n)&&n.GetString()==reference.FrameName).ToArray();
        if(frames.Length!=1)throw new ArgumentException("GAMEARTS_FRAME_NAME_REQUIRED");
        var frame=frames[0];
        if(!frame.TryGetProperty("source_sha256",out var source)||!string.Equals(source.GetString(),master.Sha256,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("GAMEARTS_FRAME_MASTER_LINEAGE_MISMATCH");
        string sourceFile=MockupCapturePipeline.ResolveSourcePath(trustedRoot,master.File);RequireSize(sourceFile,67108864);
        await using(var stream=File.OpenRead(sourceFile))
            if(!Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,token)).Equals(master.Sha256,StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("GAMEARTS_FRAME_MASTER_CHANGED");
        var anchors=new Dictionary<string,PixelAnchor>(StringComparer.Ordinal);
        if(frame.TryGetProperty("anchors",out var points))foreach(var point in points.EnumerateObject())
            if(!anchors.TryAdd(point.Name,new(point.Value[0].GetInt32(),point.Value[1].GetInt32())))
                throw new ArgumentException("GAMEARTS_FRAME_ANCHOR_DUPLICATE");
        var rect=frame.GetProperty("rect");var pivot=frame.GetProperty("pivot");var size=root.GetProperty("image_size");
        if(await FrameFileHashAsync(bundle,token)!=bundleHash||
            !string.Equals(await FrameFileHashAsync(atlas,token),chosen.AtlasSha256,StringComparison.OrdinalIgnoreCase)||
            !string.Equals(await FrameFileHashAsync(image,token),chosen.ImageSha256,StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("GAMEARTS_FRAME_SOURCE_CHANGED_DURING_RESOLUTION");
        return new(reference,manifest.BundleAssetId,chosen.ImagePath,chosen.ImageSha256,chosen.AtlasSha256,bundleHash,master.Sha256,
            size[0].GetInt32(),size[1].GetInt32(),rect[0].GetInt32(),rect[1].GetInt32(),rect[2].GetInt32(),rect[3].GetInt32(),root.GetProperty("frames").GetArrayLength(),ppm,
            new(pivot[0].GetInt32(),pivot[1].GetInt32()),anchors);
    }

    private static void RequireSize(string file,long limit)
    {if(new FileInfo(file).Length is var length&&(length<=0||length>limit))throw new ArgumentException("GAMEARTS_FRAME_FILE_SIZE_LIMIT");}
    private static bool SamePath(string a,string b)=>string.Equals(Path.GetFullPath(a),Path.GetFullPath(b),
        OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);
    private static async Task<string> FrameFileHashAsync(string file,CancellationToken token)
    {await using var stream=File.OpenRead(file);return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream,token));}
}
