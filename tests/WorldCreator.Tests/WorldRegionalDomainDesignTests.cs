using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalDomainDesignTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Domain-aware design: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}

    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var arrival=template.Regions.Single(r=>r.Id=="arrival");
        const string parent="region-west/compounds-0",child="region-west/compounds-0/modules-0";
        var logical=arrival.Recipe with{AdditionalConnections=[..arrival.Recipe.AdditionalConnections!,new("program-entry","program-access",child,parent,true)]};
        var profile=template.NetworkRules!.Single().Options.Profile;
        var physical=logical with{RouteRules=[..logical.RouteRules!,new("entry-route","program-access",profile,new(.1,[]),"east","east")]};
        var progress=new MetadataRegionalProgressionRule("progress","Reach the wider world","test:progress",new("arrival",child),
            [new("outpost",parent)],["program-access","regional-travel"],[new("arrival",parent)]);
        var design=new MetadataRegionalDesignRequest(1,"domain-aware","Select a world with a complete domain journey","test:design",template,
            [new("arrival",[new("logical-only",0,logical,arrival.Catalog),new("routed",1,physical,arrival.Catalog)])],[],2,WorldProgression:[progress]);
        var catalog=new WorldMetadataCatalog(1,"design-domain",[new("root","state","Independent destination",new Dictionary<string,JsonElement>(),[],[],[])]);
        var recipe=new WorldMetadataRecipe(1,"root","framework",catalog.Id,"root","topology",MetadataCoverage.PartialScope,["Framework test"],new Dictionary<string,int>(),new(["x","y"],"abstract"));
        var start=new MetadataDomainEndpoint("arrival",child,"east",Profile:profile);
        var entry=new MetadataDomainEndpoint("arrival",parent,"east",Profile:profile);
        var target=new MetadataDomainEndpoint("logical","root");
        var profiles=template.Regions.ToDictionary(r=>r.Id,_=>profile,StringComparer.Ordinal);
        var input=new MetadataRegionalDomainWorldDefinition(1,"designed-domains","Select and compose complete world","test:composition",null,
            [new("logical","Independent domain","test:logical",recipe,catalog)],
            [new("portal","portal","Explicit domain transition","test:portal",entry,target,true)],
            Journeys:[new("domain-journey","Actual route to independent domain and return","test:journey",start,target,["program-access","portal"],profiles,true)],
            RegionalDesign:design);
        var engine=new MetadataRegionalDomainWorldGenerator();var world=engine.Generate(input);
        var attempts=world.Selection!.Attempts;
        Check(attempts.Count==2&&!attempts[0].Accepted&&attempts[0].Rejection=="METADATA_DOMAIN_JOURNEY_UNREACHABLE:domain-journey"&&
            attempts[1].Accepted&&attempts[1].Selections.Single().OptionId=="routed","unreachable candidate rejected; actual physical route selected");
        var journey=world.Domains.Journeys!.Single();
        Check(journey.Forward.Any(s=>s.Kind=="route-segment"&&s.SourceId.Contains("program-entry",StringComparison.Ordinal))&&
            journey.Forward.Any(s=>s.SourceId=="portal")&&journey.Return is {Count:>0},"complete physical and domain round trip");
        var progression=new MetadataDomainProgressionRule("domain-progress","Reach the independent domain","test:progress",
            new("arrival",child),[new("logical","root")],["program-access","portal"],[]);
        var progressionDesign=design with{WorldProgression=null,Requirements=[new("modules","arrival","module",2,2)],
            Slots=[new("arrival",[new("disconnected",0,arrival.Recipe,arrival.Catalog),new("linked",1,logical,arrival.Catalog)])]};
        var progressionInput=input with{RegionalDesign=progressionDesign,Journeys=null,Progression=[progression]};
        var progressionWorld=engine.Generate(progressionInput);var progressionAttempts=progressionWorld.Selection!.Attempts;
        Check(progressionAttempts.Count==2&&!progressionAttempts[0].Accepted&&
            progressionAttempts[0].Rejection!.StartsWith("METADATA_PROGRESSION_UNREACHABLE:",StringComparison.Ordinal)&&
            progressionAttempts[1].Selections.Single().OptionId=="linked"&&progressionWorld.Domains.Progression is not null,
            "domain progression rejects an unconnected program and selects a linked alternative");
        Reject(()=>engine.Validate(world with{Selection=world.Selection with{Attempts=attempts.Skip(1).ToArray()}}),"METADATA_REGIONAL_DOMAIN_DOCUMENT_MISMATCH");
        Reject(()=>engine.Generate(input with{RegionalWorld=world.RegionalWorld.Definition}),"METADATA_REGIONAL_DOMAIN_DEFINITION_INVALID");
        await engine.PublishAsync(world,output);
        Check(File.Exists(Path.Combine(output,"selection.json"))&&(await File.ReadAllTextAsync(Path.Combine(output,"index.html"))).Contains("METADATA_DOMAIN_JOURNEY_UNREACHABLE",StringComparison.Ordinal),"selection evidence visible in inspector");
        var restored=await engine.ReadAsync(Path.Combine(output,"world.json"));
        Check(Json(restored)==Json(world),"selection, rejected attempt and combined world replay");
        Console.WriteLine("PASS: domain journey rejects a logical-only program, selects a routed program and preserves selection through publication and replay.");
    }
}
