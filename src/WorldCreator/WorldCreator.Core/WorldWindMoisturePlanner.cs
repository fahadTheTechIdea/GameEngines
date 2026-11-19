using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldWindMoistureDefinition(int SchemaVersion,string Id,string Purpose,string SourceRuleId,WorldPoint Direction,
    double BoundaryHumidity,double BaseCondensation,double RiseCondensationPerUnit,IReadOnlyDictionary<string,double> EvaporationByKind,
    int MaximumSamples=16384,long MaximumWork=20000000);
public sealed record WorldWindMoistureNode(int SampleIndex,double Projection,IReadOnlyList<int> UpwindSamples,
    double Incoming,double Evaporated,double Precipitation,double Outgoing);
public sealed record WorldWindMoisturePlan(int SchemaVersion,WorldSurfaceDefinition Surface,WorldWindMoistureDefinition Definition,string SurfaceSha256,
    IReadOnlyList<WorldSampledScalarField> Fields,IReadOnlyList<WorldWindMoistureNode> Nodes,long Work);

/// <summary>Optional game-authored moisture grammar over the terrain rectangle.
/// Air may pass over ground-domain voids. Ground masks still gate all consumers.
/// Normalized humidity mixing is not conserved physical atmospheric mass.</summary>
public sealed class WorldWindMoisturePlanner
{
    public WorldWindMoisturePlan Plan(WorldSurfaceDefinition surface,WorldWindMoistureDefinition definition,CancellationToken token=default)
    {
        token.ThrowIfCancellationRequested();
        static bool Fraction(double value)=>double.IsFinite(value)&&value is >=0 and <=1;
        if(definition is null||definition.SchemaVersion!=1||!WorldEnvironmentQuery.Id(definition.Id)||definition.Id.Length>96||
            !WorldEnvironmentQuery.Text(definition.Purpose)||!WorldEnvironmentQuery.Text(definition.SourceRuleId)||
            !double.IsFinite(definition.Direction.X)||!double.IsFinite(definition.Direction.Y)||Math.Abs(definition.Direction.X)>1e6||Math.Abs(definition.Direction.Y)>1e6||
            definition.Direction.X==0&&definition.Direction.Y==0||!Fraction(definition.BoundaryHumidity)||!Fraction(definition.BaseCondensation)||
            !double.IsFinite(definition.RiseCondensationPerUnit)||definition.RiseCondensationPerUnit is <0 or >1000000||
            definition.EvaporationByKind is null||definition.EvaporationByKind.Count>32||definition.EvaporationByKind.Any(p=>!WorldEnvironmentQuery.Id(p.Key)||!Fraction(p.Value))||
            definition.MaximumSamples is <4 or >263169||definition.MaximumWork is <1 or >200000000)
            throw new ArgumentException("WORLD_WIND_MOISTURE_DEFINITION_INVALID");
        definition=definition with{EvaporationByKind=new System.Collections.ObjectModel.ReadOnlyDictionary<string,double>(
            new SortedDictionary<string,double>(definition.EvaporationByKind.ToDictionary(p=>p.Key,p=>p.Value),StringComparer.Ordinal))};
        long raw=surface?.HeightField is {} h?(long)h.Columns*h.Rows:surface?.Terrain is {} t?(long)t.Columns*t.Rows:0;
        if(raw>definition.MaximumSamples)throw new ArgumentException("WORLD_WIND_MOISTURE_SAMPLE_BUDGET");
        var query=new WorldSurfaceQuery(surface!,token);var field=query.HeightField?.CompleteField??throw new ArgumentException("WORLD_WIND_MOISTURE_HEIGHTFIELD_REQUIRED");
        int count=field.Heights.Count;if(count>definition.MaximumSamples)throw new ArgumentException("WORLD_WIND_MOISTURE_SAMPLE_BUDGET");
        var budget=new WorldSpatialWorkBudget(definition.MaximumWork,token,failureCode:"WORLD_WIND_MOISTURE_WORK_BUDGET");
        double directionScale=Math.Max(Math.Abs(definition.Direction.X),Math.Abs(definition.Direction.Y));
        double sx=definition.Direction.X/directionScale,sy=definition.Direction.Y/directionScale,length=Math.Sqrt(sx*sx+sy*sy);
        double dx=sx/length,dy=sy/length;
        var projection=new double[count];var humidity=new double[count];var rainfall=new double[count];var nodes=new WorldWindMoistureNode[count];
        for(int i=0;i<count;i++){budget.Charge();projection[i]=(i%field.Columns)*field.StepX*dx+(i/field.Columns)*field.StepY*dy;}
        budget.Charge(count*(long)Math.Ceiling(Math.Log2(count)));
        var order=Enumerable.Range(0,count).OrderBy(i=>projection[i]).ThenBy(i=>i).ToArray();
        long sampleCost=1+(query.Definition.Boundary?.Vertices.Count??0)+query.Definition.Exclusions.Sum(e=>e.Boundary.Vertices.Count)+
            query.Definition.Patches.Sum(p=>p.Boundary.Vertices.Count);
        foreach(int i in order)
        {
            budget.Charge();int col=i%field.Columns,row=i/field.Columns;var upstream=new List<int>();
            double weightedHumidity=0,weightedHeight=0,weight=0;
            foreach(var (x,y) in new[]{(-1,-1),(0,-1),(-1,0),(1,0),(0,1),(1,1)})
            {
                budget.Charge();if(col+x<0||col+x>=field.Columns||row+y<0||row+y>=field.Rows)continue;
                int j=i+y*field.Columns+x;if(projection[j]>=projection[i])continue;
                double w=(projection[i]-projection[j])/Math.Sqrt(x*x*field.StepX*field.StepX+y*y*field.StepY*field.StepY);
                upstream.Add(j);weight+=w;weightedHumidity+=w*humidity[j];weightedHeight+=w*field.Heights[j];
            }
            double incoming=weight>0?Math.Clamp(weightedHumidity/weight,0,1):definition.BoundaryHumidity;
            double rise=weight>0?Math.Max(0,field.Heights[i]-weightedHeight/weight):0;
            budget.Charge(sampleCost);var sample=query.Sample(new(field.Origin.X+col*field.StepX,field.Origin.Y+row*field.StepY),token);
            if(sample.Status==WorldSurfaceStatus.Ambiguous)throw new ArgumentException("WORLD_WIND_MOISTURE_AMBIGUOUS_SAMPLE:"+i);
            double supply=sample.Status==WorldSurfaceStatus.Sampled?definition.EvaporationByKind.GetValueOrDefault(sample.Kind!):0;
            double evaporated=Math.Min(supply,Math.Max(0,1-incoming));
            double available=incoming+evaporated;
            rainfall[i]=available*Math.Clamp(definition.BaseCondensation+definition.RiseCondensationPerUnit*rise,0,1);
            humidity[i]=available-rainfall[i];
            nodes[i]=new(i,projection[i],upstream.AsReadOnly(),incoming,evaporated,rainfall[i],humidity[i]);
        }
        WorldSampledScalarField Scalar(string suffix,double[] values)=>new(1,definition.Id+"-"+suffix,definition.Purpose,definition.SourceRuleId,"relative",
            field.Origin,field.StepX,field.StepY,field.Columns,field.Rows,Array.AsReadOnly(values));
        var fields=new[]{Scalar("humidity",humidity),Scalar("rainfall",rainfall)};
        foreach(var scalar in fields){budget.Charge(count);_=new WorldSampledScalarQuery(scalar,query.HeightField!.Geometry,token);}
        return new(1,query.Definition,definition,WorldGenerationDependencyIdentity.Digest(query.Definition),Array.AsReadOnly(fields),Array.AsReadOnly(nodes),budget.Used);
    }
    public void Validate(WorldWindMoisturePlan plan,CancellationToken token=default)
    {
        ArgumentNullException.ThrowIfNull(plan);var expected=Plan(plan.Surface,plan.Definition,token);
        if(JsonSerializer.Serialize(expected,WorldJson.CreateOptions())!=JsonSerializer.Serialize(plan,WorldJson.CreateOptions()))
            throw new ArgumentException("WORLD_WIND_MOISTURE_PLAN_MISMATCH");
    }
    public WorldEnvironmentDefinition Apply(WorldEnvironmentDefinition environment,WorldWindMoisturePlan plan,CancellationToken token=default)
    {
        Validate(plan,token);ArgumentNullException.ThrowIfNull(environment);
        return new WorldEnvironmentQuery(new(plan.Surface,token),environment with{SampledFields=(environment.SampledFields??[]).Concat(plan.Fields).ToArray()},token).Definition;
    }
}
