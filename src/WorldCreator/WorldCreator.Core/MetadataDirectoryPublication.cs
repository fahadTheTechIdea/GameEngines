using System.Text.Json;

[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("WorldCreator.Tests")]

namespace WorldCreator.Core.Metadata;

internal sealed record MetadataPublicationFailure(int Attempt,int Win32Code);
internal sealed record MetadataPublicationReceipt(int Version,int CommitAttempt,IReadOnlyList<MetadataPublicationFailure> EarlierFailures);

/// <summary>Commits an already complete owned staging directory. Retries only verified Windows rename conflicts.</summary>
internal static class MetadataDirectoryPublication
{
    internal static async Task CommitAsync(string staging,string target,Action<CancellationToken> validate,CancellationToken token)
    {
        var failures=new List<MetadataPublicationFailure>();int[] delays=[50,150,350];
        async Task Record(int attempt)=>await File.WriteAllTextAsync(Path.Combine(staging,"publication.json"),
            JsonSerializer.Serialize(new MetadataPublicationReceipt(1,attempt,failures),WorldJson.CreateOptions(true)),token);
        await Record(1);
        for(int attempt=1;;attempt++)
        {
            token.ThrowIfCancellationRequested();validate(token);token.ThrowIfCancellationRequested();
            try{Directory.Move(staging,target);return;}
            catch(IOException error)when(OperatingSystem.IsWindows()&&(error.HResult&0xffff) is 5 or 32&&
                Directory.Exists(staging)&&!Directory.Exists(target)&&!File.Exists(target))
            {
                failures.Add(new(attempt,error.HResult&0xffff));
                if(attempt>delays.Length)throw new IOException("METADATA_PUBLICATION_RENAME_RETRY_EXHAUSTED:"+
                    JsonSerializer.Serialize(failures,WorldJson.CreateOptions()),error);
                // Record privately before waiting: no extra writes immediately precede the next rename.
                await Record(attempt+1);await Task.Delay(delays[attempt-1],token);
            }
        }
    }

    internal static void Cleanup(string staging,string parent,string prefix,Exception? publicationFailure)
    {
        if(!Directory.Exists(staging))return;
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(!string.Equals(Path.GetDirectoryName(Path.GetFullPath(staging)),Path.GetFullPath(parent),comparison)||
            !Path.GetFileName(staging).StartsWith(prefix,StringComparison.Ordinal))
            throw new IOException("METADATA_PUBLICATION_CLEANUP_PATH_INVALID");
        try{Directory.Delete(staging,true);}
        catch(Exception cleanup)when(publicationFailure is not null&&cleanup is IOException or UnauthorizedAccessException)
        {
            throw new IOException("METADATA_PUBLICATION_AND_CLEANUP_FAILED:staging="+staging+":publication="+publicationFailure.Message+
                ":cleanup="+cleanup.Message,new AggregateException(publicationFailure,cleanup));
        }
    }
}
