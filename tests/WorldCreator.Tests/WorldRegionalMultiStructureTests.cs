using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalMultiStructureTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Regional multi-structure: "+reason);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var template=WorldRegionalNetworkTests.Example();var region=template.Regions.Single(r=>r.Id=="arrival");
        var root=region.Catalog.Archetypes.Single(a=>a.Id==region.Recipe.RootArchetypeId);
        var compound=region.Catalog.Archetypes.Single(a=>a.Id=="compound");
        WorldMetadataCatalog Catalog(int count)
        {
            var extra=compound with{Id="secondary-compound",Parts=compound.Parts.Select(p=>p.Id=="modules"?p with{Count=count}:p).ToArray(),
                ChildRequirements=compound.ChildRequirements.Select(c=>c.Kind=="module"?c with{Minimum=count,Maximum=count}:c).ToArray()};
            return region.Catalog with{Archetypes=[..region.Catalog.Archetypes.Select(a=>a.Id!=root.Id?a:a with{
                Parts=[..a.Parts,new("secondary","secondary-compound",1,"Second required complete structure")],
                ChildRequirements=a.ChildRequirements.Select(c=>c.Kind=="compound"?c with{Minimum=2,Maximum=2}:c).ToArray()}),extra]};
        }
        WorldMetadataRecipe Recipe(bool complete)
        {
            var original=region.Recipe;var placement=original.Placement!;var req=placement.Request;var first=req.Demands[0];
            var second=first with{Id="secondary",Purpose="Second physical compound"};
            var binding=new MetadataPlacementBinding("secondary",original.Id+"/secondary-0",true);
            var local=placement.Programs!.Single();var extra=local with{Id="secondary-modules",ParentArchetypeId="secondary-compound"};
            var amended=placement with{Request=req with{Execution=WorldPlacementExecution.Search,Demands=[first,second],
                Growth=new("compound-growth","Arrange related structures within their region","test:compound-arrangement","compound",region.PlacementAnchorId!,
                    [new("compound-neighbor","compound","secondary","Place the related structures together","test:compound-arrangement")],
                    [0,90,180,270],3,30)},
                Bindings=[..placement.Bindings,binding],Programs=[local,extra]};
            var links=complete?original.AdditionalConnections!.Concat([new MetadataExplicitConnection("secondary-local","local-walk",
                original.Id+"/secondary-0/modules-0",original.Id+"/secondary-0/modules-1",true)]).ToArray():original.AdditionalConnections;
            return original with{Placement=amended,AdditionalConnections=links};
        }
        var wrong=Recipe(false);var complete=Recipe(true);
        var requirement=new MetadataRegionalDesignRequirement("complete-each","arrival","module",2,2,true,new("compound",2,2,true),"module");
        var request=new MetadataRegionalDesignRequest(1,"multi-structure-design","Select complete related game structures","test:multi",template,
            [new("arrival",[new("missing-part",0,wrong,Catalog(1)),new("complete",1,complete,Catalog(2))])],[requirement],2);
        var designer=new MetadataRegionalDesignGenerator();var result=designer.Generate(request);
        Check(result.Receipt.Attempts.Count==2&&!result.Receipt.Attempts[0].Accepted&&result.Receipt.Attempts[1].Accepted,"incomplete parent triggers complete alternative");
        var first=result.Receipt.Attempts[0].Counts.Single();var accepted=result.Receipt.Attempts[1].Counts.Single();
        Check(first.Scopes is {Count:2}&&first.Scopes.Any(s=>s.Count==1&&!s.Satisfied)&&first.Scopes.Any(s=>s.Count==2&&s.Satisfied),"first generated alternative exposes the incomplete parent");
        Check(accepted.Scopes is {Count:2}&&accepted.Scopes.All(s=>s.Count==2&&s.Satisfied),"each physical parent has both parts");
        var selected=result.World.Regions.Single(r=>r.RegionId=="arrival").World;
        Check(result.World.Regions.Count==4&&result.World.Regions.Sum(r=>r.World.Nodes.Count)==23&&result.World.Connections.Count==3&&
            result.World.Connections.All(c=>c.Forward.Path is not null&&c.Reverse?.Path is not null),"all regional programs and cross-region return journeys retained");
        Check(selected.AccessPlan!.Sites.Count==6&&selected.RoutePlan!.Routes.Count==2&&selected.RoutePlan.Routes.All(r=>r.Forward.Path is not null&&r.Reverse?.Path is not null),
            "both compounds, four parts and both local return journeys retained");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await designer.PublishAsync(result,Path.Combine(output,"world"));
        Check(Json(await designer.ReadAsync(Path.Combine(output,"world")))==Json(result),"selected complete world and per-parent witness replay");
        Console.WriteLine("PASS: two physical parents in one generated region, per-parent fallback, complete local and cross-region journeys.");
    }
}
