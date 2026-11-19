using WorldCreator.Core.Metadata;

internal static partial class WorldRegionDemandTests
{
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) CompositionExample()
    {
        var input=Example();var wide=input.Catalog.Archetypes.Single(a=>a.Id=="wide");
        var capacity=new MetadataCatalogCapacity("program","units",1,"Complete required program","demand:program");
        var rule=new MetadataProgramMixRule("options","Select complete source-sized program","demand:options",
            [new("huge",1,MaximumCount:1),new("wide",2,MaximumCount:1)],[new("program","units",1)],"units",2,1);
        var catalog=input.Catalog with{Archetypes=input.Catalog.Archetypes.Select(a=>a.Id=="wide"?a with{Capacities=[capacity]}:
            a.Id=="root"?a with{Parts=a.Parts.Select(p=>p.Id=="second"?p with{ProgramMix=rule}:p).ToArray()}:a)
            .Append(wide with{Id="huge",Capacities=[capacity],PhysicalFootprint=wide.PhysicalFootprint! with{Polygon=Box(-150,-5,300,10)},
                Access=wide.Access! with{Ports=wide.Access.Ports.Select(p=>p with{BoundaryPosition=new(p.Id=="east"?150:-150,0),ApproachPosition=new(p.Id=="east"?151:-151,0)}).ToArray()}}).ToArray()};
        var program=input.Recipe.Placement!.Programs!.Single(p=>p.ParentArchetypeId=="wide");
        var recipe=input.Recipe with{ProgramComposition=new("compose","Keep every complete program while selecting feasible sources","demand:compose"),
            Placement=input.Recipe.Placement with{Programs=input.Recipe.Placement.Programs!.Select(p=>p.ParentArchetypeId=="wide"?p with{MinimumParents=0}:p)
                .Append(program with{Id="huge-contents",ParentArchetypeId="huge",MinimumParents=0}).ToArray()}};
        return(recipe,catalog);
    }
    private static async Task CompositionAsync(string output)
    {
        var(recipe,catalog)=CompositionExample();
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,catalog);var proof=world.ProgramCompositionPlan!;
        Check(proof.Attempts.Count==2&&!proof.Attempts[0].Accepted&&proof.Attempts[0].Rejection=="WORLD_REGION_INTENT_UNSATISFIED"&&proof.Attempts[1].Accepted,"source-derived infeasibility advances complete program choice");
        Check(proof.Attempts.All(a=>a.RegionRequested==true)&&proof.Selections==4,"failed and accepted regional requests remain counted");
        Check(world.Nodes.Count==11&&world.AccessPlan!.Sites.Count==9&&world.Nodes.Single(n=>n.Id.EndsWith("/second-0",StringComparison.Ordinal)).ArchetypeId=="wide","selection preserves every nested member");
        Check(proof.RegionWork>world.RegionPlan!.Work+world.RegionDemandPlan!.Work,"composition charges both measured attempts");
        WorldMetadataGenerator.ValidateDocument(world);
        Reject(()=>generator.Generate(recipe with{ProgramComposition=recipe.ProgramComposition! with{MaximumAttempts=1}},catalog),"METADATA_PROGRAM_COMPOSITION_ATTEMPT_BUDGET");
        await File.WriteAllTextAsync(Path.Combine(output,"composed-recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(output,"composed-catalog.json"),Json(catalog));
        await generator.GenerateFilesAsync(Path.Combine(output,"composed-recipe.json"),Path.Combine(output,"composed-catalog.json"),Path.Combine(output,"composed"));
    }
}
