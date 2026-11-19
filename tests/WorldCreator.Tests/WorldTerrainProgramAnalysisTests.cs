using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldTerrainProgramAnalysisTests
{
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x,WorldJson.CreateOptions());
    private static void Check(bool b,string m){if(!b)throw new Exception("Terrain program analysis: "+m);}
    private static void Near(double a,double b,string m)=>Check(Math.Abs(a-b)<=1e-7*Math.Max(1,Math.Abs(b)),m+": "+a+" / "+b);
    private static void Reject(Action a,string code){try{a();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing terrain-analysis refusal: "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve existing output");Directory.CreateDirectory(output);
        var generator=new WorldMetadataGenerator();MetadataProgramAnalysis? baseline=null;WorldMetadataDocument? prior=null;double baselineArea=0;
        foreach(var (name,richer,larger) in new[]{("baseline",false,false),("richer",true,false),("larger",false,true)})
        {
            var e=WorldTerrainDemandTests.Example(richer);
            if(larger)e=(e.Recipe,e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="feature"?a with{PhysicalFootprint=a.PhysicalFootprint! with{
                Polygon=Scale(a.PhysicalFootprint.Polygon,1.1),Reservation=Scale(a.PhysicalFootprint.Reservation!,1.1)}}:a).ToArray()});
            string before=Json(e);var analysis=generator.AnalyzeProgram(e.Recipe,e.Catalog);
            Check(Json(e)==before,"captured caller input remains unchanged");
            var world=generator.Generate(e.Recipe,e.Catalog);
            await File.WriteAllTextAsync(Path.Combine(output,name+"-analysis-attempt.json"),Json(analysis));
            await File.WriteAllTextAsync(Path.Combine(output,name+"-world-attempt.json"),Json(world));Same(analysis,world);
            var counts=richer?new[]{110,129,2}:new[]{100,117,2};int members=counts.Sum(),nodes=members+29;
            Check(world.Nodes.Count==nodes&&world.AccessPlan!.Sites.Count==nodes-1&&analysis.SourceProgram!.Nodes.Count==29,"complete original compounds and every terrain-derived member");
            Check(world.RoutePlan!.Routes.Count==19&&world.RoutePlan.Routes.All(r=>r.Reverse is not null),"all nineteen original two-way journeys retained");
            var regionIds=new[]{"lowland","highland","woodland"};
            Check(regionIds.Select(id=>analysis.TerrainAnalysis!.Plan.Results.Single(r=>r.RuleId==id).Count).SequenceEqual(counts),"known full-habitat integer counts by declared region");
            var access=e.Catalog.Archetypes.Where(a=>a.Access is not null).ToDictionary(a=>a.Id,a=>a.Access!);
            var roots=new MetadataPlacementPlanner().RegionSources(e.Recipe.Placement!,analysis.Nodes,e.Recipe.Space.Unit,analysis.FootprintDefinitions,analysis.StructureDefinitions,access,new(1000000,default),default,unplaced:true);
            var envelopes=MetadataRegionPlanner.MeasureEnvelopes(roots.Select(r=>r.Source),new(1000000,default));double area=envelopes.Sum(s=>s.EnvelopeArea);
            Check(envelopes.Count(s=>analysis.Nodes.Single(n=>n.Id==s.Source.NodeId).ArchetypeId=="feature")==members,"every required habitat feature contributes its root envelope once");
            if(baseline is null){baseline=analysis;prior=world;baselineArea=area;}
            else
            {
                Check(analysis.Nodes.Count>=baseline.Nodes.Count,"no required members removed by density or larger sources");
                if(larger)Near(area-baselineArea,219*(.36*1.21-.36),"source-size change reaches actual unplaced area demand");
                else Check(area>baselineArea&&analysis.TerrainAnalysis!.HabitatSha256!=baseline.TerrainAnalysis!.HabitatSha256,"richer measured habitat enlarges complete content demand and provenance");
            }
            string folder=Path.Combine(output,name);await generator.PublishAsync(e.Recipe,e.Catalog,world,folder,null,default);
            await File.WriteAllTextAsync(Path.Combine(folder,"program-analysis.json"),Json(analysis));
            await File.WriteAllTextAsync(Path.Combine(folder,"root-envelopes.json"),Json(envelopes));
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(folder,"world-metadata.json")))==Json(world),"saved actual terrain/count/placement replay");
        }
        await Context(output,generator);Refusals(generator);Mutation();
        await Lifecycle(output,generator);
        Console.WriteLine("PASS: resolved terrain program analysis with complete248/270-node worlds,219/241 demanded members, original source geometry and nineteen round trips, measured root envelopes, source/context/input parity, exact work/refusals and source-history replay.");
    }
    private static WorldPolygon Scale(WorldPolygon p,double factor)=>new(p.Vertices.Select(v=>new WorldPoint(v.X*factor,v.Y*factor)).ToArray());
    private static void Same(MetadataProgramAnalysis a,WorldMetadataDocument w)
    {
        Check(Json(a.Nodes.Select(n=>n with{Position=null}))==Json(w.Nodes.Select(n=>n with{Position=null})),"actual complete members match analysis");
        Check(a.Nodes.Where(n=>n.ParentId is not null).All(n=>n.Position is null),"consumer members remain unplaced; authored root origin is retained");
        var ids=a.Connections.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        Check(Json(a.Connections)==Json(w.Connections.Where(c=>ids.Contains(c.Id)))&&Json(a.CapacityPlan)==Json(w.CapacityPlan)&&Json(a.VariationPlan)==Json(w.VariationPlan)&&Json(a.ProgramMixPlan)==Json(w.ProgramMixPlan),"same complete choices and source links");
        Check(Json(a.FootprintDefinitions)==Json(w.FootprintDefinitions??new Dictionary<string,MetadataCatalogFootprint>())&&Json(a.StructureDefinitions)==Json(w.StructureDefinitions),"all source geometry retained");
        Check(Json(a.TerrainAnalysis!.Plan with{SourceRecipeSha256=w.TerrainDemandPlan!.SourceRecipeSha256})==Json(w.TerrainDemandPlan),"same complete measured habitat/count plan; selected physical recipe identity remains distinct");
        var source=w.PlacementPlan!.Definition.SourceWorld!;
        var habitat=a.TerrainAnalysis.Source;
        Check(Json(new{habitat.Surface,habitat.Environment,habitat.Regions,habitat.LocalLayouts})==Json(new{source.Surface,Environment=source.EnvironmentPlan!.Definition,Regions=source.RegionPlan,LocalLayouts=source.LocalLayoutPlan})&&a.TerrainAnalysis.PreparationWork>0,"exact prepared habitat and reported preparation work");
        Check(a.SourceProgram!.RecipeSha256==habitat.RecipeSha256&&a.SourceProgram.CatalogSha256==source.CatalogSha256&&w.TerrainDemandPlan.SourceRecipeSha256==source.RecipeSha256,"declared and actual source identities remain explicit");
        Check(MetadataTerrainDemandPlanner.MatchesAnalysis(a.TerrainAnalysis,w),"native complete habitat/measurement binding");
        Check(source.Nodes.All(n=>Json(n)==Json(w.Nodes.Single(m=>m.Id==n.Id)))&&Json(source.RoutePlan?.Routes)==Json(w.RoutePlan?.Routes),"all original source members and paths remain exact");
    }
}
