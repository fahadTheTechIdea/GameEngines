using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldDomainTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("World domains: "+message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    internal static MetadataWorldDomainDefinition Example()
    {
        var outside=WorldLayoutAdaptationTests.Example(power:true);var floor=WorldGeneratedInteriorTests.Example(count:3);
        // Author stable endpoint references from the current generated topology,
        // not guessed spatial coordinates. Both floors intentionally reuse it.
        var known=new WorldMetadataGenerator().Generate(floor.Recipe,floor.Catalog);var openings=known.InteriorPlan!.Results.Single().Openings;
        string entry="generated-interior/rooms-0",exit="generated-interior/rooms-2";
        string Entry(string room)=>openings.First(o=>o.FromId==room||o.ToId==room).ConnectionId;
        var profile=floor.Recipe.Interiors!.Rules[0].Profile;
        MetadataDomainEndpoint Room(string domain,string id)=>new(domain,id,InteriorConnectionId:Entry(id),Profile:profile);
        return new(1,"exploration-domains","Reusable outdoor and independent interior floors","domains:program",
            [new("outdoor","Complete outside program","domains:outside",outside.Recipe,outside.Catalog),
                new("lower","First independent floor","domains:lower",floor.Recipe,floor.Catalog),
                new("upper","Second floor with the same local coordinates and node IDs","domains:upper",floor.Recipe,floor.Catalog)],
            [new("entry","doorway","Transition between world domains","domains:entry",new("outdoor","local-layouts/sites-0","east",Profile:outside.Recipe.RouteRules![0].Profile),Room("lower",entry),true),
                new("stairs","stairs","Explicit floor transition; no implied 3D stair mesh","domains:stairs",Room("lower",exit),Room("upper",entry),true)],
            [new("round-trip","Required domain graph traversal","domains:graph","outdoor","upper",true)]);
    }
    public static async Task WriteExampleAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory,"definition.json"),Json(Example()));
    }
    public static async Task RunAsync()
    {
        var definition=Example();var generator=new WorldMetadataGenerator();var world=generator.GenerateDomains(definition);
        Check(world.Domains.Count==3&&world.Domains.Sum(d=>d.World.Nodes.Count)==21&&world.Transitions.Count==2,"all three complete local programs retained");
        var lower=world.Domains.Single(d=>d.Id=="lower").World;var upper=world.Domains.Single(d=>d.Id=="upper").World;
        Check(Json(lower)==Json(upper),"identical local IDs/geometry in separate domains do not collide or merge");
        foreach(var source in definition.Domains)Check(Json(world.Domains.Single(d=>d.Id==source.Id).World)==Json(generator.Generate(source.Recipe,source.Catalog)),"local generation is unchanged");
        var entry=world.Transitions.Single(t=>t.Definition.Id=="entry");var outdoor=world.Domains.Single(d=>d.Id=="outdoor").World;
        var port=outdoor.AccessPlan!.Sites.Single(s=>s.NodeId==entry.From.Definition.NodeId).Ports.Single(p=>p.Id=="east");
        Check(entry.From.Position!.SequenceEqual(new[]{port.ApproachPosition.X,port.ApproachPosition.Y})&&entry.From.AvailableWidth==port.Width,"exact existing outdoor access endpoint");
        foreach(var transition in world.Transitions)foreach(var endpoint in new[]{transition.From,transition.To}.Where(e=>e.Definition.InteriorConnectionId is not null))
        {
            var local=world.Domains.Single(d=>d.Id==endpoint.Definition.DomainId).World;var opening=local.InteriorPlan!.Results.SelectMany(r=>r.Openings).Single(o=>o.ConnectionId==endpoint.Definition.InteriorConnectionId);
            var point=opening.FromId==endpoint.Definition.NodeId?opening.FromApproach:opening.ToApproach;
            Check(endpoint.Position!.SequenceEqual(new[]{point.X,point.Y})&&endpoint.AvailableWidth==opening.Width,"exact existing interior-side endpoint");
        }
        var path=world.Reachability.Single();Check(path.ForwardTransitions.SequenceEqual(new[]{"entry","stairs"})&&path.ReturnTransitions!.SequenceEqual(new[]{"stairs","entry"}),"independent directed path oracle");
        Check(Json(generator.GenerateDomains(definition with{Domains=definition.Domains.Reverse().ToArray(),Transitions=definition.Transitions.Reverse().ToArray()}))==Json(world),"canonical declaration order");
        generator.ValidateDomains(world);
        Reject(()=>generator.ValidateDomains(world with{TransitionWork=world.TransitionWork+1}),"METADATA_DOMAIN_DOCUMENT_MISMATCH");
        Reject(()=>generator.ValidateDomains(world with{Transitions=world.Transitions.Select(t=>t with{From=t.From with{Position=[999,999]}}).ToArray()}),"METADATA_DOMAIN_DOCUMENT_MISMATCH");
        Reject(()=>generator.GenerateDomains(definition with{MaximumTotalNodes=20}),"METADATA_DOMAIN_CONTENT_BUDGET");
        Reject(()=>generator.GenerateDomains(definition with{MaximumTransitionWork=world.TransitionWork-1}),"METADATA_DOMAIN_TRANSITION_WORK_BUDGET");
        Reject(()=>generator.GenerateDomains(definition with{MaximumGraphWork=world.GraphWork-1}),"METADATA_DOMAIN_GRAPH_WORK_BUDGET");
        Check(generator.GenerateDomains(definition with{MaximumTotalNodes=21,MaximumTransitionWork=world.TransitionWork,MaximumGraphWork=world.GraphWork}).GraphWork==world.GraphWork,"exact aggregate counts and proof budgets");
        var stairs=definition.Transitions.Single(t=>t.Id=="stairs");
        Reject(()=>generator.GenerateDomains(definition with{Transitions=definition.Transitions.Select(t=>t.Id=="stairs"?t with{Bidirectional=false}:t).ToArray()}),"METADATA_DOMAIN_UNREACHABLE:round-trip");
        var wide=stairs.From with{Profile=stairs.From.Profile! with{Radius=1}};
        Reject(()=>generator.GenerateDomains(definition with{Transitions=[definition.Transitions[0],stairs with{From=wide}]}),"METADATA_DOMAIN_ENDPOINT_TOO_WIDE");
        Reject(()=>generator.GenerateDomains(definition with{Transitions=[definition.Transitions[0],stairs with{From=stairs.From with{Profile=stairs.From.Profile! with{KindCosts=new Dictionary<string,double>{{"water",1}}}}}]}),"METADATA_DOMAIN_ENDPOINT_BLOCKED");
        Reject(()=>generator.GenerateDomains(definition with{Transitions=[definition.Transitions[0],stairs with{From=stairs.From with{NodeId="missing"}}]}),"METADATA_DOMAIN_NODE_REQUIRED");
        Reject(()=>generator.GenerateDomains(definition with{Transitions=[definition.Transitions[0],stairs with{From=stairs.From with{InteriorConnectionId="missing"}}]}),"METADATA_DOMAIN_OPENING_REQUIRED");
        Reject(()=>generator.GenerateDomains(definition with{Transitions=[definition.Transitions[0],stairs with{From=stairs.From with{Profile=null}}]}),"METADATA_DOMAIN_PHYSICAL_ENDPOINT_REQUIRED");
        Reject(()=>generator.GenerateDomains(definition with{Domains=[definition.Domains[0],definition.Domains[0]]}),"METADATA_DOMAIN_INVALID");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.GenerateDomains(definition,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        string directory=Path.Combine(Path.GetTempPath(),"world-domains-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);string input=Path.Combine(directory,"definition.json"),output=Path.Combine(directory,"output");
        await File.WriteAllTextAsync(input,Json(definition));var saved=await generator.GenerateDomainFilesAsync(input,output);
        var restored=await WorldMetadataGenerator.ReadAsync<MetadataWorldDomainDocument>(Path.Combine(output,"world-domains.json"));Check(Json(saved)==Json(restored),"collection source/dependencies/worlds/proofs replay on read");
        Check(Directory.GetDirectories(output).Length==3&&File.Exists(Path.Combine(output,"index.html")),"one atomic complete collection with all local schematics");
        foreach(string folder in Directory.GetDirectories(output))
        {
            Check(File.Exists(Path.Combine(folder,"world-metadata.json"))&&File.Exists(Path.Combine(folder,"schematic.html")),"per-domain map exported");
            int index=int.Parse(Path.GetFileName(folder)[7..],System.Globalization.CultureInfo.InvariantCulture);
            string page=await File.ReadAllTextAsync(Path.Combine(folder,"schematic.html"));
            using var navigation=JsonDocument.Parse(System.Text.RegularExpressions.Regex.Match(page,"<script id=\"domain-data\" type=\"application/json\">([\\s\\S]*?)</script>").Groups[1].Value);
            var view=navigation.RootElement;
            Check(view.GetProperty("Version").GetInt32()==1&&view.GetProperty("CurrentDomainId").GetString()==saved.Domains[index].Id&&
                view.GetProperty("DefinitionSha256").GetString()==saved.DefinitionSha256,"each child binds its own independent domain receipt");
            var entries=view.GetProperty("Entries");Check(entries.GetArrayLength()==3,"every coordinate domain is selectable");
            for(int j=0;j<3;j++)Check(entries[j].GetProperty("Page").GetString()=="../domain-"+j.ToString("D4",System.Globalization.CultureInfo.InvariantCulture)+"/schematic.html"&&
                entries[j].GetProperty("Id").GetString()==saved.Domains[j].Id&&entries[j].GetProperty("WorldId").GetString()==saved.Domains[j].World.Id&&
                entries[j].GetProperty("RecipeSha256").GetString()==saved.Domains[j].World.RecipeSha256&&entries[j].GetProperty("CatalogSha256").GetString()==saved.Domains[j].World.CatalogSha256,"canonical sibling target and source identity");
            Check(view.GetProperty("Transitions").GetArrayLength()==saved.Transitions.Count,"all declared transitions retained in navigation");
        }
        var standalone=await new WorldMetadataSchematicExporter().CreateAsync(saved.Domains[0].World);
        Check(standalone.Html.Contains("<script id=\"domain-data\" type=\"application/json\">null</script>"),"standalone export has no invented collection context");
        string accepted=await File.ReadAllTextAsync(Path.Combine(output,"world-domains.json"));
        try{await generator.GenerateDomainFilesAsync(input,output);throw new Exception("Missing existing-output refusal");}catch(IOException e)when(e.Message=="METADATA_DOMAIN_OUTPUT_EXISTS"){}
        Check(await File.ReadAllTextAsync(Path.Combine(output,"world-domains.json"))==accepted,"existing complete collection preserved");
        string refused=Path.Combine(directory,"refused");await File.WriteAllTextAsync(input,Json(definition with{Transitions=definition.Transitions.Select(t=>t with{Bidirectional=false}).ToArray()}));
        try{await generator.GenerateDomainFilesAsync(input,refused);throw new Exception("Missing collection refusal");}catch(ArgumentException e)when(e.Message.StartsWith("METADATA_DOMAIN_UNREACHABLE",StringComparison.Ordinal)){}
        Check(!Directory.Exists(refused)&&!Directory.EnumerateDirectories(directory,".world-domains-*").Any(),"failed generation publishes no partial collection");
        var properties=new Dictionary<string,JsonElement>();var catalog=new WorldMetadataCatalog(1,"abstract-domains",[new("state","state","A logical state",properties,[],[],[])]);
        var recipe=new WorldMetadataRecipe(1,"shared-state","abstract-game","abstract-domains","state","topology",MetadataCoverage.CompleteDeclaredScope,[],new Dictionary<string,int>(),new(["x","y"],"abstract"));
        var logical=new MetadataWorldDomainDefinition(1,"abstract-links","Independent geometry-free game domains","domains:abstract",[new("a","First abstract domain","domains:a",recipe,catalog),new("b","Second abstract domain","domains:b",recipe,catalog)],
            [new("advance","advance","Declared logical transition","domains:advance",new("a","shared-state"),new("b","shared-state"))],
            [new("forward","Required abstract transition","domains:reach","a","b")]);
        var graph=generator.GenerateDomains(logical);Check(graph.Domains.All(d=>d.World.Surface is null)&&graph.Domains.Sum(d=>d.World.Nodes.Count)==2&&graph.Transitions.Single().From.Position is null,"logical domains require no terrain/art/physical endpoints");
        generator.ValidateDomains(graph);
        const string sourceText="</script><script>untrusted()</script> __WORLD_METADATA_JSON__ __DOMAIN_NAVIGATION_JSON__";
        var escaped=logical with{Domains=logical.Domains.Select(d=>d.Id=="a"?d with{Purpose=sourceText}:d).ToArray()};
        await File.WriteAllTextAsync(input,Json(escaped));string escapedOutput=Path.Combine(directory,"escaped");await generator.GenerateDomainFilesAsync(input,escapedOutput);
        string escapedPage=await File.ReadAllTextAsync(Path.Combine(escapedOutput,"domain-0000","schematic.html"));
        using(var nav=JsonDocument.Parse(System.Text.RegularExpressions.Regex.Match(escapedPage,"<script id=\"domain-data\" type=\"application/json\">([\\s\\S]*?)</script>").Groups[1].Value))
            Check(nav.RootElement.GetProperty("Entries")[0].GetProperty("Purpose").GetString()==sourceText&&!escapedPage.Contains("<script>untrusted()"),"source text and placeholder names survive safe HTML encoding");
        Console.WriteLine($"PASS: three independent domains / 21 local nodes / two qualified transitions, overlapping floors and unchanged generation, exact endpoints and bidirectional graph, canonical order, source/witness replay, invalid/narrow/missing/one-way/content/work/cancel refusal and atomic per-domain export. Endpoint work={world.TransitionWork}; graph work={world.GraphWork}; files={directory}");
    }
}
