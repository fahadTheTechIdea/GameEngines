using System.Text.Json;
namespace WorldCreator.Core.Metadata;

/// <summary>Places a declared group on an arc or closed ring in two selected
/// axes. This is a spatial grammar, not orbital or other physical simulation.</summary>
public sealed class MetadataArcLayout : IMetadataLayout,IWorldGenerationDependency
{
    public string ConfigurationSha256=>WorldGenerationDependencyIdentity.EmptyConfigurationSha256;
    public string Id=>"arc";
    public sealed record Settings(IReadOnlyList<double> Center,double Radius,int AxisU=0,int AxisV=1,
        double StartDegrees=0,double SweepDegrees=360);
    public static MetadataLayout Parameters(IReadOnlyList<double> center,double radius,int axisU=0,int axisV=1,
        double startDegrees=0,double sweepDegrees=360)=>new("arc",JsonSerializer.SerializeToElement(
            new Settings(center,radius,axisU,axisV,startDegrees,sweepDegrees)));
    public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
    {
        var p=MetadataLayoutInput.Read<Settings>(parameters,dimensions,count,token);
        if(!MetadataLayoutInput.Vector(p.Center,dimensions)||!double.IsFinite(p.Radius)||p.Radius<=0||
            p.AxisU<0||p.AxisU>=dimensions||p.AxisV<0||p.AxisV>=dimensions||p.AxisU==p.AxisV||
            !double.IsFinite(p.StartDegrees)||!double.IsFinite(p.SweepDegrees)||p.SweepDegrees==0||Math.Abs(p.SweepDegrees)>360)
            throw new ArgumentException("METADATA_ARC_PARAMETERS_INVALID");
        var positions=new IReadOnlyList<double>[count];
        for(int i=0;i<count;i++)
        {
            token.ThrowIfCancellationRequested();
            double degrees=p.StartDegrees%360+i*p.SweepDegrees/Math.Max(1,Math.Abs(p.SweepDegrees)==360?count:count-1);
            double angle=degrees*Math.PI/180;var point=p.Center.ToArray();
            point[p.AxisU]+=p.Radius*Math.Cos(angle);point[p.AxisV]+=p.Radius*Math.Sin(angle);
            if(point.Any(v=>!double.IsFinite(v)))throw new ArgumentException("METADATA_POSITION_NONFINITE");
            positions[i]=point;
        }
        return positions;
    }
}
