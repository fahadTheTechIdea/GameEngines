using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

internal sealed record MetadataJsonPart(string Sha256,int Bytes);
internal sealed record MetadataJsonManifest(string StorageFormat,int Version,string DocumentSha256,long DocumentBytes,IReadOnlyList<MetadataJsonPart> Parts);

/// <summary>One bounded JSON transport. Parts contain the complete serialized UTF8
/// stream, including source history; no geometry cache or domain-specific projection.</summary>
internal static class MetadataJsonStorage
{
    internal const int MaximumFileBytes=16777216,InlineBytes=8388608,PartBytes=1048576;
    internal const long MaximumDocumentBytes=268435456;
    internal const string Format="worldcreator-json-chunks",PartDirectory="_worlddata";

    internal static long Limit(Type type)=>type==typeof(WorldMetadataDocument)||type==typeof(MetadataSessionCheckpoint)||
        type==typeof(MetadataWorldDomainDocument)||type==typeof(MetadataRegionalWorldDocument)||type==typeof(MetadataRegionalDomainWorldDocument)||type==typeof(MetadataRegionalSessionCheckpoint)||type==typeof(MetadataRegionalSessionSnapshot)||type==typeof(MetadataDomainSessionCheckpoint)||type==typeof(MetadataStudyDefinition)||type==typeof(MetadataPopulationSelectionRequest)||type==typeof(WorldDrainagePlan)||
        type==typeof(WorldWaterPlan)||type==typeof(WorldWindMoisturePlan)||type==typeof(MetadataWindMoisturePlan)||type==typeof(MetadataChunkPlan)
            ?MaximumDocumentBytes:MaximumFileBytes;
    internal static JsonSerializerOptions Options(bool indented=false)
    {
        var options=WorldJson.CreateOptions(indented);options.MaxDepth=32;
        options.RespectRequiredConstructorParameters=true;options.UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow;
        return options;
    }
    internal static bool Hash(string? value)=>value is {Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static string Digest(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void NoLink(string path)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("METADATA_STORAGE_LINK_REFUSED");
    }
    internal static void Validate(MetadataJsonManifest manifest,long maximum)
    {
        if(manifest is null||manifest.StorageFormat!=Format||manifest.Version!=1||!Hash(manifest.DocumentSha256)||
            manifest.DocumentBytes<=InlineBytes||manifest.DocumentBytes>maximum||manifest.DocumentBytes>MaximumDocumentBytes||
            manifest.Parts is null||manifest.Parts.Count!=(manifest.DocumentBytes+PartBytes-1)/PartBytes||manifest.Parts.Count>256)
            throw new ArgumentException("METADATA_STORAGE_MANIFEST_INVALID");
        for(int i=0;i<manifest.Parts.Count;i++)
        {
            var part=manifest.Parts[i];long expected=i==manifest.Parts.Count-1?manifest.DocumentBytes-(long)i*PartBytes:PartBytes;
            if(part is null||!Hash(part.Sha256)||part.Bytes!=expected)throw new ArgumentException("METADATA_STORAGE_PART_DEFINITION_INVALID");
        }
    }
    internal static async Task<Stream> OpenReadAsync(string file,long maximum,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var stream=File.OpenRead(file);
        try
        {
            if(stream.Length is <=0 or >MaximumFileBytes)throw new ArgumentException("METADATA_INPUT_SIZE_LIMIT");
            using var json=await JsonDocument.ParseAsync(stream,new(){MaxDepth=32,AllowDuplicateProperties=false},token);
            if(json.RootElement.ValueKind!=JsonValueKind.Object||!json.RootElement.TryGetProperty("StorageFormat",out _))
            {stream.Position=0;return stream;}
            var manifest=json.RootElement.Deserialize<MetadataJsonManifest>(Options())??throw new ArgumentException("METADATA_STORAGE_MANIFEST_REQUIRED");
            Validate(manifest,maximum);
            var directory=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file))!,PartDirectory);NoLink(directory);
            stream.Dispose();return new PartReader(directory,manifest,token);
        }
        catch{stream.Dispose();throw;}
    }

    internal static async Task CompleteReadAsync(Stream stream,CancellationToken token)
    {
        if(stream is PartReader parts)
        {
            byte[] scratch=new byte[4096];
            while(await parts.ReadAsync(scratch,token)>0){}
            parts.Verified();
        }
    }

    internal static async Task<MetadataJsonManifest?> WriteAsync<T>(string file,T value,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(File.Exists(file)||Directory.Exists(file))throw new IOException("METADATA_STORAGE_OUTPUT_EXISTS");
        await using var stream=new PartWriter(file,Limit(typeof(T)),token);
        await JsonSerializer.SerializeAsync(stream,value,Options(),token);
        return await stream.CompleteAsync();
    }

    internal static async Task<byte[]> InlineAsync<T>(T value,CancellationToken token)
    {
        await using var stream=new InlineBuffer();
        await JsonSerializer.SerializeAsync(stream,value,Options(),token);return stream.ToArray();
    }
    internal static async Task<T> SnapshotAsync<T>(T value,CancellationToken token)
    {
        await using var stream=new InlineBuffer(Limit(typeof(T)));
        await JsonSerializer.SerializeAsync(stream,value,Options(),token);stream.Position=0;
        return await JsonSerializer.DeserializeAsync<T>(stream,Options(),token)??throw new ArgumentException("METADATA_INPUT_REQUIRED");
    }
    internal static T Snapshot<T>(T value)
    {
        using var stream=new InlineBuffer(Limit(typeof(T)));
        JsonSerializer.Serialize(stream,value,Options());stream.Position=0;
        return JsonSerializer.Deserialize<T>(stream,Options())??throw new ArgumentException("METADATA_INPUT_REQUIRED");
    }
    internal static void ValidateSize<T>(T value,CancellationToken token=default)
    {
        using var stream=new SizeCounter(Limit(typeof(T)),token);
        JsonSerializer.Serialize(stream,value,Options());
    }
    private sealed class SizeCounter(long maximum,CancellationToken token):Stream
    {
        private long length;
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>length;public override long Position{get=>length;set=>throw new NotSupportedException();}
        public override void Flush(){}
        public override void Write(byte[] buffer,int offset,int count)=>Write(buffer.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {token.ThrowIfCancellationRequested();if(buffer.Length>maximum-length)throw new ArgumentException("METADATA_STORAGE_SNAPSHOT_LIMIT");length+=buffer.Length;}
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
    }
    private sealed class InlineBuffer(long maximum=InlineBytes):MemoryStream
    {
        private void Guard(int length)
        {if(length>maximum-Length)throw new ArgumentException(maximum==InlineBytes?"METADATA_STORAGE_INLINE_LIMIT":"METADATA_STORAGE_SNAPSHOT_LIMIT");}
        public override void Write(byte[] b,int o,int c){Guard(c);base.Write(b,o,c);}
        public override void Write(ReadOnlySpan<byte> bytes){Guard(bytes.Length);base.Write(bytes);}
        public override Task WriteAsync(byte[] b,int o,int c,CancellationToken token)
        {token.ThrowIfCancellationRequested();Guard(c);return base.WriteAsync(b,o,c,token);}
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken token=default)
        {token.ThrowIfCancellationRequested();Guard(bytes.Length);return base.WriteAsync(bytes,token);}
    }

    private sealed class PartWriter(string file,long maximum,CancellationToken token):Stream
    {
        private readonly MemoryStream small=new();
        private readonly byte[] buffer=new byte[PartBytes];
        private readonly IncrementalHash digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private readonly List<MetadataJsonPart> parts=[];
        private int used;
        private long total;
        private bool partitioned,complete;
        private string DirectoryPath=>Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file))!,PartDirectory);
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>!complete;
        public override long Length=>total;public override long Position{get=>total;set=>throw new NotSupportedException();}
        public override void Flush(){}public override Task FlushAsync(CancellationToken cancellationToken)=>Task.CompletedTask;
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();
        public override void Write(byte[] b,int o,int c)=>WriteAsync(b.AsMemory(o,c),token).AsTask().GetAwaiter().GetResult();
        public override Task WriteAsync(byte[] b,int o,int c,CancellationToken cancellationToken)=>WriteAsync(b.AsMemory(o,c),cancellationToken).AsTask();
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken cancellationToken=default)
        {
            token.ThrowIfCancellationRequested();cancellationToken.ThrowIfCancellationRequested();
            if(complete)throw new InvalidOperationException("METADATA_STORAGE_WRITER_COMPLETE");
            if(bytes.Length>maximum-total)throw new ArgumentException("METADATA_STORAGE_DOCUMENT_SIZE_LIMIT");
            digest.AppendData(bytes.Span);total+=bytes.Length;
            if(!partitioned&&total<=InlineBytes){await small.WriteAsync(bytes,token);return;}
            if(!partitioned)
            {
                partitioned=true;Directory.CreateDirectory(DirectoryPath);NoLink(DirectoryPath);
                await StoreAsync(small.GetBuffer().AsMemory(0,(int)small.Length));small.SetLength(0);
            }
            await StoreAsync(bytes);
        }
        private async Task StoreAsync(ReadOnlyMemory<byte> bytes)
        {
            while(bytes.Length>0)
            {
                token.ThrowIfCancellationRequested();int count=Math.Min(buffer.Length-used,bytes.Length);
                bytes[..count].CopyTo(buffer.AsMemory(used));used+=count;bytes=bytes[count..];
                if(used==buffer.Length)await PartAsync();
            }
        }
        private async Task PartAsync()
        {
            token.ThrowIfCancellationRequested();if(parts.Count>=256)throw new ArgumentException("METADATA_STORAGE_PART_COUNT_LIMIT");
            string hash=Digest(buffer.AsSpan(0,used)),path=Path.Combine(DirectoryPath,hash+".bin");
            if(File.Exists(path))
            {
                NoLink(path);await using var existing=File.OpenRead(path);
                if(existing.Length!=used||Convert.ToHexStringLower(await SHA256.HashDataAsync(existing,token))!=hash)
                    throw new ArgumentException("METADATA_STORAGE_EXISTING_PART_MISMATCH");
            }
            else
            {
                await using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true);
                await output.WriteAsync(buffer.AsMemory(0,used),token);
            }
            parts.Add(new(hash,used));used=0;
        }
        internal async Task<MetadataJsonManifest?> CompleteAsync()
        {
            token.ThrowIfCancellationRequested();if(complete)throw new InvalidOperationException("METADATA_STORAGE_WRITER_COMPLETE");
            MetadataJsonManifest? manifest=null;
            if(partitioned)
            {
                if(used>0)await PartAsync();
                manifest=new(Format,1,Convert.ToHexStringLower(digest.GetHashAndReset()),total,parts.AsReadOnly());Validate(manifest,maximum);
            }
            byte[] bytes=manifest is null?small.ToArray():JsonSerializer.SerializeToUtf8Bytes(manifest,Options(true));
            if(bytes.Length is <=0 or >MaximumFileBytes)throw new ArgumentException("METADATA_STORAGE_ROOT_SIZE_LIMIT");
            await using var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true);
            await output.WriteAsync(bytes,token);complete=true;return manifest;
        }
        protected override void Dispose(bool disposing){if(disposing){small.Dispose();digest.Dispose();}base.Dispose(disposing);}
    }

    private sealed class PartReader(string directory,MetadataJsonManifest manifest,CancellationToken token):Stream
    {
        private readonly IncrementalHash digest=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private byte[] buffer=[];private int position,index;private long total;private bool verified;
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>manifest.DocumentBytes;public override long Position{get=>total;set=>throw new NotSupportedException();}
        public override void Flush(){}public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long l)=>throw new NotSupportedException();
        public override int Read(byte[] b,int o,int c)=>ReadAsync(b.AsMemory(o,c),token).AsTask().GetAwaiter().GetResult();
        public override Task<int> ReadAsync(byte[] b,int o,int c,CancellationToken cancellationToken)=>ReadAsync(b.AsMemory(o,c),cancellationToken).AsTask();
        public override async ValueTask<int> ReadAsync(Memory<byte> target,CancellationToken cancellationToken=default)
        {
            token.ThrowIfCancellationRequested();cancellationToken.ThrowIfCancellationRequested();if(target.Length==0)return 0;
            if(position==buffer.Length)
            {
                if(index==manifest.Parts.Count){Verified();return 0;}
                NoLink(directory);var part=manifest.Parts[index++];string path=Path.Combine(directory,part.Sha256+".bin");NoLink(path);
                await using var input=File.OpenRead(path);
                if(input.Length!=part.Bytes)throw new ArgumentException("METADATA_STORAGE_PART_SIZE_MISMATCH");
                buffer=new byte[part.Bytes];await input.ReadExactlyAsync(buffer,token);
                if(input.Length!=part.Bytes||Digest(buffer)!=part.Sha256)throw new ArgumentException("METADATA_STORAGE_PART_HASH_MISMATCH");
                position=0;
            }
            int count=Math.Min(target.Length,buffer.Length-position);buffer.AsMemory(position,count).CopyTo(target);
            digest.AppendData(buffer.AsSpan(position,count));position+=count;total+=count;return count;
        }
        internal void Verified()
        {
            if(verified)return;
            if(index!=manifest.Parts.Count||position!=buffer.Length||total!=manifest.DocumentBytes||
                Convert.ToHexStringLower(digest.GetHashAndReset())!=manifest.DocumentSha256)
                throw new ArgumentException("METADATA_STORAGE_DOCUMENT_HASH_MISMATCH");
            verified=true;
        }
        protected override void Dispose(bool disposing){if(disposing)digest.Dispose();base.Dispose(disposing);}
    }
}
