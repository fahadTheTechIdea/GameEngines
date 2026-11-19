using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;

internal static class WorldRegionFacingTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions(true));
    private static void Check(bool value,string message){if(!value)throw new Exception("Region facing: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    private static WorldMetadataRecipe Face(WorldMetadataRecipe recipe,MetadataPopulationFacing facing)=>recipe with{Placement=recipe.Placement! with{
        Bindings=recipe.Placement.Bindings.Select(b=>b.DemandId=="resources"?b with{Population=b.Population! with{Facing=facing}}:b).ToArray()}};
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        Directory.CreateDirectory(output);var generator=new WorldMetadataGenerator();
        foreach(bool local in new[]{false,true})
        {
            var input=WorldRegionPopulationTests.Example(local:local);
            foreach(var facing in new[]{MetadataPopulationFacing.Along,MetadataPopulationFacing.Against,MetadataPopulationFacing.Left,MetadataPopulationFacing.Right})
            {
                var recipe=Face(input.Recipe,facing);var world=generator.Generate(recipe,input.Catalog);var plan=world.PlacementPlan!;
                var trace=plan.PopulationOrientations!.Single();double offset=facing switch{MetadataPopulationFacing.Along=>0,MetadataPopulationFacing.Against=>180,MetadataPopulationFacing.Left=>90,_=>-90};
                Check(trace.NodeIds.Count==4&&trace.TangentDegrees.All(v=>v==180),"four region band headings");
                double expected=((180+30+offset+180)%360+360)%360-180;
                Check(trace.Rotations.All(r=>Math.Abs(r-expected)<1e-9),"declared facing and source rotation");
                var resource=input.Catalog.Archetypes.Single(a=>a.Id=="resource").PhysicalFootprint!;
                var footprints=plan.Footprints.Concat(plan.NestedStages.SelectMany(s=>s.Footprints)).ToDictionary(f=>f.NodeId);
                var bindings=plan.Definition.Bindings.Concat((plan.Definition.NestedStages??[]).SelectMany(s=>s.Bindings)).ToDictionary(b=>b.NodeId);
                double angle=expected*Math.PI/180,c=Math.Cos(angle),s=Math.Sin(angle);
                for(int i=0;i<trace.NodeIds.Count;i++){
                    string id=trace.NodeIds[i];Check(Math.Abs(bindings[id].RotationDegrees-expected)<1e-9,"physical binding rotation");
                    for(int j=0;j<resource.Polygon.Vertices.Count;j++){
                        var v=resource.Polygon.Vertices[j];var actual=footprints[id].Polygon.Vertices[j];
                        Check(actual.DistanceTo(new WorldPoint(v.X*c-v.Y*s,v.X*s+v.Y*c))<1e-9,"source-sized asymmetric footprint rotates exactly");
                    }
                }
                WorldMetadataGenerator.ValidateDocument(world);
                var changed=trace with{TangentDegrees=trace.TangentDegrees.Select((v,i)=>i==0?v+1:v).ToArray()};
                Reject(()=>WorldMetadataGenerator.ValidateDocument(world with{PlacementPlan=plan with{PopulationOrientations=[changed]}}),"PLAN_MISMATCH");
                string name=(local?"local-":"region-")+facing.ToString().ToLowerInvariant();
                string path=Path.Combine(output,name+".json");await File.WriteAllTextAsync(path,Json(world));
                if(facing==MetadataPopulationFacing.Along){
                    var loaded=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(path);
                    Check(Json(loaded)==Json(world),"persisted complete region-facing world replays");
                }
            }
            Reject(()=>generator.Generate(Face(input.Recipe,MetadataPopulationFacing.Inside),input.Catalog),"METADATA_POPULATION_FACING_INVALID");
        }
        Console.WriteLine("PASS: generated regions and local allocations orient all four complete physical members across four facing modes; source-sized footprints and replay/tamper checks pass.");
    }
}
