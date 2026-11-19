using System.Security.Cryptography;
using System.Text.Json;

namespace WorldCreator.Core;

/// <summary>Opt-in for deterministic extension code used by incremental generation.
/// The digest must cover all configuration/state that can affect results, excluding
/// explicit method inputs. Keep it stable throughout a generation operation. Code
/// identity is measured separately; a label/version number alone is insufficient.</summary>
public interface IWorldGenerationDependency
{
    string ConfigurationSha256 {get;}
}

public sealed record WorldGenerationImplementationIdentity(string Type,string Assembly,string ModuleId,string ConfigurationSha256);
public static class WorldGenerationDependencyIdentity
{
    public static string EmptyConfigurationSha256 {get;}=Digest<object?>(null);
    public static string Digest<T>(T configuration)=>Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(configuration,WorldJson.CreateOptions())));
    public static WorldGenerationImplementationIdentity Describe(object implementation)
    {
        if(implementation is not IWorldGenerationDependency versioned)
            throw new ArgumentException("METADATA_INCREMENTAL_EXTERNAL_IMPLEMENTATION_UNVERSIONED:"+implementation.GetType().FullName);
        string digest=versioned.ConfigurationSha256;
        if(digest is null||digest.Length!=64||digest.Any(c=>!char.IsAsciiHexDigit(c))||digest!=digest.ToLowerInvariant())
            throw new ArgumentException("METADATA_GENERATION_DEPENDENCY_DIGEST_INVALID");
        var type=implementation.GetType();
        return new(type.FullName!,type.Assembly.FullName!,type.Module.ModuleVersionId.ToString("D"),digest);
    }
}
