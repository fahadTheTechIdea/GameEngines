using System.Text.Json;
namespace WorldCreator.Core;
/// <summary>Exports lossless world JSON and its exact SVG plan for downstream consumers.</summary>
public sealed class WorldDocumentExporter(IWorldPlanRenderer? planRenderer=null) : IWorldExporter
{
    public async Task ExportAsync(WorldDocument world, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var json = JsonSerializer.Serialize(world, new JsonSerializerOptions { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } });
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "world.json"), json, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "plan.svg"), (planRenderer ?? new WorldPlanSvgRenderer()).Render(world,cancellationToken),cancellationToken);
    }
}
