using System.Globalization;
using System.Text.Json;

namespace WorldCreator.Core.Metadata;

/// <summary>One bounded JSON-pointer owner for local and regional input protections.</summary>
internal static class MetadataInputValues
{
    internal static JsonElement Element<T>(T value)=>JsonSerializer.SerializeToElement(value,WorldJson.CreateOptions());
    internal static JsonElement? Resolve(JsonElement root,string pointer)
    {
        if(pointer is null||pointer.Length>2048||pointer.Length>0&&!pointer.StartsWith('/'))
            throw new ArgumentException("METADATA_PROTECTED_POINTER_INVALID");
        if(pointer.Length==0)return root;
        var parts=pointer[1..].Split('/');
        if(parts.Length>32)throw new ArgumentException("METADATA_PROTECTED_POINTER_INVALID");
        foreach(string encoded in parts)
        {
            for(int i=0;i<encoded.Length;i++)if(encoded[i]=='~'&&(++i>=encoded.Length||encoded[i] is not ('0' or '1')))
                throw new ArgumentException("METADATA_PROTECTED_POINTER_INVALID");
            string part=encoded.Replace("~1","/",StringComparison.Ordinal).Replace("~0","~",StringComparison.Ordinal);
            if(root.ValueKind==JsonValueKind.Object)
            {if(!root.TryGetProperty(part,out root))return null;}
            else if(root.ValueKind==JsonValueKind.Array)
            {
                if(!int.TryParse(part,NumberStyles.None,CultureInfo.InvariantCulture,out int index)||index.ToString(CultureInfo.InvariantCulture)!=part||index>=root.GetArrayLength())return null;
                root=root[index];
            }
            else return null;
        }
        return root;
    }
}
