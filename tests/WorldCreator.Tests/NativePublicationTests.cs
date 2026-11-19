using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using WorldCreator.Godot;

internal static class NativePublicationTests
{
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern SafeFileHandle CreateFileW(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);

    public static async Task VerifyAsync(string root)
    {
        if(!OperatingSystem.IsWindows())return;
        string staging=Path.Combine(root,".native-world-controlled-lock"),target=Path.Combine(root,"lock-recovery");
        Directory.CreateDirectory(staging);await File.WriteAllTextAsync(Path.Combine(staging,"fixture.txt"),"Native publication test only");
        // Hold the real directory before invoking the production publication owner;
        // an asynchronous creation watcher can lose the race to a fast rename.
        using var handle=CreateFileW(staging,0x80000000,3,IntPtr.Zero,3,0x02000000,IntPtr.Zero);
        if(handle.IsInvalid)throw new IOException("Directory lock acquisition failed: "+Marshal.GetLastWin32Error());
        var publication=GodotStructureLayerExporter.PublishNativeDirectoryAsync(staging,target,CancellationToken.None);
        var deadline=System.Diagnostics.Stopwatch.StartNew();int observed=0;
        try
        {
            while(observed<2&&deadline.Elapsed<TimeSpan.FromSeconds(2))
            {
                try
                {
                    using var file=new FileStream(Path.Combine(staging,"publication.json"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                    using var value=JsonDocument.Parse(file);observed=value.RootElement.GetProperty("CommitAttempt").GetInt32();
                }
                catch(Exception error)when(error is IOException or JsonException){}
                if(observed<2)await Task.Delay(5);
            }
        }
        finally{handle.Dispose();}
        await publication;
        using var record=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(target,"publication.json")));
        if(observed<2||record.RootElement.GetProperty("CommitAttempt").GetInt32()<2||record.RootElement.GetProperty("EarlierFailures").GetArrayLength()<1)
            throw new Exception("Native publication retry was not exercised by the real directory lock");
        Console.WriteLine("PASS: Windows publication recovers from a real sharing/access lock with retained bounded-attempt evidence.");
    }
}
