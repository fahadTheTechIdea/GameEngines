using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

/// <summary>Places an authored sequence on a spiral or ring in two selected axes.
/// Positions are a layout grammar, not physical or orbital simulation.</summary>
public sealed class MetadataSpiralLayout : IMetadataLayout,IWorldGenerationDependency
{
    public string Id=>"spiral";
    public string ConfigurationSha256=>WorldGenerationDependencyIdentity.EmptyConfigurationSha256;
    public sealed record Settings(IReadOnlyList<double> Center,double StartRadius,double RadiusStep,double AngleStepDegrees,
        int AxisU=0,int AxisV=1,double StartDegrees=0,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? AxisRatio=null,
        [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? OrientationDegrees=null);
    public static MetadataLayout Parameters(IReadOnlyList<double> center,double startRadius,double radiusStep,double angleStepDegrees,
        int axisU=0,int axisV=1,double startDegrees=0,double? axisRatio=null,double? orientationDegrees=null)=>new("spiral",JsonSerializer.SerializeToElement(
            new Settings(center,startRadius,radiusStep,angleStepDegrees,axisU,axisV,startDegrees,axisRatio,orientationDegrees)));
    public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement parameters,int dimensions,int count,CancellationToken token=default)
    {
        var p=MetadataLayoutInput.Read<Settings>(parameters,dimensions,count,token);
        return PlanSettings(p,dimensions,count,token);
    }
    internal static IReadOnlyList<IReadOnlyList<double>> PlanSettings(Settings p,int dimensions,int count,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(p is null||dimensions is <1 or >8||count is <0 or >100000)throw new ArgumentException("METADATA_LAYOUT_INPUT_INVALID");
        double endRadius=p.StartRadius+Math.Max(0,count-1)*p.RadiusStep;
        if(!MetadataLayoutInput.Vector(p.Center,dimensions)||!double.IsFinite(p.StartRadius)||p.StartRadius<0||
            !double.IsFinite(p.RadiusStep)||(p.RadiusStep==0&&p.StartRadius==0)||!double.IsFinite(endRadius)||endRadius<0||
            !double.IsFinite(p.AngleStepDegrees)||p.AngleStepDegrees==0||Math.Abs(p.AngleStepDegrees)>360||
            !double.IsFinite(p.StartDegrees)||p.AxisRatio is {} ratio&&(!double.IsFinite(ratio)||ratio is <.000001 or >1000000)||
            p.OrientationDegrees is {} orientation&&!double.IsFinite(orientation)||p.AxisU<0||p.AxisU>=dimensions||p.AxisV<0||p.AxisV>=dimensions||p.AxisU==p.AxisV)
            throw new ArgumentException("METADATA_SPIRAL_PARAMETERS_INVALID");
        var positions=new IReadOnlyList<double>[count];
        double rotation=(p.OrientationDegrees.GetValueOrDefault()%360)*Math.PI/180,cosRotation=Math.Cos(rotation),sinRotation=Math.Sin(rotation),axisRatio=p.AxisRatio??1;
        for(int i=0;i<count;i++)
        {
            token.ThrowIfCancellationRequested();
            double radius=p.StartRadius+i*p.RadiusStep;
            double angle=((p.StartDegrees%360+i*p.AngleStepDegrees)%360)*Math.PI/180;
            double u=radius*Math.Cos(angle),v=radius*Math.Sin(angle)*axisRatio;
            var point=p.Center.ToArray();point[p.AxisU]+=u*cosRotation-v*sinRotation;point[p.AxisV]+=u*sinRotation+v*cosRotation;
            if(point.Any(v=>!double.IsFinite(v)))throw new ArgumentException("METADATA_POSITION_NONFINITE");
            positions[i]=point;
        }
        return positions;
    }
    internal static IReadOnlyList<WorldPopulationPose> PlanPoses2D(Settings p,int count,CancellationToken token)
    {
        var positions=PlanSettings(p,2,count,token);var result=new WorldPopulationPose[count];
        double rotation=(p.OrientationDegrees.GetValueOrDefault()%360)*Math.PI/180,cosRotation=Math.Cos(rotation),sinRotation=Math.Sin(rotation),axisRatio=p.AxisRatio??1;
        for(int i=0;i<count;i++)
        {
            token.ThrowIfCancellationRequested();
            double radius=p.StartRadius+i*p.RadiusStep;
            double angle=((p.StartDegrees%360+i*p.AngleStepDegrees)%360)*Math.PI/180;
            // Normalize before multiplying so finite large radii cannot overflow
            // the derivative. A zero-step ring has positive radius, so scale is positive.
            double scale=Math.Max(Math.Abs(p.RadiusStep),radius),radial=p.RadiusStep/scale;
            double angular=(radius/scale)*(p.AngleStepDegrees*Math.PI/180);
            double du=radial*Math.Cos(angle)-angular*Math.Sin(angle);
            double dv=(radial*Math.Sin(angle)+angular*Math.Cos(angle))*axisRatio;
            (du,dv)=(du*cosRotation-dv*sinRotation,du*sinRotation+dv*cosRotation);
            double dx=p.AxisU==0?du:dv,dy=p.AxisU==1?du:dv;
            result[i]=new(new WorldPoint(positions[i][0],positions[i][1]),Math.Atan2(dy,dx)*180/Math.PI);
        }
        return Array.AsReadOnly(result);
    }
}
