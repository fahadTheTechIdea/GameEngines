using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core;
using WorldCreator.Core.Operations;

namespace WorldCreator.Cli;

internal sealed record CanonicalGameArtsConfiguration(int Version,string FrameworkRoot,string PythonExecutable,
    string ProjectRoot,IReadOnlyDictionary<string,string> Revisions,int TimeoutSeconds=30,int MaximumOutputCharacters=1048576);

/// <summary>Loads explicit consumer configuration; never starts a setup wizard.</summary>
internal sealed record CanonicalGameArtsContext(string AssetRoot,GameArtsLifecycleRegistry Assets,GameArtsLifecycleRegistry Mockups)
{
    public static async Task<CanonicalGameArtsContext> LoadAsync(string file,WorldOperationReporter reporter,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(new FileInfo(file).Length is <=0 or >65536)throw new ArgumentException("GAMEARTS_CONSUMER_CONFIG_SIZE");
        var json=WorldJson.CreateOptions();json.UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow;
        string text=await File.ReadAllTextAsync(file,token);
        using(var document=JsonDocument.Parse(text))ValidateProperties(document.RootElement);
        var config=JsonSerializer.Deserialize<CanonicalGameArtsConfiguration>(text,json)
            ??throw new ArgumentException("GAMEARTS_CONSUMER_CONFIG_MISSING");
        if(config.Version!=1)throw new ArgumentException("GAMEARTS_CONSUMER_CONFIG_VERSION");
        string directory=Path.GetDirectoryName(Path.GetFullPath(file))!;
        string Resolve(string value)=>string.IsNullOrWhiteSpace(value)?throw new ArgumentException("GAMEARTS_CONSUMER_CONFIG_PATH_REQUIRED"):
            Path.GetFullPath(value,directory);
        var options=new GameArtsLifecycleOptions(Resolve(config.FrameworkRoot),Resolve(config.PythonExecutable),Resolve(config.ProjectRoot),
            GameArtsLifecycleKind.Assets,config.Revisions,config.TimeoutSeconds,config.MaximumOutputCharacters);
        return new(options.ProjectRoot,new(options,reporter),new(options with{Kind=GameArtsLifecycleKind.Mockups},reporter));
    }

    private static void ValidateProperties(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object)
        {
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var property in value.EnumerateObject())
            {
                if(!names.Add(property.Name))throw new ArgumentException("GAMEARTS_CONSUMER_CONFIG_DUPLICATE_PROPERTY");
                ValidateProperties(property.Value);
            }
        }
        else if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())ValidateProperties(item);
    }
}
