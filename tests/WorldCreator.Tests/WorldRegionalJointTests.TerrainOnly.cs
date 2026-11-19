using System.Text.Json;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionalJointTests
{
    internal static MetadataRegionalOwnershipRequest TerrainExample(bool larger=false,bool exclusion=false)
    {
        const string id="joint-terrain";var original=Example();var profile=new WorldSuitabilityProfile("ground","Required game ground","joint:ground",["ground"],[],[]);var properties=new Dictionary<string,JsonElement>();
        var root=new WorldMetadataArchetype("root","world","Whole source habitat",properties,[],[],[]);
        var sourceCatalog=new WorldMetadataCatalog(1,"joint-terrain-catalog",[root]);
        var fields=new WorldEnvironmentDefinition(1,"fields",[new("constant","Game-authored neutral field","joint:field","relative",1,[])]);
        var source=new WorldMetadataRecipe(1,id,"framework-reference",sourceCatalog.Id,"root","habitat",MetadataCoverage.PartialScope,
            ["Complete declared technical terrain/content/journey control; broad professional-world design remains open."],new Dictionary<string,int>(),new(["x","y"],"meters"),
            Environment:fields,Regions:new("habitat","Actual suitable owned habitat","joint:habitat",null!,profile,[],Intents:[new("habitat","Required owned habitat","joint:habitat",1)]));
        var types=original.Regions[0].Catalog.Archetypes.Where(a=>a.Id is "landmark" or "landmark-alt" or "marker").Select(a=>
            !larger||a.Id!="landmark-alt"?a:a with{PhysicalFootprint=a.PhysicalFootprint! with{Polygon=Box(-1.8,-1.8,3.6,3.6),Reservation=Box(-2.3,-2.3,4.6,4.6)}}).ToArray();
        var mix=original.Regions[0].Catalog.Archetypes.Single(a=>a.Id==original.Regions[0].Recipe.RootArchetypeId).Parts.Single(p=>p.Id=="landmarks").ProgramMix!;
        var catalog=sourceCatalog with{Archetypes=[root with{Parts=[new("landmarks","landmark",1,"Every terrain-required complete discovery",ProgramMix:mix)],
            Connections=[new("chain","walk",MetadataConnectionPattern.Chain,"landmarks")],ChildRequirements=[new("discovery",1,128)],RequireWeaklyConnectedChildren=true},..types]};
        var placement=new MetadataPlacementDefinition(new("discoveries","Allocate all actual habitat discoveries","joint:placement",null,
            [new("landmarks","Complete source-sized discoveries","joint:discovery",null,profile,RegionId:"habitat")],[],[],Execution:WorldPlacementExecution.Prescribed),
            [new("landmarks",id,true,PartId:"landmarks",Population:new("Represent actual suitable owned habitat","joint:population",Region:new("habitat",Area:new(Interior:new(.1,65536)))))],
            Programs:original.Regions[0].Recipe.Placement!.Programs!.Where(p=>p.ParentArchetypeId is "landmark" or "landmark-alt").Select(p=>p with{ScopeNodeId=id,
                Request=p.Request with{Demands=p.Request.Demands.Select(d=>d with{Profile=profile}).ToArray()}}).ToArray());
        var walk=new WorldTraversalProfile("walk","Every required discovery journey and return","joint:walk",.2,.5,new Dictionary<string,double>{{"ground",1}},1,1,1);
        var recipe=source with{Regions=null,RequireAllPositions=true,Placement=placement,PopulationSource=new(source,sourceCatalog,new([],UsePlacementBoundary:true)),
            TerrainDemands=new([new("discoveries","Actual suitable owned habitat funds every discovery","joint:count",id,"landmarks","habitat",300)]),
            ProgramComposition=new("choices","Complete actual habitat programs","joint:choices",MaximumAttempts:64),RouteRules=[new("walk","walk",walk,new(.1,[]))]};
        var boundary=Box(0,0,80,40);var surface=new WorldSurfaceDefinition(1,"terrain","meters",boundary,
            exclusion?[new("protected","Protected terrain changes the actual habitat","joint:protected",Box(4,4,10,20))]:[],
            [new("ground","ground","Whole authored terrain","joint:terrain",boundary,0,new(0,0),0,0,0)]);
        var regions=new[]{"alpha","beta"}.Select(name=>new MetadataRegionalProgramRegion(name,"Complete "+name+" habitat","joint:"+name,1,1,0,profile,"regional-origin",recipe,catalog,
            new("Reserve every complete source program","joint:capacity",.7))).ToArray();
        var connection=new MetadataRegionalConnection("journey","walk","Join both actual game regions","joint:journey",new("alpha",id+"/landmarks-0"),new("beta",id+"/landmarks-0"),"alpha",boundary,walk,new(.1,[]),true);
        return new(1,"joint-terrain-world","Terrain-only sources and complete physical nested content","joint:world",surface,regions,[connection],new(Interior:new(.1,65536)),
            Coupling:new("Converge actual habitat counts and source space","joint:coupling"),ProgramComposition:new("Select complete source programs","joint:choices"));
    }
    internal static async Task RunTerrainAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Preserve previous output");Directory.CreateDirectory(output);
        var planner=new MetadataRegionalOwnershipPlanner();var engine=new MetadataRegionalWorldGenerator();
        foreach(var (name,input) in new[]{("baseline",TerrainExample()),("larger-source",TerrainExample(larger:true)),("terrain-edit",TerrainExample(exclusion:true))})
        {
            await File.WriteAllTextAsync(Path.Combine(output,name+"-request.json"),Json(input));
            var definition=planner.Plan(input);Verify(definition);var world=engine.Generate(definition);
            foreach(var r in world.Regions)
            {
                var w=r.World;int n=w.TerrainDemandPlan!.Results.Single().Count;
                Check(w.Nodes.Count==1+2*n&&w.AccessPlan!.Sites.Count==2*n,"every actual discovery and nested marker retained");
                Check(w.Nodes.Where(n=>n.Kind=="discovery").All(n=>n.ArchetypeId=="landmark-alt"),"larger parent selected without shrinking marker");
                Check(w.RoutePlan!.Routes.Count==n-1&&w.RoutePlan.Routes.All(r=>r.Reverse is not null),"all local discovery return journeys");
                Check(w.PlacementPlan!.Definition.SourceWorld!.PlacementPlan is null&&Json(w.RegionPlan)==Json(w.PlacementPlan.Definition.SourceWorld.RegionPlan),"terrain-only source is inherited exactly without fabricated placement");
                Check(w.ProgramCompositionPlan!.Definition.RequiredRanks!.Count==n+1,"actual full decision topology bound");
            }
            Check(world.Connections.Single().Reverse is not null,"required inter-region return journey");
            await engine.PublishAsync(world,Path.Combine(output,name));
            Check(Json(await engine.ReadWorldAsync(Path.Combine(output,name,"world-regions.json")))==Json(world),"native source and joint trace replay");
            if(name=="baseline")
            {
                AnalysisControls(definition);
                Check(planner.Plan(input with{MaximumWork=definition.OwnershipPlan!.Work}).OwnershipPlan!.Work==definition.OwnershipPlan.Work,"exact work cap");
                Reject(()=>planner.Plan(input with{MaximumWork=definition.OwnershipPlan!.Work-1}),"METADATA_REGIONAL_OWNERSHIP_WORK_BUDGET");
                var limited=Reject(()=>planner.Plan(input with{ProgramComposition=input.ProgramComposition! with{MaximumAttempts=1}}),"METADATA_REGIONAL_JOINT_ATTEMPT_LIMIT");
                await File.WriteAllTextAsync(Path.Combine(output,"limited.json"),Json(limited.Data["RegionalJoint"]));
            }
        }
        Console.WriteLine("PASS: terrain-only joint source/count/program/physical worlds, exact nested decision topology, larger original source, protected-terrain counterfactual, every required member and journey, bounded memo/budgets/dependency/cancel and native replay.");
    }
}
