using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Operations;
using WorldCreator.Core.Spatial;

internal static class WorldParentRouteTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool v,string why){if(!v)throw new Exception("Parent route: "+why);}
    private static T Reject<T>(Action run)where T:Exception
    {try{run();}catch(T error){return error;}throw new Exception("Expected "+typeof(T).Name);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(int rooms=6,double rotation=0,double offset=-.8)
    {
        // Author a single reusable program. This path does not generate a world
        // to discover per-parent node/connection IDs or copy path coordinates.
        var e=WorldInteriorPopulationTests.Source(rooms,rotation);var p=e.Recipe.Placement!.Programs!.Single();
        var catalog=e.Catalog with{Archetypes=[..e.Catalog.Archetypes.Select(a=>a.Id=="room"?a with{Parts=[..a.Parts,new("markers","marker",2,"Every required route-side equipment member")]}:a),WorldInteriorPopulationTests.Equipment()]};
        var program=p with{MinimumParents=1,MaximumParents=16,Request=p.Request with{Demands=[..p.Request.Demands,
            new("markers","Required route-side content","interior-population:placement",null,p.Request.Demands[0].Profile)]},
            Bindings=[..p.Bindings,new("markers","markers",Population:new("Follow the accepted incident room passage","interior-population:route",
                Route:new(null,.5,.6,NormalOffset:offset,Parent:new("door")),Facing:MetadataPopulationFacing.Along))]};
        return(e.Recipe with{PopulationSource=new(e.Recipe,e.Catalog),Placement=e.Recipe.Placement with{Programs=[program]}},catalog);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Parent route evidence exists");Directory.CreateDirectory(output);
        var g=new WorldMetadataGenerator();var facts=new List<object>();WorldMetadataDocument? baseline=null;
        foreach(var (name,count,rotation) in new[]{("branching",6,0d),("rotated",6,23d),("compact",3,0d)})
        {
            var e=Example(count,rotation);string folder=Path.Combine(output,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            var w=g.Generate(e.Recipe,e.Catalog);var explicitInput=WorldInteriorPopulationTests.Example(count,rotation);var explicitWorld=g.Generate(explicitInput.Recipe,explicitInput.Catalog);
            Check(Json(w.Nodes)==Json(explicitWorld.Nodes)&&Json(w.AccessPlan!.Sites)==Json(explicitWorld.AccessPlan!.Sites)&&Json(w.InteriorPlan!.Results)==Json(explicitWorld.InteriorPlan!.Results),"one program retains exact explicit per-parent geometry and original paths");
            Check(w.Nodes.Count==1+4*count&&w.PlacementPlan!.ProgramInstances!.Count==count&&e.Recipe.Placement!.NestedStages is null,"one authored program expands all required parents/members");
            Check(w.PlacementPlan!.PopulationOrientations!.Sum(t=>t.NodeIds.Count)==2*count,"complete derived facing witnesses");
            WorldMetadataGenerator.ValidateDocument(w);await g.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(folder,"published"));
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"published/world-metadata.json")))==Json(w),"native source/read/export replay");
            facts.Add(new{Name=name,Nodes=w.Nodes.Count,Physical=w.AccessPlan!.Sites.Count,Programs=e.Recipe.Placement!.Programs!.Count,Instances=count,Members=2*count,PlacementWork=w.PlacementPlan!.Work});
            if(name=="branching")baseline=w;
            Console.WriteLine("Qualified shared parent-route program: "+name+"; "+count+" parents; "+w.Nodes.Count+" nodes.");
        }
        SelectionCases(baseline!.PlacementPlan!.Definition.SourceWorld!,output);await Lifecycle(output,g);
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(facts));
        Console.WriteLine("PASS: reusable parent-route program, explicit-ID geometry equivalence, canonical ordinal/direction selection, bounded/cancelled work, missing-source refusal and edit/checkpoint lifecycle.");
    }
    private static void SelectionCases(WorldMetadataDocument source,string output)
    {
        var budget=new Func<WorldSpatialWorkBudget>(()=>new(1000000,CancellationToken.None));
        var parents=source.InteriorPlan!.Results.SelectMany(r=>r.RoomNodeIds).ToArray();var facts=new List<object>();int checkedCases=0;
        foreach(string parent in parents)
        {
            var ordered=source.Connections.Where(c=>c.Kind=="door"&&(c.FromId==parent||c.ToId==parent)).OrderBy(c=>c.Id,StringComparer.Ordinal).ToArray();
            for(int index=0;index<ordered.Length;index++)foreach(bool reverse in new[]{false,true})
            {
                var p=new MetadataGeneratedRoutePattern(null,.5,Reverse:reverse,Parent:new("door",index));var selected=ordered[index];bool actualReverse=reverse^(selected.ToId==parent);
                var expected=MetadataPopulationSources.RoutePath(new(selected.Id,.5,Reverse:actualReverse),source,parent,budget());
                Check(Json(MetadataPopulationSources.RoutePath(p,source,parent,budget()))==Json(expected),"independent incident-ID/direction oracle");
                Check(Json(MetadataPopulationSources.RoutePath(p,source with{Connections=source.Connections.Reverse().ToArray()},parent,budget()))==Json(expected),"input ordering cannot change source selection");
                facts.Add(new{Parent=parent,Index=index,Reverse=reverse,Selected=selected.Id,ActualReverse=actualReverse});checkedCases++;
            }
        }
        string owner=parents[0];var pattern=new MetadataGeneratedRoutePattern(null,.5,Parent:new("door"));
        foreach(var invalid in new[]{pattern with{ConnectionId=source.Connections[0].Id},pattern with{Parent=new("door",-1)},pattern with{Parent=new("door",128)},pattern with{Parent=new("invalid kind")}})
            Check(Reject<ArgumentException>(()=>MetadataPopulationSources.RoutePath(invalid,source,owner,budget())).Message=="METADATA_POPULATION_PARENT_ROUTE_SELECTOR_INVALID","invalid/conflicting selector refuses");
        foreach(string? invalidOwner in new string?[]{null,"missing"})
            Check(Reject<ArgumentException>(()=>MetadataPopulationSources.RoutePath(pattern,source,invalidOwner,budget())).Message=="METADATA_POPULATION_PARENT_ROUTE_CONTEXT_REQUIRED","source owner is mandatory");
        Check(Reject<ArgumentException>(()=>MetadataPopulationSources.RoutePath(pattern with{Parent=new("missing")},source,owner,budget())).Message=="METADATA_POPULATION_PARENT_ROUTE_MISSING","kind mismatch cannot select another kind");
        string leaf=parents.First(p=>source.Connections.Count(c=>c.Kind=="door"&&(c.FromId==p||c.ToId==p))==1);
        Check(Reject<ArgumentException>(()=>MetadataPopulationSources.RoutePath(pattern with{Parent=new("door",1)},source,leaf,budget())).Message=="METADATA_POPULATION_PARENT_ROUTE_MISSING","ordinal exhaustion cannot fall back to zero");
        var link=source.Connections.First(c=>c.Kind=="door");
        var oneWay=source with{InteriorPlan=source.InteriorPlan with{Results=source.InteriorPlan.Results.Select(r=>r with{Routes=r.Routes.Select(p=>p.ConnectionId==link.Id?p with{Reverse=null}:p).ToArray()}).ToArray()}};
        int rank=source.Connections.Where(c=>c.Kind=="door"&&(c.FromId==link.ToId||c.ToId==link.ToId)).OrderBy(c=>c.Id,StringComparer.Ordinal).Select((c,i)=>(c.Id,i)).Single(p=>p.Id==link.Id).i;
        Check(Reject<ArgumentException>(()=>MetadataPopulationSources.RoutePath(pattern with{Parent=new("door",rank)},oneWay,link.ToId,budget())).Message=="METADATA_POPULATION_SOURCE_DIRECTION_MISSING","missing reverse proof cannot silently use incoming direction");
        var absent=source with{Connections=[source.Connections[0] with{Id="0-unrouted",FromId=owner},..source.Connections]};
        Check(Reject<ArgumentException>(()=>MetadataPopulationSources.RoutePath(pattern,absent,owner,budget())).Message=="METADATA_POPULATION_SOURCE_ROUTE_MISSING","unrouted selected connection cannot fall back to an available one");
        var many=source with{Connections=[..source.Connections,..Enumerable.Range(0,10000).Select(i=>link with{Id="z-extra-"+i.ToString("D5"),FromId=owner})]};
        Check(Json(MetadataPopulationSources.RoutePath(pattern,many,owner,budget()))==Json(MetadataPopulationSources.RoutePath(pattern,source,owner,budget())),"bounded prefix retains ordinal despite 10000 later connections");
        Check(Reject<ArgumentException>(()=>MetadataPopulationSources.RoutePath(pattern,source,owner,new(1,CancellationToken.None))).Message=="WORLD_ROUTE_WORK_BUDGET","selector charges actual shared work");
        using(var c=new CancellationTokenSource()){c.Cancel();Reject<OperationCanceledException>(()=>MetadataPopulationSources.RoutePath(pattern,source,owner,new(100000,c.Token)));}
        File.WriteAllText(Path.Combine(output,"selection-oracle.json"),Json(new{Cases=checkedCases,Selections=facts,LargeIncidentEdges=many.Connections.Count,
            Scope="Direct source-selection unit proofs; forged/missing-trace inputs are not valid whole documents. Complete native source replay is exercised by the published cases."}));
    }
    private static async Task Lifecycle(string output,WorldMetadataGenerator g)
    {
        var a=Example(3);var b=Example(6);var settings=new MetadataSessionReporting(new(new JsonLineOperationStore(Path.Combine(output,"log.jsonl")),
            new JsonLineOperationStore(Path.Combine(output,"audit.jsonl")),new JsonLineOperationStore(Path.Combine(output,"notifications.jsonl"))),"catalog-program-reference");
        var session=await MetadataGenerationSession.StartAsync(g,a.Recipe,a.Catalog,settings);var initial=session.Snapshot;
        await session.PublishAsync(Path.Combine(output,"baseline"));
        var edit=new MetadataEditRequest("more-rooms","Expand the complete source program using the same room-content template","parent-route:count",initial.World.RecipeSha256,initial.World.CatalogSha256,b.Recipe,b.Catalog);
        await File.WriteAllTextAsync(Path.Combine(output,"edit.json"),Json(edit));
        var accepted=await session.ApplyAndPublishAsync(edit,Path.Combine(output,"edited"));
        Check(initial.World.Nodes.Count==13&&accepted.World.Nodes.Count==25&&Json(accepted.World)==Json(g.Generate(b.Recipe,b.Catalog)),"authored parent-count edit expands every required child and matches clean generation");
        Check(Json(a.Recipe.Placement!.Programs)==Json(b.Recipe.Placement!.Programs),"identical reusable content program handles changed parent count");
        var restored=await MetadataGenerationSession.RestoreAsync(g,Path.Combine(output,"edited/generation-session.json"),reporting:settings);
        Check(Json(restored.Snapshot)==Json(accepted),"source history restores exact generated programs and route derivation");
        var program=b.Recipe.Placement!.Programs![0];var bindings=program.Bindings.Select(x=>x.Population?.Route is not {} p?x:x with{Population=x.Population with{Route=p with{Parent=new("missing")}}}).ToArray();
        var invalid=b.Recipe with{Placement=b.Recipe.Placement with{Programs=[program with{Bindings=bindings}]}};
        var bad=new MetadataEditRequest("missing-route","Retain accepted world when shared selector has no source","parent-route:rollback",accepted.World.RecipeSha256,accepted.World.CatalogSha256,invalid,b.Catalog);
        await File.WriteAllTextAsync(Path.Combine(output,"invalid-edit.json"),Json(bad));
        try{await restored.ApplyAndPublishAsync(bad,Path.Combine(output,"refused"));throw new Exception("Missing selector refusal");}catch(ArgumentException){}
        Check(Json(restored.Snapshot)==Json(accepted)&&!Directory.Exists(Path.Combine(output,"refused")),"invalid shared selectors roll back complete accepted world/history/output");
    }
}
