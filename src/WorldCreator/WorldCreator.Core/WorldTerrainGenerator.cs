using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

public enum WorldLandformMode { Add, Level }
public enum WorldLandformFalloff { Smooth, Linear, Quadratic }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldLandformOperation(string Id, string Purpose, string SourceRuleId,
    WorldLandformMode Mode, IReadOnlyList<WorldPoint> ControlPath, double Radius, double Height,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? DetailProtectionRadius=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] WorldLandformFalloff? Falloff=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? OrientationDegrees=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? AxisStretch=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? InnerRadius=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldTerrainDetail(int Seed, double Wavelength, double Amplitude, int Octaves = 1,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? Persistence = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? Lacunarity = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? OrientationDegrees = null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] double? AxisStretch = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldTerrainRecipe(int SchemaVersion, string Id, string Kind, string Purpose, string SourceRuleId,
    string Unit, WorldPoint Origin, double StepX, double StepY, int Columns, int Rows,
    double BaseElevation, double GradientX, double GradientY, IReadOnlyList<WorldLandformOperation> Operations,
    WorldTerrainDetail? Detail = null, long MaximumWork = 20000000);

/// <summary>Replayable height operations. Coordinate-hashed detail is terrain variation, not object placement.</summary>
public sealed partial class WorldTerrainGenerator
{
    public WorldHeightField Generate(WorldTerrainRecipe recipe, CancellationToken token = default)
    {
        var prepared=Prepare(recipe,token);
        if(prepared.SampleCost*prepared.Recipe.Columns*prepared.Recipe.Rows>prepared.Recipe.MaximumWork)
            throw new ArgumentException("WORLD_TERRAIN_WORK_BUDGET");
        var field=Header(prepared.Recipe);WorldHeightFieldQuery.ValidateHeader(field);
        return field with{Heights=Array.AsReadOnly(GenerateSamples(prepared,0,0,recipe.Columns,recipe.Rows,token))};
    }

    // Used by enclosing bounded operations before they request a full terrain
    // replay. Preparation validates the same recipe and uses the same sample
    // cost as Generate; the terrain's own work limit remains authoritative.
    internal static long FullGenerationWork(WorldTerrainRecipe recipe,CancellationToken token)
    {
        var prepared=Prepare(recipe,token);
        long work=prepared.SampleCost*prepared.Recipe.Columns*prepared.Recipe.Rows;
        if(work>prepared.Recipe.MaximumWork)throw new ArgumentException("WORLD_TERRAIN_WORK_BUDGET");
        WorldHeightFieldQuery.ValidateHeader(Header(prepared.Recipe));
        return work;
    }

    private sealed record PreparedLandform(WorldLandformOperation Source,bool Transform,double Cos,double Sin,double Stretch,IReadOnlyList<WorldPoint> Path);
    private sealed record Prepared(WorldTerrainRecipe Recipe,long SampleCost,IReadOnlyList<PreparedLandform> Operations,IReadOnlyList<PreparedLandform> ProtectedOperations);
    private static Prepared Prepare(WorldTerrainRecipe recipe,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (recipe is null || recipe.SchemaVersion != 1 || recipe.Operations is null || recipe.Operations.Count > 64 ||
            !WorldHeightFieldQuery.Number(recipe.BaseElevation) || !WorldHeightFieldQuery.Number(recipe.GradientX) ||
            !WorldHeightFieldQuery.Number(recipe.GradientY) || recipe.MaximumWork is < 1 or > 200000000 ||
            recipe.Columns is < 2 or > 513 || recipe.Rows is < 2 or > 513)
            throw new ArgumentException("WORLD_TERRAIN_RECIPE_INVALID");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var operations = new List<WorldLandformOperation>();
        long sampleCost = 1;
        foreach (var op in recipe.Operations)
        {
            token.ThrowIfCancellationRequested();
            if (op is null || !SettlementCatalogRegistry.ValidId(op.Id) || !ids.Add(op.Id) ||
                !WorldHeightFieldQuery.Text(op.Purpose) || !WorldHeightFieldQuery.Text(op.SourceRuleId) ||
                !Enum.IsDefined(op.Mode) || op.Falloff is {} falloff&&!Enum.IsDefined(falloff) || op.ControlPath is null || op.ControlPath.Count is < 1 or > 64 ||
                op.OrientationDegrees is {} opOrientation&&(!double.IsFinite(opOrientation)||Math.Abs(opOrientation)>360)||
                op.AxisStretch is {} opStretch&&(!double.IsFinite(opStretch)||opStretch<.125||opStretch>8)||
                op.InnerRadius is {} innerRadius&&(!double.IsFinite(innerRadius)||innerRadius<0||innerRadius>=op.Radius)||
                op.ControlPath.Any(p => !WorldHeightFieldQuery.Point(p)) || !double.IsFinite(op.Radius) || op.Radius < .001 || op.Radius > 10000000 ||
                !WorldHeightFieldQuery.Number(op.Height) || op.ControlPath.Zip(op.ControlPath.Skip(1)).Any(p => p.First.DistanceTo(p.Second) < .001))
                throw new ArgumentException("WORLD_TERRAIN_OPERATION_INVALID");
            operations.Add(op with { ControlPath = Array.AsReadOnly(op.ControlPath.ToArray()) });
            sampleCost += Math.Max(1, op.ControlPath.Count - 1);
            if(op.InnerRadius is >0)sampleCost++;
            bool transformed=((op.OrientationDegrees??0)%360)!=0||(op.AxisStretch??1)!=1;
            if(transformed)sampleCost+=5;
            if(op.DetailProtectionRadius is {} protection)
            {
                if(!double.IsFinite(protection)||protection<=0||protection>=op.Radius||recipe.Detail is null)
                    throw new ArgumentException("WORLD_TERRAIN_DETAIL_PROTECTION_INVALID");
                sampleCost+=Math.Max(1,op.ControlPath.Count-1);
                if(transformed)sampleCost+=5;
            }
        }
        if (recipe.Detail is {} detail)
        {
            if (!double.IsFinite(detail.Wavelength) || detail.Wavelength < .001 || detail.Wavelength > 10000000 ||
                !double.IsFinite(detail.Amplitude) || detail.Amplitude < 0 || detail.Amplitude > 10000000 || detail.Octaves is < 1 or > 8 ||
                detail.Persistence is {} persistence&&(!double.IsFinite(persistence)||persistence<0||persistence>1)||
                detail.Lacunarity is {} lacunarity&&(!double.IsFinite(lacunarity)||lacunarity<1||lacunarity>8)||
                detail.OrientationDegrees is {} orientation&&(!double.IsFinite(orientation)||Math.Abs(orientation)>360)||
                detail.AxisStretch is {} stretch&&(!double.IsFinite(stretch)||stretch<.125||stretch>8)||
                detail.Wavelength * Math.Min(1,detail.AxisStretch??1) / Math.Pow(detail.Lacunarity??2, detail.Octaves - 1) < .001)
                throw new ArgumentException("WORLD_TERRAIN_DETAIL_INVALID");
            sampleCost += detail.Octaves * 4;
            if((detail.OrientationDegrees??0)!=0||(detail.AxisStretch??1)!=1)sampleCost+=5;
        }
        var prepared=operations.Select(op=>{
            double angle=((op.OrientationDegrees??0)%360)*Math.PI/180,stretch=op.AxisStretch??1;
            bool transform=angle!=0||stretch!=1;double c=transform?Math.Cos(angle):1,s=transform?Math.Sin(angle):0;
            var origin=op.ControlPath[0];
            WorldPoint Transform(WorldPoint p){double x=p.X-origin.X,y=p.Y-origin.Y;return new(x*c+y*s,(-x*s+y*c)/stretch);}
            return new PreparedLandform(op,transform,c,s,stretch,transform?Array.AsReadOnly(op.ControlPath.Select(Transform).ToArray()):op.ControlPath);
        }).ToArray();
        return new(recipe with{Operations=operations.AsReadOnly()},sampleCost,prepared,prepared.Where(o=>o.Source.DetailProtectionRadius is not null).ToArray());
    }

    internal static WorldHeightField Header(WorldTerrainRecipe recipe)=>new(1,recipe.Id,recipe.Kind,recipe.Purpose,recipe.SourceRuleId,
        recipe.Unit,recipe.Origin,recipe.StepX,recipe.StepY,recipe.Columns,recipe.Rows,Array.Empty<double>());

    private static double[] GenerateSamples(Prepared prepared,int firstColumn,int firstRow,int columns,int rows,CancellationToken token)
    {
        var recipe=prepared.Recipe;var values=new double[columns*rows];
        double orientation=(recipe.Detail?.OrientationDegrees??0)*Math.PI/180,stretch=recipe.Detail?.AxisStretch??1;
        bool transform=orientation!=0||stretch!=1;double cosine=transform?Math.Cos(orientation):1,sine=transform?Math.Sin(orientation):0;
        for (int localY = 0; localY < rows; localY++) for (int localX = 0; localX < columns; localX++)
        {
            token.ThrowIfCancellationRequested();
            int x=firstColumn+localX,y=firstRow+localY;
            var p = new WorldPoint(recipe.Origin.X + x * recipe.StepX, recipe.Origin.Y + y * recipe.StepY);
            double height = recipe.BaseElevation + x * recipe.StepX * recipe.GradientX + y * recipe.StepY * recipe.GradientY;
            if (recipe.Detail is {} noise)
            {
                double nx=transform?p.X*cosine+p.Y*sine:p.X;
                double ny=transform?(-p.X*sine+p.Y*cosine)/stretch:p.Y;
                double sum = 0, weight = 0, amplitude = 1, wavelength = noise.Wavelength;
                for (int octave = 0; octave < noise.Octaves; octave++)
                {
                    sum += Noise(nx / wavelength, ny / wavelength, unchecked(noise.Seed + octave * 7919)) * amplitude;
                    weight += amplitude; amplitude *= noise.Persistence??.5; wavelength *= 1/(noise.Lacunarity??2);
                }
                double protection=1;
                foreach(var op in prepared.ProtectedOperations)
                {
                    if(op.Source.DetailProtectionRadius is not {} inner)continue;
                    double distance=LandformDistance(p,op);
                    double t=Math.Clamp((distance-inner)/(op.Source.Radius-inner),0,1);
                    protection=Math.Min(protection,t*t*(3-2*t));
                }
                height += sum / weight * noise.Amplitude * protection;
            }
            foreach (var preparedOp in prepared.Operations)
            {
                var op=preparedOp.Source;double distance=LandformDistance(p,preparedOp);
                double t = op.InnerRadius is >0 and var inner
                    ? Math.Clamp((op.Radius-distance)/(op.Radius-inner),0,1)
                    : Math.Clamp(1-distance/op.Radius,0,1);
                double influence=op.Falloff switch {WorldLandformFalloff.Linear=>t,WorldLandformFalloff.Quadratic=>t*t,_=>t*t*(3-2*t)};
                height += (op.Mode == WorldLandformMode.Add ? op.Height : op.Height - height) * influence;
            }
            if (!WorldHeightFieldQuery.Number(height)) throw new ArgumentException("WORLD_TERRAIN_HEIGHT_INVALID");
            values[localY * columns + localX] = height;
        }
        return values;
    }

    private static double LandformDistance(WorldPoint point,PreparedLandform op)
    {
        if(op.Transform){double x=point.X-op.Source.ControlPath[0].X,y=point.Y-op.Source.ControlPath[0].Y;
            point=new(x*op.Cos+y*op.Sin,(-x*op.Sin+y*op.Cos)/op.Stretch);}
        double distance=point.DistanceTo(op.Path[0]);
        for(int i=1;i<op.Path.Count;i++)distance=Math.Min(distance,Distance(point,op.Path[i-1],op.Path[i]));
        return distance;
    }

    private static double Distance(WorldPoint p, WorldPoint a, WorldPoint b)
    {
        double dx = b.X-a.X, dy = b.Y-a.Y, t = Math.Clamp(((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy), 0, 1);
        return p.DistanceTo(new(a.X+t*dx,a.Y+t*dy));
    }

    // Fixed integer mixer and quintic interpolation; no platform Random/hash-code dependency.
    private static double Noise(double x, double y, int seed)
    {
        long ix = (long)Math.Floor(x), iy = (long)Math.Floor(y);
        static double Smooth(double t) => t*t*t*(t*(t*6-15)+10);
        static double Mix(long x, long y, int seed)
        {
            unchecked
            {
                ulong n = (ulong)x * 0x9E3779B185EBCA87UL ^ (ulong)y * 0xC2B2AE3D27D4EB4FUL ^ (uint)seed;
                n ^= n >> 30; n *= 0xBF58476D1CE4E5B9UL; n ^= n >> 27; n *= 0x94D049BB133111EBUL; n ^= n >> 31;
                return (n >> 11) * (1.0 / 9007199254740992.0) * 2 - 1;
            }
        }
        double u=Smooth(x-ix),v=Smooth(y-iy),a=Mix(ix,iy,seed),b=Mix(ix+1,iy,seed),c=Mix(ix,iy+1,seed),d=Mix(ix+1,iy+1,seed);
        return (a+(b-a)*u)*(1-v)+(c+(d-c)*u)*v;
    }
}
