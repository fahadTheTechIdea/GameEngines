using System.Text.Json;
namespace WorldCreator.Core.Metadata;

public sealed class MetadataLineLayout : IMetadataLayout,IWorldGenerationDependency
{
    public string ConfigurationSha256=>WorldGenerationDependencyIdentity.EmptyConfigurationSha256;
    public string Id=>"line";
    public sealed record Settings(IReadOnlyList<double> Origin,IReadOnlyList<double> Step);
    public static MetadataLayout Parameters(IReadOnlyList<double> origin,IReadOnlyList<double> step)=>
        new("line",JsonSerializer.SerializeToElement(new Settings(origin,step)));
    public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
    {
        var p=MetadataLayoutInput.Read<Settings>(parameters,dimensions,count,token);
        if(!MetadataLayoutInput.Vector(p.Origin,dimensions)||!MetadataLayoutInput.Vector(p.Step,dimensions))
            throw new ArgumentException("METADATA_LINE_VECTORS_INVALID");
        var positions=new IReadOnlyList<double>[count];
        for(int i=0;i<count;i++)
        {
            token.ThrowIfCancellationRequested();
            var point=p.Origin.Select((v,axis)=>v+i*p.Step[axis]).ToArray();
            if(point.Any(v=>!double.IsFinite(v)))throw new ArgumentException("METADATA_POSITION_NONFINITE");
            positions[i]=point;
        }
        return positions;
    }
}
