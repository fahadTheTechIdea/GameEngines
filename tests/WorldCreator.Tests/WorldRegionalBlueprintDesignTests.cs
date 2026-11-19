using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalBlueprintDesignTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException("BLUEPRINT_DESIGN: "+reason);}
    private static void Reject(Action action,string code)
    {try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new InvalidOperationException("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");
        var original=await WorldMetadataGenerator.ReadAsync<MetadataRegionalDesignRequest>("examples/metadata/regional-design/world-seed-a-request.json");
        var prototype=original.Template.Regions[0];var slot=original.Slots.Single();
        foreach(var region in original.Template.Regions)
            Check(Json(region with{Id=prototype.Id,Purpose=prototype.Purpose,SourceRuleId=prototype.SourceRuleId})==Json(prototype),"all base region programs are identical");
        var programs=slot.Options.Select(o=>prototype with{Id=o.Id,Recipe=o.Recipe,Catalog=o.Catalog}).ToArray();
        Check(programs.Any(p=>p.Id=="standard")&&programs.Any(p=>p.Id=="wide"),"two authored program sources are present");
        var blueprint=new MetadataRegionalBlueprint(1,original.Template with{Regions=programs},original.Template.Regions.Select(r=>
            new MetadataRegionalBlueprintRegion(r.Id,r.Purpose,r.SourceRuleId,"standard")).ToArray());
        var choices=original.Slots.Select(s=>new MetadataRegionalBlueprintDesignSlot(s.RegionId,s.Options.Select(o=>
            new MetadataRegionalBlueprintDesignOption(o.Id,o.Priority,o.Id)).ToArray())).ToArray();
        var input=new MetadataRegionalBlueprintDesign(1,original.Id,original.Purpose,original.SourceRuleId,blueprint,choices,original.Requirements,
            original.MaximumAttempts,original.Bindings,original.MaximumBindingWork,original.SelectionSeed,original.SeedPlan,original.MaximumRequirementWork,
            original.WorldRequirements,original.WorldProgression,original.WorldJourneys);
        string before=Json(input);var expanded=MetadataRegionalDesignGenerator.ExpandBlueprintDesign(input);
        Check(Json(expanded)==Json(original),"expansion exactly reconstructs the full existing design and explicit endpoint bindings");
        var bad=input with{Slots=[input.Slots[0] with{Options=[input.Slots[0].Options![0] with{ProgramId="unknown"}]}]};
        Reject(()=>MetadataRegionalDesignGenerator.ExpandBlueprintDesign(bad),"METADATA_REGIONAL_BLUEPRINT_DESIGN_PROGRAM_UNKNOWN");
        var duplicate=input with{Slots=[input.Slots[0],input.Slots[0]]};
        Reject(()=>new MetadataRegionalDesignGenerator().Generate(MetadataRegionalDesignGenerator.ExpandBlueprintDesign(duplicate)),"METADATA_REGIONAL_DESIGN_SLOT_INVALID");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{MetadataRegionalDesignGenerator.ExpandBlueprintDesign(input,cancel.Token);throw new InvalidOperationException("Cancellation ignored");}catch(OperationCanceledException){}}
        Directory.CreateDirectory(output);var other=input with{SeedPlan=input.SeedPlan! with{Seed="world-b"}};
        await File.WriteAllTextAsync(Path.Combine(output,"design-a.json"),Json(input));await File.WriteAllTextAsync(Path.Combine(output,"design-b.json"),Json(other));
        var engine=new MetadataRegionalDesignGenerator();var first=engine.Generate(expanded);var second=engine.Generate(MetadataRegionalDesignGenerator.ExpandBlueprintDesign(other));
        foreach(var result in new[]{first,second})
        {
            Check(result.Receipt.SeedDerivations is {Count:3}&&result.Receipt.Attempts.Last().Accepted,"three declared layers are seeded and the complete program is accepted");
            Check(result.World.Regions.Count==4&&result.World.Regions.Sum(r=>r.World.Nodes.Count)==20&&result.World.Regions.Sum(r=>r.World.AccessPlan!.Sites.Count)==12&&
                result.World.Connections.Count==3&&result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"every declared physical program and two-way connection remains");
        }
        Check(first.Receipt.Attempts.Last().Selections.Single().OptionId!=second.Receipt.Attempts.Last().Selections.Single().OptionId,"fixed world seeds select distinct actual content programs");
        Check(Json(first.World.Definition.Regions.Select(r=>r.Ownership))!=Json(second.World.Definition.Regions.Select(r=>r.Ownership)),"actual region geometry differs");
        Check(!new WorldTerrainGenerator().Generate(first.World.Definition.Surface.Terrain!).Heights.SequenceEqual(
            new WorldTerrainGenerator().Generate(second.World.Definition.Surface.Terrain!).Heights),"actual terrain samples differ");
        // A selected alternative can carry the regional allocation and suitability
        // policy belonging to its program, instead of silently using the base program.
        var selected=second.Receipt.Attempts.Last().Selections.Single().OptionId;
        var changedPrograms=other.Blueprint.Template.Regions.Select(p=>p.Id==selected?p with{Weight=p.Weight*1.5}:p).ToArray();
        var policyInput=other with{Blueprint=other.Blueprint with{Template=other.Blueprint.Template with{Regions=changedPrograms}},
            Slots=other.Slots.Select(s=>s with{Options=s.Options!.Select(o=>o with{UseProgramPolicy=true}).ToArray()}).ToArray()};
        await File.WriteAllTextAsync(Path.Combine(output,"design-policy.json"),Json(policyInput));
        var policyRequest=MetadataRegionalDesignGenerator.ExpandBlueprintDesign(policyInput);
        var invalid=policyRequest with{Slots=[policyRequest.Slots[0] with{Options=[policyRequest.Slots[0].Options[0] with{
            Policy=policyRequest.Slots[0].Options[0].Policy! with{Weight=-1}}]}]};
        Reject(()=>engine.Generate(invalid),"METADATA_REGIONAL_DESIGN_POLICY_INVALID");
        var policyWorld=engine.Generate(policyRequest);
        Check(policyWorld.Receipt.Attempts.Last().Selections.Single().OptionId==selected,"same seed selects same option");
        var selectedRegion=other.Slots.Single().RegionId;
        var actualPolicy=policyWorld.World.Definition.OwnershipPlan!.Source.Regions.Single(r=>r.Id==selectedRegion);
        var previousPolicy=second.World.Definition.OwnershipPlan!.Source.Regions.Single(r=>r.Id==selectedRegion);
        Check(actualPolicy.Weight==changedPrograms.Single(p=>p.Id==selected).Weight&&actualPolicy.Weight!=previousPolicy.Weight,
            "selected program policy reaches the actual ownership planner");
        Check(actualPolicy.Id==previousPolicy.Id&&actualPolicy.Purpose==previousPolicy.Purpose&&actualPolicy.SourceRuleId==previousPolicy.SourceRuleId,
            "the region instance retains its identity and purpose");
        Check(Json(policyWorld.World.Definition.Regions.Select(r=>r.Ownership))!=Json(second.World.Definition.Regions.Select(r=>r.Ownership)),
            "selected weight changes actual ownership geometry");
        Check(policyWorld.World.Connections.Count==3&&policyWorld.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),
            "all required connections survive the alternative policy");
        await engine.PublishAsync(policyWorld,Path.Combine(output,"world-policy"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world-policy")))==Json(policyWorld),"selected policy survives publication and replay");
        Check(Json(input)==before,"caller input is unchanged");
        await engine.PublishAsync(first,Path.Combine(output,"world-a"));await engine.PublishAsync(second,Path.Combine(output,"world-b"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world-a")))==Json(first)&&Json(await engine.ReadAsync(Path.Combine(output,"world-b")))==Json(second),"both complete worlds and seed receipts replay");
        Console.WriteLine("PASS: reusable blueprint alternatives drive actual seeded terrain, regions and complete program selection; both worlds replay.");
    }
}
