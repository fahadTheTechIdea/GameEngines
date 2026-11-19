using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Geometry;

internal static class WorldRegionalDesignProtectionTests
{
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    public static async Task PinnedSeedAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var first=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/world-seed-a-request.json");
        var second=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/world-seed-b-request.json");
        string authored=Json(second);var generator=new WorldMetadataGenerator();
        var session=await MetadataRegionalGenerationSession.StartDesignedAsync(generator,first);
        var original=session.Snapshot;var origin=original.World.Definition.Regions.Single(r=>r.Id=="arrival").Recipe.Origin!;
        MetadataRegionalDesignEditRequest Edit(string id,MetadataRegionalDesignRequest source,MetadataRegionalProtectionChange? change=null)
        {var before=session.Snapshot;return new(id,"Change seeded layout around a protected destination","test:pinned-seed",before.World.DefinitionSha256,
            before.Report.WorldSha256,before.DesignSourceSha256,source,change);}
        var guard=new MetadataRegionalAnchorProtection("keep-arrival","arrival","Keep the accepted destination location","test:keep-arrival");
        await session.ApplyDesignAsync(Edit("pin",first,new(original.Report.EditId,[],[],[guard])));
        var edited=await session.ApplyDesignAndPublishAsync(Edit("new-seed",second),output);
        var actual=edited.World.Definition.Regions.Single(r=>r.Id=="arrival").Recipe.Origin!;
        var effective=edited.World.Definition.OwnershipPlan!.Source.Regions.Single(r=>r.Id=="arrival").PinnedAnchor;
        if(!actual.SequenceEqual(origin)||effective?.Position!=new WorldPoint(origin[0],origin[1])||
            original.World.Definition.Surface.Terrain!.Detail!.Seed==edited.World.Definition.Surface.Terrain!.Detail!.Seed||
            edited.World.Regions.Sum(r=>r.World.Nodes.Count)!=20||edited.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)!=12||
            edited.World.Connections.Count!=3||edited.World.Connections.Any(c=>c.Forward.Path is null||c.Reverse?.Path is null))
            throw new Exception("Seeded regeneration did not preserve the pinned destination and complete physical scope");
        if(Json(second)!=authored||Json(edited.DesignSource)!=authored)throw new Exception("Effective anchor binding changed authored design inputs");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));
        if(Json(restored.Snapshot)!=Json(edited))throw new Exception("Pin-conditioned seed history did not replay");
        Console.WriteLine("PASS: changed world seed regenerates terrain and layout around the protected destination, retains all physical content/routes and replays unmodified authored sources.");
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var region=template.Regions.Single(r=>r.Id=="arrival");
        int root=region.Catalog.Archetypes.ToList().FindIndex(a=>a.Id==region.Recipe.RootArchetypeId);
        WorldMetadataCatalog Catalog(int value)=>region.Catalog with{Archetypes=region.Catalog.Archetypes.Select((a,i)=>i!=root?a:a with{
            Properties=new Dictionary<string,JsonElement>(a.Properties){["session-guard"]=JsonSerializer.SerializeToElement(value)}}).ToArray()};
        var kept=Catalog(1);var changed=Catalog(2);
        var good=new MetadataRegionalDesignOption("preserve",2,region.Recipe,kept);
        var bad=new MetadataRegionalDesignOption("change",0,region.Recipe,changed);
        var source=new MetadataRegionalDesignRequest(1,"protected-design","Preserve game-authored input across program choices","test:protected-design",template,
            [new("arrival",[good])],[new("role","arrival",kept.Archetypes[root].Kind,1,1)]);
        var generator=new WorldMetadataGenerator();var session=await MetadataRegionalGenerationSession.StartDesignedAsync(generator,source);
        var origin=session.Snapshot.World.Definition.Regions.Single(r=>r.Id=="arrival").Recipe.Origin!;
        var moved=new MetadataRegionalDesignOption("move-origin",1,region.Recipe,kept,new(region.Weight,region.MinimumArea,region.MinimumClearance,
            region.Profile,region.PlacementAnchorId,region.Capacity,region.SpatialContext,new("Declared alternative origin","test:move-origin",new WorldPoint(origin[0]+1,origin[1]))));
        MetadataRegionalDesignEditRequest Edit(string id,MetadataRegionalDesignRequest request,MetadataRegionalProtectionChange? change=null)
        {
            var before=session.Snapshot;return new(id,"Revise complete design","test:protected-design-edit",before.World.DefinitionSha256,
                before.Report.WorldSha256,before.DesignSourceSha256,request,change);
        }
        var guard=new MetadataRegionalInputProtection("guard",MetadataRegionalInputSource.Catalog,"arrival",
            $"/Archetypes/{root}/Properties/session-guard","Keep the accepted game property","test:guard");
        var anchor=new MetadataRegionalAnchorProtection("anchor-guard","arrival","Keep the accepted region origin","test:anchor-guard");
        await session.ApplyDesignAsync(Edit("protect",source,new(session.Snapshot.Report.EditId,[],[guard],[anchor])));
        var alternatives=source with{Slots=[new("arrival",[bad,moved,good])]};
        var accepted=await session.ApplyDesignAndPublishAsync(Edit("alternatives",alternatives),Path.Combine(output,"protected"));
        if(accepted.Report.DesignAttempts is not {Count:3} attempts||attempts[0].Accepted||
            attempts[0].Rejection?.StartsWith("METADATA_REGIONAL_PROTECTED_INPUT_CONFLICT:",StringComparison.Ordinal)!=true||
            attempts[1].Rejection?.StartsWith("METADATA_REGIONAL_ANCHOR_PROTECTION_CONFLICT:",StringComparison.Ordinal)!=true||
            !attempts[2].Accepted||attempts[2].Selections.Single().OptionId!="preserve")throw new Exception("Protected alternative search did not retain rejection and accepted choice");
        var blocked=alternatives with{Slots=[new("arrival",[bad])]};
        try{await session.ApplyDesignAsync(Edit("blocked",blocked));throw new Exception("Conflicting-only design accepted");}
        catch(MetadataRegenerationException){}
        if(Json(session.Snapshot)!=Json(accepted))throw new Exception("Conflicting edit changed accepted design");
        var released=await session.ApplyDesignAndPublishAsync(Edit("release",blocked,new(accepted.Report.EditId,["guard","anchor-guard"],[],[])),Path.Combine(output,"released"));
        if(released.Report.Protections is not null||released.Report.DesignAttempts is not {Count:1}||
            released.World.Definition.OwnershipPlan!.Source.Regions.Single(r=>r.Id=="arrival").Catalog.Archetypes[root].Properties["session-guard"].GetInt32()!=2)
            throw new Exception("Explicit protection release did not allow the changed complete program");
        foreach(var world in new[]{accepted.World,released.World})
            if(world.Regions.Sum(r=>r.World.Nodes.Count)!=20||world.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)!=12||
                world.Connections.Count!=3||world.Connections.Any(c=>c.Forward.Path is null||c.Reverse?.Path is null))throw new Exception("Protected design lost complete physical scope");
        var restored=await MetadataRegionalGenerationSession.RestoreAsync(generator,Path.Combine(output,"released/generation-session.json"));
        if(Json(restored.Snapshot)!=Json(released))throw new Exception("Protected design attempts and releases did not replay");
        Console.WriteLine("PASS: protected design search retains catalog and anchor conflicts, selects the compatible third choice, preserves failed state, allows explicit release and replays complete history.");
    }
}
