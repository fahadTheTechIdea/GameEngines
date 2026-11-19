using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldRegionalInteriorJourneyTests
{
    static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    static void Check(bool value,string reason){if(!value)throw new Exception("Regional interior journey: "+reason);}
    static void Reject(Action run,string code){try{run();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");
        var interior=WorldEntranceTravelTests.Example();var baseRecipe=interior.Recipe with{Surface=null,Origin=null};
        var noEntrance=baseRecipe with{Interiors=baseRecipe.Interiors! with{Rules=baseRecipe.Interiors.Rules.Select(r=>r with{Entrances=null}).ToArray()}};
        var properties=new Dictionary<string,JsonElement>();
        var otherCatalog=new WorldMetadataCatalog(1,"outside-catalog",[new("root","location","Other destination",properties,[],[],[])]);
        var otherRecipe=new WorldMetadataRecipe(1,"outside-root","framework","outside-catalog","root","region",MetadataCoverage.PartialScope,
            ["Framework test source"],new Dictionary<string,int>(),new(["x","y"],"meters"),Origin:null,RequireAllPositions:true,Environment:baseRecipe.Environment);
        var boundary=Box(-40,-40,160,80);var surface=new WorldSurfaceDefinition(1,"shared-deck","meters",boundary,[],
            [new("deck","deck","Declared walkable surface","test:deck",boundary,0,new(0,0),0,0,0)]);
        var suitability=new WorldSuitabilityProfile("deck","Select available surface","test:deck",["deck"],[],[]);
        var profile=baseRecipe.Interiors!.Rules[0].Profile;
        var connection=new MetadataRegionalConnection("outside-link","regional-link","Connect exterior destination","test:regional",
            new("building","generated-interior/trail-1"),new("outside","outside-root"),"building",boundary,profile,
            new(.1,[],MaximumWork:100000000,Objective:WorldRouteObjective.FirstFeasible),true);
        var regions=new[]{
            new MetadataRegionalProgramRegion("building","Complete interior program","test:building",1,100,1,suitability,"regional-anchor",baseRecipe,interior.Catalog,
                PinnedAnchor:new("Preserve entry program anchor","test:pin",new(0,0))),
            new MetadataRegionalProgramRegion("outside","Other region","test:outside",1,100,1,suitability,null,otherRecipe,otherCatalog,
                PinnedAnchor:new("Place outside destination","test:pin",new(80,0)))};
        var template=new MetadataRegionalOwnershipRequest(1,"interior-world","Connect interior and exterior","test:world",surface,regions,[connection],
            new(Interior:new(.1,65536),PartitionSearch:new([0,90])),MaximumWork:200000000);
        var journey=new MetadataRegionalJourneyRule("journey","Travel from room to other region and back","test:journey",
            new("building","generated-interior/rooms-5"),new("outside","outside-root"),["door","entry","regional","regional-link"],profile,true);
        var request=new MetadataRegionalDesignRequest(1,"interior-selection","Select a usable exterior entrance","test:design",template,
            [new("building",[new("closed",0,noEntrance,interior.Catalog),new("open",1,baseRecipe,interior.Catalog)])],[],2,WorldJourneys:[journey]);
        var engine=new MetadataRegionalDesignGenerator();var result=engine.Generate(request);var attempts=result.Receipt.Attempts;
        Check(attempts.Count==2&&!attempts[0].Accepted&&attempts[0].Rejection=="METADATA_REGIONAL_JOURNEY_UNREACHABLE:journey"&&
            attempts[1].Accepted&&attempts[1].Selections.Single().OptionId=="open","closed entrance rejects; declared opening succeeds");
        var world=result.World;var local=world.Regions.Single(r=>r.RegionId=="building").World;
        var proof=attempts[1].WorldJourneys!.Journeys.Single();var inside=local.InteriorPlan!.Results.Single();
        Check(inside.Openings.Count==6&&inside.Openings.Count(o=>o.Entrance is not null)==1&&world.Connections.Count==1&&
            world.Connections[0].Forward.Path is not null&&world.Connections[0].Reverse?.Path is not null,"all room openings and regional directions retained");
        Check(proof.Forward.Any(s=>s.ConnectionId.EndsWith("/explicit/entry",StringComparison.Ordinal))&&
            proof.Forward.Any(s=>inside.Routes.Any(r=>r.ConnectionId==s.ConnectionId))&&proof.Forward.Any(s=>s.RegionId is null)&&
            proof.Return!.Any(s=>s.ConnectionId.EndsWith("/explicit/entry",StringComparison.Ordinal)),"actual paths cross declared entrance, interior doors and regional connection");
        void Continuous(IReadOnlyList<MetadataRegionalJourneyStep> steps){for(int i=1;i<steps.Count;i++)Check(steps[i-1].To==steps[i].From,"connected source route segments");}
        Continuous(proof.Forward);Continuous(proof.Return!);
        var planner=new MetadataRegionalJourneyPlanner();
        Reject(()=>planner.Plan([journey with{From=new("building","generated-interior/rooms-0/fixture-0")}],world),"METADATA_REGIONAL_JOURNEY_NODE_ANCHOR_REQUIRED");
        Reject(()=>planner.Plan([journey with{MaximumWork=1}],world),"METADATA_REGIONAL_JOURNEY_WORK_BUDGET");
        var forwardOnly=inside with{Routes=inside.Routes.Select(r=>r.ConnectionId.EndsWith("/explicit/entry",StringComparison.Ordinal)?r with{Reverse=null}:r).ToArray()};
        var oneWayLocal=local with{InteriorPlan=local.InteriorPlan! with{Results=[forwardOnly]}};
        var oneWayWorld=world with{Regions=world.Regions.Select(r=>r.RegionId=="building"?r with{World=oneWayLocal}:r).ToArray()};
        Reject(()=>planner.Plan([journey],oneWayWorld),"METADATA_REGIONAL_JOURNEY_UNREACHABLE");
        var missingOpening=inside with{Openings=inside.Openings.Where(o=>o.Entrance is null).ToArray()};
        var missingLocal=local with{InteriorPlan=local.InteriorPlan! with{Results=[missingOpening]}};
        var missingWorld=world with{Regions=world.Regions.Select(r=>r.RegionId=="building"?r with{World=missingLocal}:r).ToArray()};
        Reject(()=>planner.Plan([journey],missingWorld),"METADATA_REGIONAL_JOURNEY_INTERIOR_OPENING_REQUIRED");
        Directory.CreateDirectory(output);await File.WriteAllTextAsync(Path.Combine(output,"request.json"),Json(request));
        await engine.PublishAsync(result,Path.Combine(output,"world"));
        Check(Json(await engine.ReadAsync(Path.Combine(output,"world")))==Json(result),"interior, exterior and complete journey replay");
        Console.WriteLine("PASS: closed interior entrance rejects; declared opening connects room, exterior and regional destination in both directions.");
    }
}
