namespace WorldCreator.Core;

/// <summary>One fail-closed technical production gate. Approval must be supplied by
/// a trusted lifecycle record; caller-supplied state alone is insufficient proof.</summary>
public sealed record GameArtsReadinessResult(bool TechnicallyValid, bool Approved,
    IReadOnlyList<string> Issues)
{
    public bool Ready => TechnicallyValid && Approved && Issues.Count == 0;
}
public interface IGameArtsApprovalAuthority
{
    Task<bool> IsApprovedAsync(string assetId, string revision, CancellationToken token = default);
    Task<bool> IsApprovedArtifactAsync(string assetId,string revision,string artifactSha256,CancellationToken token=default);
}
public sealed partial class GameArtsProductionGate(IGameArtsApprovalAuthority approvals)
{
    public async Task<GameArtsReadinessResult> CheckAsync(
        string bundleFile, string approvedRevision, string assetId, string bundleAssetId, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundleFile);
        var issues = new List<string>();
        NativeBundleResult bundle;
        try
        {
            bundle = await new GameArtsBundleReader().ReadAsync(bundleFile, token);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException
            or InvalidOperationException or KeyNotFoundException or ArgumentException)
        {
            return new(false, false, ["BUNDLE_UNREADABLE:" + ex.GetType().Name]);
        }
        if (!bundle.IsValid)
            return new(false, false, bundle.Errors);
        if(string.IsNullOrWhiteSpace(assetId)||string.IsNullOrWhiteSpace(bundleAssetId)||bundle.Bundle!.AssetId!=bundleAssetId)
            return new(false,false,["NATIVE_BUNDLE_IDENTITY_MISMATCH"]);
        var approved = false;
        if (string.IsNullOrWhiteSpace(approvedRevision))
            issues.Add("APPROVED_REVISION_REQUIRED");
        else
        {
            await using var file=File.OpenRead(bundleFile);
            string hash=Convert.ToHexStringLower(await System.Security.Cryptography.SHA256.HashDataAsync(file,token));
            approved = await approvals.IsApprovedArtifactAsync(assetId, approvedRevision, hash, token);
        }
        if (!approved) issues.Add("LIFECYCLE_APPROVAL_NOT_VERIFIED");
        try
        {
            issues.AddRange(await new GameArtsBundleFileValidator().ValidateAsync(bundleFile, token));
            issues.AddRange(await new GameArtsMotionValidator().ValidateAsync(bundleFile, token));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException
            or InvalidOperationException or KeyNotFoundException or ArgumentException)
        {
            issues.Add("TECHNICAL_VALIDATION_FAILED:" + ex.GetType().Name);
        }
        var technical = !issues.Any(i => i != "APPROVED_REVISION_REQUIRED" &&
                                          i != "LIFECYCLE_APPROVAL_NOT_VERIFIED");
        return new(technical, approved, issues.Distinct(StringComparer.Ordinal).ToArray());
    }
}
