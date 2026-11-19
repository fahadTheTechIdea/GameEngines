using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldDirectInputTests
{
    private sealed class CallbackLine(Action<int> callback):IMetadataLayout
    {
        public string Id=>"line";
        public IReadOnlyList<IReadOnlyList<double>> Plan(JsonElement p,int dimensions,int count,CancellationToken token=default)
        {var result=new MetadataLineLayout().Plan(p,dimensions,count,token);callback(count);return result;}
    }
    private sealed class Input
    {
        internal readonly Dictionary<string,int> Parameters=new(){{"members",2}};
        internal readonly Dictionary<string,JsonElement> Properties=new(){{"label",JsonSerializer.SerializeToElement("original")}};
        internal readonly double[] Origin=[3,7];
        internal readonly string[] Axes=["x","y"];
        internal readonly string[] Remaining=["Declared input-isolation fixture; no physical/art/runtime acceptance."];
        internal readonly MetadataPart[] Parts=[
            new("a","member",0,"First group",CountParameter:"members",Layout:MetadataLineLayout.Parameters([0,0],[1,0])),
            new("b","member",0,"Second group",CountParameter:"members")];
        internal WorldMetadataRecipe Recipe;
        internal WorldMetadataCatalog Catalog;
        internal Input()
        {
            Catalog=new(1,"direct-catalog",[new("root","world","Complete declared program",new Dictionary<string,JsonElement>(),Parts,[],[]),
                new("member","entity","Declared member",Properties,[],[],[])]);
            Recipe=new(1,"direct-world","framework",Catalog.Id,"root","level",MetadataCoverage.PartialScope,Remaining,Parameters,new(Axes,"meters"),Origin);
        }
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Direct input isolation: "+message);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Evidence exists");Directory.CreateDirectory(output);
        var original=new Input();var expected=new WorldMetadataGenerator().Generate(original.Recipe,original.Catalog);
        foreach(bool duringValidation in new[]{false,true})
        {
            var input=new Input();bool called=false;
            var generator=new WorldMetadataGenerator(layouts:[new CallbackLine(count=>{
                if(called||(count==0)!=duringValidation)return;called=true;
                input.Parameters["members"]=5;
                input.Parts[1]=input.Parts[1] with{CountParameter=null,Count=7,Purpose="Caller replacement"};
                input.Properties["label"]=JsonSerializer.SerializeToElement("caller changed");
                input.Origin[0]=999;input.Axes[0]="caller-axis";input.Remaining[0]="Caller scope replacement";
            })]);
            var world=generator.Generate(input.Recipe,input.Catalog);
            Check(called&&input.Parameters["members"]==5,"actual caller mutation exercised");
            Check(world.Nodes.Count==5&&Json(world)==Json(expected),"validation/run callbacks cannot mix counts, geometry, properties or source hashes");
            input.Properties.Clear();input.Parameters.Clear();Array.Clear(input.Origin);
            Check(Json(world)==Json(expected),"returned world remains detached after caller storage changes");
            await File.WriteAllTextAsync(Path.Combine(output,duringValidation?"validation-world.json":"layout-world.json"),Json(world));
        }
        var discover=new Input();var descriptions=new WorldMetadataGenerator().DescribeParameters(discover.Recipe,discover.Catalog);
        bool observed=false;
        var api=new WorldMetadataGenerator(layouts:[new CallbackLine(_=>{
            if(observed)return;observed=true;
            discover.Parts[0]=discover.Parts[0] with{CountParameter="replacement"};
            discover.Parts[1]=discover.Parts[1] with{CountParameter="another"};
            discover.Parameters.Clear();
        })]);
        Check(Json(api.DescribeParameters(discover.Recipe,discover.Catalog))==Json(descriptions)&&observed,"binding discovery uses the captured catalog across layout validation");
        var sessionInput=new Input();var session=new MetadataGenerationSession(new WorldMetadataGenerator(),sessionInput.Recipe,sessionInput.Catalog);
        Check(Json(session.Snapshot.World)==Json(expected),"direct and managed session generation agree");
        foreach(double number in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})
        {
            var invalid=new Input();Reject(()=>new WorldMetadataGenerator().Generate(invalid.Recipe with{Origin=[number,0]},invalid.Catalog),"METADATA_ORIGIN_INVALID");
            Reject(()=>new WorldMetadataGenerator().DescribeParameters(invalid.Recipe with{Origin=[number,0]},invalid.Catalog),"METADATA_ORIGIN_INVALID");
        }
        var cancelled=new Input();using(var cts=new CancellationTokenSource())
        {
            cts.Cancel();bool invoked=false;
            var generator=new WorldMetadataGenerator(layouts:[new CallbackLine(_=>invoked=true)]);
            try{generator.Generate(cancelled.Recipe,cancelled.Catalog,cts.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}
            Check(!invoked,"cancelled capture never calls extension code");
        }
        using(var cts=new CancellationTokenSource())
        {
            var generator=new WorldMetadataGenerator(layouts:[new CallbackLine(_=>cts.Cancel())]);
            try{generator.DescribeParameters(cancelled.Recipe,cancelled.Catalog,cts.Token);throw new Exception("Missing callback cancellation");}catch(OperationCanceledException){}
        }
        await File.WriteAllTextAsync(Path.Combine(output,"baseline-world.json"),Json(expected));
        await File.WriteAllTextAsync(Path.Combine(output,"bindings.json"),Json(descriptions));
        Console.WriteLine("PASS: direct recipe/catalog capture before validation and layout callbacks; complete world/hash equivalence, arrays/properties/controls, binding discovery, output isolation, unversioned direct extension, managed equality, nonfinite semantic diagnostics and cancellation.");
    }
}
