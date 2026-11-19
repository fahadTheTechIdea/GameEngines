using System.Text.Json;
using System.Text.Json.Serialization;
namespace WorldCreator.Core.Metadata;

internal static class MetadataLayoutInput
{
    internal static T Read<T>(JsonElement value,int dimensions,int count,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(dimensions is <1 or >8||count is <0 or >100000||value.ValueKind!=JsonValueKind.Object)
            throw new ArgumentException("METADATA_LAYOUT_INPUT_INVALID");
        try
        {
            var options=WorldJson.CreateOptions();options.RespectRequiredConstructorParameters=true;
            options.UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow;
            return value.Deserialize<T>(options)??throw new ArgumentException("METADATA_LAYOUT_PARAMETERS_REQUIRED");
        }
        catch(JsonException ex){throw new ArgumentException("METADATA_LAYOUT_PARAMETERS_INVALID",ex);}
    }
    internal static bool Vector(IReadOnlyList<double>? value,int dimensions)=>
        value is not null&&value.Count==dimensions&&value.All(double.IsFinite);
}
