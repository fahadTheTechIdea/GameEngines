using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Planning;

namespace WorldCreator.Core.Spatial;

public enum WorldFieldInput { Elevation, Slope, X, Y, Field, SampledField }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldFieldTerm(WorldFieldInput Input, double Coefficient, string? FieldId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldEnvironmentalField(string Id, string Purpose, string SourceRuleId, string Unit,
    double Offset, IReadOnlyList<WorldFieldTerm> Terms, double? Minimum = null, double? Maximum = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorldEnvironmentDefinition(int SchemaVersion, string Id, IReadOnlyList<WorldEnvironmentalField> Fields,
    IReadOnlyList<WorldSampledScalarField>? SampledFields=null);
public sealed record WorldEnvironmentSample(WorldSurfaceSample Surface, IReadOnlyDictionary<string,double> Values);

/// <summary>Game-authored derived scalar fields. Unknown terrain is unavailable, never zero or suitable.</summary>
public sealed class WorldEnvironmentQuery
{
    private readonly WorldSurfaceQuery surface;
    private readonly IReadOnlyList<WorldEnvironmentalField> ordered;
    private readonly Dictionary<string,WorldSampledScalarQuery> sampled=new(StringComparer.Ordinal);
    internal WorldSurfaceQuery Surface => surface;
    internal IReadOnlyList<WorldEnvironmentalField> OrderedFields => ordered;
    public WorldEnvironmentDefinition Definition { get; }
    public IReadOnlyList<string> EvaluationOrder { get; }
    public WorldEnvironmentQuery(WorldSurfaceQuery surface, WorldEnvironmentDefinition definition, CancellationToken token = default)
    {
        this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
        token.ThrowIfCancellationRequested();
        if (definition is null || definition.SchemaVersion != 1 || !Id(definition.Id) || definition.Fields is null || definition.Fields.Count is <1 or >32)
            throw new ArgumentException("WORLD_ENVIRONMENT_DEFINITION_INVALID");
        if(definition.SampledFields is {} scalarFields)
        {
            if(scalarFields.Count>8||scalarFields.Any(f=>f is null||f.Values is null)||scalarFields.Sum(f=>(long)f.Values.Count)>2105352)
                throw new ArgumentException("WORLD_ENVIRONMENT_SCALAR_BUDGET");
            foreach(var scalar in scalarFields)
            {
                var terrain=surface.HeightField?.Geometry??throw new ArgumentException("WORLD_ENVIRONMENT_SCALAR_TERRAIN_REQUIRED");
                var query=new WorldSampledScalarQuery(scalar,terrain,token);
                if(!sampled.TryAdd(query.Definition.Id,query))throw new ArgumentException("WORLD_ENVIRONMENT_SCALAR_DUPLICATE");
            }
        }
        var fields = new SortedDictionary<string,WorldEnvironmentalField>(StringComparer.Ordinal);
        int terms = 0;
        foreach (var field in definition.Fields)
        {
            token.ThrowIfCancellationRequested();
            if (field is null || !Id(field.Id) || fields.ContainsKey(field.Id) || !Id(field.Unit) ||
                !Text(field.Purpose) || !Text(field.SourceRuleId) || !Number(field.Offset) || field.Terms is null || field.Terms.Count >32 ||
                (terms += field.Terms.Count)>256 || field.Minimum is {} min && !Number(min) || field.Maximum is {} max && !Number(max) || field.Minimum>field.Maximum)
                throw new ArgumentException("WORLD_ENVIRONMENT_FIELD_INVALID");
            foreach (var term in field.Terms)
                if (term is null || !Enum.IsDefined(term.Input) || !Number(term.Coefficient) ||
                    (term.Input is WorldFieldInput.Field or WorldFieldInput.SampledField ? !Id(term.FieldId) : term.FieldId is not null))
                    throw new ArgumentException("WORLD_ENVIRONMENT_TERM_INVALID");
            foreach(var term in field.Terms.Where(t=>t.Input==WorldFieldInput.SampledField))
                if(!sampled.ContainsKey(term.FieldId!))throw new ArgumentException("WORLD_ENVIRONMENT_SCALAR_UNKNOWN:"+term.FieldId);
            fields.Add(field.Id,field with {Terms=Array.AsReadOnly(field.Terms.ToArray())});
        }
        var state = new Dictionary<string,int>(StringComparer.Ordinal);var sorted = new List<WorldEnvironmentalField>();
        void Visit(string id)
        {
            token.ThrowIfCancellationRequested();
            if (!fields.TryGetValue(id,out var field)) throw new ArgumentException("WORLD_ENVIRONMENT_FIELD_UNKNOWN:"+id);
            if (state.TryGetValue(id,out int mark)) {if(mark==1)throw new ArgumentException("WORLD_ENVIRONMENT_CYCLE:"+id);return;}
            state[id]=1;
            foreach (string dependency in field.Terms.Where(t=>t.Input==WorldFieldInput.Field).Select(t=>t.FieldId!).Distinct().Order(StringComparer.Ordinal)) Visit(dependency);
            state[id]=2;sorted.Add(field);
        }
        foreach (string id in fields.Keys) Visit(id);
        ordered=sorted.AsReadOnly();EvaluationOrder=Array.AsReadOnly(sorted.Select(f=>f.Id).ToArray());
        Definition=definition with{Fields=Array.AsReadOnly(fields.Values.ToArray()),SampledFields=definition.SampledFields is null?null:
            Array.AsReadOnly(sampled.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>p.Value.Definition).ToArray())};
    }
    public WorldEnvironmentSample Sample(WorldPoint point,CancellationToken token=default)
    {
        var terrain=surface.Sample(point,token);var values=new SortedDictionary<string,double>(StringComparer.Ordinal);
        if (terrain.Status==WorldSurfaceStatus.Sampled)
            foreach(var field in ordered)
            {
                token.ThrowIfCancellationRequested();double value=field.Offset;
                foreach(var term in field.Terms)
                {
                    token.ThrowIfCancellationRequested();
                    double input=term.Input switch {WorldFieldInput.Elevation=>terrain.Elevation!.Value,WorldFieldInput.Slope=>terrain.Slope!.Value,
                        WorldFieldInput.X=>point.X,WorldFieldInput.Y=>point.Y,WorldFieldInput.Field=>values[term.FieldId!],
                        WorldFieldInput.SampledField=>SampleScalar(term.FieldId!,point,token).Value,_=>throw new ArgumentException("WORLD_ENVIRONMENT_INPUT_INVALID")};
                    value+=input*term.Coefficient;
                    if(!double.IsFinite(value)||Math.Abs(value)>1e100)throw new ArgumentException("WORLD_ENVIRONMENT_VALUE_OVERFLOW:"+field.Id);
                }
                if(field.Minimum is {} min)value=Math.Max(value,min);
                if(field.Maximum is {} max)value=Math.Min(value,max);
                if(!Number(value))throw new ArgumentException("WORLD_ENVIRONMENT_VALUE_RANGE:"+field.Id);
                values.Add(field.Id,value);
            }
        return new(terrain,new ReadOnlyDictionary<string,double>(values));
    }
    internal WorldScalarSample SampleScalar(string id,WorldPoint point,CancellationToken token=default)=>sampled[id].Sample(point,token);
    internal static bool Id(string? s)=>SettlementCatalogRegistry.ValidId(s);
    internal static bool Text(string? s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=2048;
    internal static bool Number(double x)=>double.IsFinite(x)&&Math.Abs(x)<=1000000000;
}
