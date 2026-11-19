using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionDemandTests
{
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v,WorldJson.CreateOptions());
    private static void Check(bool value,string message){if(!value)throw new Exception("Region demand: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6,message+": "+a+" / "+b);
    private static void Reject(Action run,string prefix){try{run();}catch(ArgumentException error)when(error.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool enlarged=false)
    {
        var source=WorldRegionPartitionTests.Example();var recipe=JsonSerializer.Deserialize<WorldMetadataRecipe>(Json(source.Recipe)
            .Replace("compounds-0","first-0",StringComparison.Ordinal).Replace("compounds-1","second-0",StringComparison.Ordinal).Replace("compounds-2","third-0",StringComparison.Ordinal),WorldJson.CreateOptions())!;
        var compound=source.Catalog.Archetypes.Single(a=>a.Id=="compound");double half=enlarged?9:6;
        var wide=compound with{Id="wide",PhysicalFootprint=compound.PhysicalFootprint! with{Polygon=Box(-half,-5,2*half,10)},
            Access=compound.Access! with{Ports=compound.Access.Ports.Select(p=>p with{BoundaryPosition=new(p.Id=="east"?half:-half,0),ApproachPosition=new(p.Id=="east"?half+1:-half-1,0)}).ToArray()}};
        var catalog=source.Catalog with{Archetypes=source.Catalog.Archetypes.Select(a=>a.Id!="root"?a:a with{Parts=[new("first","compound",1,"Complete first program"),
            new("second","wide",1,"Complete second program"),new("third","compound",1,"Complete third program"),a.Parts.Single(p=>p.Id=="markers")]}).Append(wide).ToArray()};
        var program=recipe.Placement!.Programs!.Single();
        recipe=recipe with{Regions=recipe.Regions! with{Intents=recipe.Regions.Intents!.Select(i=>i with{MinimumArea=1,MinimumClearance=0}).ToArray()},
            Placement=recipe.Placement with{Programs=[program with{MinimumParents=2,MaximumParents=2},program with{Id="wide-modules",ParentArchetypeId="wide",MinimumParents=1,MaximumParents=1}]},
            RegionDemands=new(1,"program-capacity","Derive all region minima from complete program envelopes","demand:regions",
                new[]{"alpha","beta","gamma"}.Select(id=>new MetadataRegionDemandRule(id,"Room for complete "+id+" program","demand:"+id,id,.12)).ToArray())};
        return(recipe,catalog);
    }
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var input=Example();var changed=Example(true);var generator=new WorldMetadataGenerator();
        var world=generator.Generate(input.Recipe,input.Catalog);var larger=generator.Generate(changed.Recipe,changed.Catalog);
        foreach(var w in new[]{world,larger})
        {
            Check(w.Nodes.Count==11&&w.AccessPlan!.Sites.Count==9&&w.RegionPlan!.Regions.Count==3,"all complete required programs retained");
            Check(w.RegionDemandPlan!.Results.Sum(r=>r.Sources.Count)==3,"root envelopes counted once without six contained modules");
            foreach(var result in w.RegionDemandPlan.Results)
            {
                Near(result.RequiredArea,result.EnvelopeArea/.12,"coverage-derived minimum");
                var intent=w.RegionPlan!.Request.Intents!.Single(i=>i.Id==result.Rule.RegionId);
                Near(intent.MinimumArea,result.RequiredArea,"region binds source-derived minimum");
                Near(intent.MinimumClearance,result.RequiredClearance,"region binds circular source bound");
            }
            WorldMetadataGenerator.ValidateDocument(w);
        }
        var small=world.RegionDemandPlan!.Results.Single(r=>r.Rule.RegionId=="beta");var large=larger.RegionDemandPlan!.Results.Single(r=>r.Rule.RegionId=="beta");
        Near(small.EnvelopeArea,120,"original program source area");Near(large.EnvelopeArea,180,"larger program source area");Near(large.RequiredArea,1500,"required space grows with source");
        Check(larger.RegionPlan!.Regions.Single(r=>r.Id=="beta").Area>world.RegionPlan!.Regions.Single(r=>r.Id=="beta").Area,"physical source edit enlarges derived region");
        Check(Json(world.Nodes.Select(n=>n with{Position=null}))==Json(larger.Nodes.Select(n=>n with{Position=null})),"source edit preserves every content identity");
        await BoundariesAsync(output,input,world);
        await File.WriteAllTextAsync(Path.Combine(output,"recipe.json"),Json(input.Recipe));await File.WriteAllTextAsync(Path.Combine(output,"catalog.json"),Json(input.Catalog));
        await File.WriteAllTextAsync(Path.Combine(output,"larger-catalog.json"),Json(changed.Catalog));
        await generator.GenerateFilesAsync(Path.Combine(output,"recipe.json"),Path.Combine(output,"catalog.json"),Path.Combine(output,"world"));
        var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var before=session.Snapshot;
        var after=await session.ApplyAndPublishAsync(new("larger-source","Keep all members and grow required region","demand:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed.Recipe,changed.Catalog),Path.Combine(output,"larger"));
        Check(Json(after.World)==Json(larger),"incremental source edit equals full clean generation");
        Check(after.Report.Stages.Any(s=>s.Id=="region-demands"&&!s.Reused)&&after.Report.Stages.Any(s=>s.Id=="regions"&&!s.Reused),"catalog edit invalidates source-demand and geometry stages");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"larger/generation-session.json"));Check(Json(restored.Snapshot)==Json(after),"saved source demand and full history replay");
        var invalid=changed.Recipe with{RegionDemands=changed.Recipe.RegionDemands! with{Rules=changed.Recipe.RegionDemands.Rules.Select(r=>r with{MaximumCoverage=.001}).ToArray()}};
        try{session.Apply(new("capacity-refusal","Keep complete declared demand","demand:refusal",after.World.RecipeSha256,after.World.CatalogSha256,invalid,changed.Catalog));throw new Exception("Missing capacity refusal");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(after),"infeasible source demand leaves accepted state exact");
        var composed=generator.Generate(input.Recipe with{ProgramComposition=new("compose","Qualify complete region capacity","demand:compose")},input.Catalog);
        Check(composed.ProgramCompositionPlan!.RegionWork>=composed.RegionPlan!.Work+composed.RegionDemandPlan!.Work,"composition includes actual preparation work");WorldMetadataGenerator.ValidateDocument(composed);
        await CompositionAsync(output);
        Console.WriteLine("PASS: catalog-source region demand, complete11-node/9-source/3-region pipeline, larger source grows derived region, source/proof replay, cache invalidation, history and failed-edit preservation, bounded measurement and composition accounting.");
    }
}
