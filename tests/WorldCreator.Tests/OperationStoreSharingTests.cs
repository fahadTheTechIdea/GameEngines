using System.Text.Json;
using WorldCreator.Core.Operations;

internal static class OperationStoreSharingTests
{
    public static async Task RunAsync()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Controlled Windows sharing-violation checks require Windows.");
        string folder=Path.Combine(Path.GetTempPath(),"world-operation-sharing-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"events.jsonl");
        OperationEvent Event(string id)=>new(id,DateTimeOffset.UtcNow,OperationSeverity.Information,"test","sharing",id,"Controlled sharing test");
        try
        {
            Task pending;
            using(var held=new FileStream(file,FileMode.OpenOrCreate,FileAccess.Write,FileShare.Read))
            {
                pending=new JsonLineOperationStore(file).WriteAsync(Event("after-release"));
                await Task.Delay(100);
                if(pending.IsCompleted)throw new Exception("Writer did not wait for held sharing lock");
            }
            await pending;
            await Task.WhenAll(Enumerable.Range(0,12).Select(i=>new JsonLineOperationStore(file).WriteAsync(Event("parallel-"+i))));
            using(var held=new FileStream(file,FileMode.Open,FileAccess.Write,FileShare.Read))
            {
                bool failed=false;
                try{await new JsonLineOperationStore(file).WriteAsync(Event("never-written"));}
                catch(IOException error)when((error.HResult&0xffff) is 32 or 33){failed=true;}
                if(!failed)throw new Exception("Permanent sharing conflict was hidden");
                using var cancel=new CancellationTokenSource(40);bool cancelled=false;
                try{await new JsonLineOperationStore(file).WriteAsync(Event("cancelled"),cancel.Token);}
                catch(OperationCanceledException){cancelled=true;}
                if(!cancelled)throw new Exception("Retry wait ignored cancellation");
            }
            var rows=File.ReadAllLines(file).Select(line=>JsonSerializer.Deserialize<OperationEvent>(line)!).ToArray();
            if(rows.Length!=13||rows.Select(r=>r.EventId).Distinct(StringComparer.Ordinal).Count()!=13||rows.Any(r=>r.EventId is "cancelled" or "never-written"))
                throw new Exception("Durable operation records lost, duplicated or partially written");
            Console.WriteLine("PASS: Windows open-sharing recovery, 12 independent appenders, bounded refusal, cancellation and exactly one complete record per successful event.");
        }
        finally{if(File.Exists(file))File.Delete(file);if(Directory.Exists(folder))Directory.Delete(folder);}
    }
}
