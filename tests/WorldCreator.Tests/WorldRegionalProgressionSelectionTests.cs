using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;

internal static class WorldRegionalProgressionSelectionTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string reason){if(!value)throw new Exception("Progression selection: "+reason);}
    static void Reject(Action run,string code){try{run();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var arrival=template.Regions.Single(r=>r.Id=="arrival");
        const string parent="region-west/compounds-0",child="region-west/compounds-0/modules-0";
        var linked=arrival.Recipe with{AdditionalConnections=[..arrival.Recipe.AdditionalConnections!,
            new("program-entry","program-access",child,parent,true)]};
        var rule=new MetadataRegionalProgressionRule("journey","Access the wider world","test:journey",
            new("arrival",child),[new("outpost",parent)],["program-access","regional-travel"],[new("arrival",parent)]);
        var request=new MetadataRegionalDesignRequest(1,"select-progression","Choose a connected program","test:selection",template,
            [new("arrival",[new("disconnected",0,arrival.Recipe,arrival.Catalog),new("connected",1,linked,arrival.Catalog)])],[],2,
            WorldProgression:[rule]);
        var engine=new MetadataRegionalDesignGenerator();
        Reject(()=>engine.Generate(request with{WorldProgression=[rule with{Start=new("unknown",child)}]}),"METADATA_REGIONAL_PROGRESSION_RULE_INVALID");
        Reject(()=>engine.Generate(request with{WorldProgression=[rule with{ConnectionKinds=[]}]}),"METADATA_REGIONAL_PROGRESSION_RULE_INVALID");
        Reject(()=>engine.Generate(request with{WorldProgression=[rule,rule]}),"METADATA_REGIONAL_PROGRESSION_RULE_INVALID");
        try{engine.Generate(request with{WorldProgression=[rule with{MaximumWork=0}]});throw new Exception("Invalid budget accepted");}
        catch(ArgumentException e)when(e.Message=="METADATA_REGIONAL_PROGRESSION_RULE_INVALID")
        {Check(!e.Data.Contains("RegionalDesignAttempts"),"invalid declaration rejected before candidate generation");}
        var invalidCapabilities=rule with{Capabilities=new(["key"],[],[],[new(new(null,"gate"),["missing"])])};
        Reject(()=>engine.Generate(request with{WorldProgression=[invalidCapabilities]}),"METADATA_PROGRESSION_CAPABILITY_UNKNOWN");
        var result=engine.Generate(request);var attempts=result.Receipt.Attempts;
        Check(attempts.Count==2&&!attempts[0].Accepted&&attempts[0].Rejection!.StartsWith("METADATA_PROGRESSION_UNREACHABLE:",StringComparison.Ordinal)&&
            attempts[1].Accepted&&attempts[1].Selections.Single().OptionId=="connected","selects connected alternative after actual progression refusal");
        var path=attempts[1].WorldProgression!.Results.Single().Paths.Single();
        Check(path.Nodes.First()==rule.Start&&path.Nodes.Last()==rule.Goals[0]&&path.Nodes.Contains(rule.RequiredMilestones[0])&&
            path.Links.Any(l=>l.RegionId=="arrival"&&l.ConnectionId.EndsWith("/program-entry",StringComparison.Ordinal))&&
            path.Links.Any(l=>l.RegionId is null),"witness includes authored local and generated regional links");
        Check(result.World.Regions.Count==4&&result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&
            result.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&result.World.Connections.Count==3&&
            result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"all required source nodes, sites and regional return routes remain");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await engine.PublishAsync(result,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(result),"rejection and selected world progression replay");
        Console.WriteLine("PASS: progression rejection selects a complete connected alternative; early malformed input refusal and replay verified.");
    }
}
