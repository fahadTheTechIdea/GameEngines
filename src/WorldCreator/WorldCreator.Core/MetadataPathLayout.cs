using System.Text.Json;
using WorldCreator.Core.Geometry;
namespace WorldCreator.Core.Metadata;

/// <summary>Places locations at declared distances along an authored polyline,
/// suitable for routes, stage sections and other game-defined spatial programs.</summary>
public sealed class MetadataPathLayout : IMetadataLayout,IWorldGenerationDependency
{
    public string ConfigurationSha256=>WorldGenerationDependencyIdentity.EmptyConfigurationSha256;
    public string Id=>"path";
    public sealed record Settings(IReadOnlyList<IReadOnlyList<double>> Points,double Spacing,double StartDistance=0);
    public static MetadataLayout Parameters(IReadOnlyList<IReadOnlyList<double>> points,double spacing,double startDistance=0)=>
        new("path",JsonSerializer.SerializeToElement(new Settings(points,spacing,startDistance)));
    public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
    {
        var p=MetadataLayoutInput.Read<Settings>(parameters,dimensions,count,token);
        if(p.Points is not {Count:>=2 and <=4096}||p.Points.Any(v=>!MetadataLayoutInput.Vector(v,dimensions))||
            !double.IsFinite(p.Spacing)||p.Spacing<=0||!double.IsFinite(p.StartDistance)||p.StartDistance<0)
            throw new ArgumentException("METADATA_PATH_PARAMETERS_INVALID");
        return new WorldArcLengthPath(p.Points,dimensions,token).Sample(count,p.Spacing,p.StartDistance,token:token);
    }
}
