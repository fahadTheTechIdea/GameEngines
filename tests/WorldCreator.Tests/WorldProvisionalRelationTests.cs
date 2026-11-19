using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldProvisionalRelationTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Check(bool value,string reason){if(!value)throw new Exception("Provisional relations: "+reason);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.Contains(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool reverse=false,bool backtrack=false)
    {
        var e=WorldRelationCandidateTests.MetadataExample();string id=e.Recipe.Id;
        var profile=e.Recipe.Placement!.Request.Demands[0].Profile;
        var request=e.Recipe.Placement.Request with{Demands=[new("z-first","First complete source","test:first",null,profile),new("y-second","Second complete source","test:second",null,profile),new("a-child","Required dependent source","test:child",null,profile)],
            Relations=[new("first-pin","z-first","first","First source intent","test:first",0,backtrack?2:0,0,1),
                new("second-pin","y-second","second","Second source intent","test:second",0,0,0,1),
                new("child-first",reverse?"z-first":"a-child",reverse?"a-child":"z-first","Dependent distance to first source","test:child",5,5,5,1),
                new("child-second",reverse?"y-second":"a-child",reverse?"a-child":"y-second","Dependent distance to second source","test:child",5,5,5,1)]};
        var exclusions=new List<WorldSurfaceExclusion>{new("lower","Protect the lower dependent site","test:lower",Box(2,-5,3,3))};
        if(backtrack)exclusions.Add(new("upper","Preferred source arrangement cannot support content","test:upper",Box(2.75,3.75,.5,.5)));
        return(e.Recipe with{Surface=e.Recipe.Surface! with{Exclusions=exclusions},Placement=new(request,
            [new("z-first",id+"/sites-0",true),new("y-second",id+"/sites-1",true),new("a-child",id+"/sites-2",true)])},
            e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="root"?a with{Parts=[a.Parts[0] with{Count=3}]}:a).ToArray()});
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) NestedExample()
    {
        var e=Example();string id=e.Recipe.Id;var boundary=Box(-2,-6,36,14);var properties=new Dictionary<string,JsonElement>();
        var container=new WorldMetadataArchetype("container","compound","Reusable complete parent",properties,
            [new("first","site",1,"First required source"),new("second","site",1,"Second required source"),new("child","site",1,"Required dependent source")],[],[],
            PhysicalFootprint:new("meters","Complete parent envelope","test:parent",Box(-2,-6,16,14)),Access:new(MetadataOccupancyKind.Reservation,[]));
        var profile=e.Recipe.Placement!.Request.Demands[0].Profile;
        var childProfile=profile with{Preferences=[new("upper","y","Prefer the declared upper activity side","test:upper",4,10,1)]};
        var rootRequest=new WorldPlacementRequest("parents","Place complete declared parent frames","test:parents",boundary,
            [new("parents","Both complete parents","test:parents",null,profile)],[],[],AllowBoundaryContact:true,Execution:WorldPlacementExecution.Prescribed);
        var program=new MetadataNestedProgram("contents","Reuse one relational layout in each parent","test:contents",id,"container",2,2,
            e.Recipe.Placement.Request with{Boundary=null,Demands=e.Recipe.Placement.Request.Demands.Select(d=>d.Id=="a-child"?d with{Profile=childProfile}:d).ToArray()},
            [new("z-first","first"),new("y-second","second"),new("a-child","child")]);
        return(e.Recipe with{Surface=e.Recipe.Surface! with{Boundary=boundary,Patches=e.Recipe.Surface.Patches.Select(p=>p with{Boundary=boundary}).ToArray(),
            Exclusions=[]},Environment=e.Recipe.Environment! with{Fields=[..e.Recipe.Environment.Fields,new("y","Authored activity side","test:y","meters",0,[new(WorldFieldInput.Y,1)])]},
            Placement=new(rootRequest,[new("parents",id,true,PartId:"containers",Population:new("Declared parent frames","test:frames",Sockets:
                [new(id+"/containers-0",new(0,0)),new(id+"/containers-1",new(20,0))]))],Programs:[program])},
            e.Catalog with{Archetypes=[e.Catalog.Archetypes.Single(a=>a.Id=="root") with{Parts=[new("containers","container",2,"Both complete parents")]},container,e.Catalog.Archetypes.Single(a=>a.Id=="site")]});
    }
    public static async Task RunAsync(string folder)
    {
        if(Directory.Exists(folder)||File.Exists(folder))throw new IOException("Output exists");Directory.CreateDirectory(folder);
        var generator=new WorldMetadataGenerator();var e=Example();var world=generator.Generate(e.Recipe,e.Catalog);string id=e.Recipe.Id;
        Check(world.Nodes.Count==4&&world.AccessPlan!.Sites.Count==3,"every declared source retained");
        Check(world.Nodes.Single(n=>n.Id==id+"/sites-0").Position!.SequenceEqual(new[]{0d,0d})&&world.Nodes.Single(n=>n.Id==id+"/sites-1").Position!.SequenceEqual(new[]{6d,0d}),"both provisional source poses retained");
        Check(world.Nodes.Single(n=>n.Id==id+"/sites-2").Position!.SequenceEqual(new[]{3d,4d}),"dependent position derives from both provisional structures");
        var plan=world.PlacementPlan!.Placement;
        Check(plan.Sites.Single(s=>s.DemandId=="a-child").CandidateSource.Contains(":provisional",StringComparison.Ordinal)&&plan.Diagnostics.Count==3,"branch construction and aggregated diagnostics retained");
        Check(Json(world)==Json(generator.Generate(e.Recipe,e.Catalog)),"deterministic complete metadata replay");
        var reverse=Example(reverse:true);Check(Json(generator.Generate(reverse.Recipe,reverse.Catalog).Nodes)==Json(world.Nodes),"distance links work in both declared directions");
        var focusRequest=e.Recipe.Placement!.Request with{
            Relations=e.Recipe.Placement.Request.Relations.Select(r=>r.Id=="first-pin"?r with{Maximum=2}:r).Append(
                new("child-fixed","a-child","second","Dependent band around an authored landmark","test:focus",5,5,5,1)).ToArray(),
            Demands=e.Recipe.Placement.Request.Demands.Select(d=>d.Id=="a-child"?d with{Profile=d.Profile with{Requirements=[
                new("focus-x","x","Required activity window","test:focus",2.7,3.3),new("focus-y","y","Required activity window","test:focus",3.7,4.3)]}}:d).ToArray()};
        var focus=e.Recipe with{Environment=e.Recipe.Environment! with{Fields=[..e.Recipe.Environment.Fields,
            new("x","Activity x coordinate","test:x","meters",0,[new(WorldFieldInput.X,1)]),new("y","Activity y coordinate","test:y","meters",0,[new(WorldFieldInput.Y,1)])]},
            Placement=e.Recipe.Placement with{Request=focusRequest}};
        var focused=generator.Generate(focus,e.Catalog);var focusedPlan=focused.PlacementPlan!.Placement;
        var focusedQuery=new WorldEnvironmentQuery(new(focused.Surface!),focused.EnvironmentPlan!.Definition);
        var initialOnly=new WorldSitePlacementPlanner().Plan(focusedQuery,focusedPlan.Request with{Relations=focusedPlan.Request.Relations.Where(r=>r.Id is not "child-first" and not "child-second").ToArray()});
        Check(initialOnly.Status==WorldPlacementStatus.NoFeasibleAssignment&&initialOnly.Diagnostics.Single(d=>d.DemandId=="a-child").Eligible==0,
            "dependent starts with a genuinely empty fixed-anchor domain");
        Check(focused.Nodes.Single(n=>n.Id==id+"/sites-2").Position!.SequenceEqual(new[]{3d,4d}),"a currently empty domain waits for its unpinned source and gains the required intersection");
        WorldMetadataGenerator.ValidateDocument(focused);
        var reorder=e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Reverse().ToArray(),Request=e.Recipe.Placement.Request with{
            Demands=e.Recipe.Placement.Request.Demands.Reverse().ToArray(),Relations=e.Recipe.Placement.Request.Relations.Reverse().ToArray(),Anchors=e.Recipe.Placement.Request.Anchors.Reverse().ToArray()}}};
        Check(Json(generator.Generate(reorder,e.Catalog).Nodes)==Json(world.Nodes),"declaration order preserves output");
        var query=new WorldEnvironmentQuery(new(world.Surface!),world.EnvironmentPlan!.Definition);var planner=new WorldSitePlacementPlanner();var direct=planner.Plan(query,plan.Request);
        Reject(()=>planner.Plan(query,plan.Request with{MaximumWork=direct.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Check(planner.Plan(query,plan.Request with{MaximumWork=direct.Work}).Work==direct.Work,"existing exact work ceiling");
        int initial=plan.Diagnostics.Single(d=>d.DemandId=="z-first").Candidates;
        Reject(()=>planner.Plan(query,plan.Request with{MaximumCandidates=initial}),"WORLD_PLACEMENT_CANDIDATE_BUDGET");
        Reject(()=>planner.Plan(query,plan.Request with{MaximumSearchStates=1}),"WORLD_PLACEMENT_SEARCH_BUDGET");
        var impossibleSpacing=planner.Plan(query,plan.Request with{MinimumSeparation=500});
        Check(impossibleSpacing.Status==WorldPlacementStatus.NoFeasibleAssignment&&impossibleSpacing.SearchStates==0&&impossibleSpacing.Conflicts["footprint-separation"]>0,
            "domain geometry proves impossible separation before provisional expansion");
        var changing=Example(backtrack:true);var moved=generator.Generate(changing.Recipe,changing.Catalog);
        var first=moved.Nodes.Single(n=>n.Id==id+"/sites-0").Position!;var second=moved.Nodes.Single(n=>n.Id==id+"/sites-1").Position!;var child=moved.Nodes.Single(n=>n.Id==id+"/sites-2").Position!;
        Check(!first.SequenceEqual(new[]{0d,0d})&&second.SequenceEqual(new[]{6d,0d}),"source moves when its preferred arrangement cannot fit required content");
        Check(Math.Abs(new WorldPoint(child[0],child[1]).DistanceTo(new(first[0],first[1]))-5)<1e-12&&Math.Abs(new WorldPoint(child[0],child[1]).DistanceTo(new(second[0],second[1]))-5)<1e-12,"dependent geometry belongs to the accepted branch");
        WorldMetadataGenerator.ValidateDocument(moved);
        var nested=NestedExample();var compound=generator.Generate(nested.Recipe,nested.Catalog);
        Check(compound.Nodes.Count==9&&compound.AccessPlan!.Sites.Count==8&&compound.PlacementPlan!.ProgramInstances!.Count==2,"one program reused in both complete parent frames");
        for(int i=0;i<2;i++)
        {
            string parent=id+"/containers-"+i;
            foreach(var pair in new[]{("first",new[]{20d*i,0d}),("second",new[]{20d*i+6,0d}),("child",new[]{20d*i+3,4d})})
                Check(compound.Nodes.Single(n=>n.Id==parent+"/"+pair.Item1+"-0").Position!.SequenceEqual(pair.Item2),"exact local-to-world frame for "+parent+"/"+pair.Item1);
        }
        WorldMetadataGenerator.ValidateDocument(compound);
        Reject(()=>generator.Generate(nested.Recipe with{Surface=nested.Recipe.Surface! with{Exclusions=[new("parent-hole","Forbidden terrain cannot be covered by the parent","test:parent-hole",Box(2,-5,3,3))]}},nested.Catalog),"METADATA_PLACEMENT_UNRESOLVED");
        var session=new MetadataGenerationSession(generator,e.Recipe,e.Catalog);var before=session.Snapshot;
        var changed=e.Recipe with{Placement=e.Recipe.Placement! with{Request=e.Recipe.Placement.Request with{Anchors=e.Recipe.Placement.Request.Anchors.Select(a=>a.Id=="second"?a with{Position=new(8,0)}:a).ToArray()}}};
        var edited=session.Apply(new("source","Move authored source intent","test:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed,e.Catalog));
        Check(Json(edited.World)==Json(generator.Generate(changed,e.Catalog))&&edited.World.Nodes.Single(n=>n.Id==id+"/sites-2").Position!.SequenceEqual(new[]{4d,3d}),"source edit regenerates dependent content exactly");
        var invalid=changed with{Placement=changed.Placement! with{Request=changed.Placement.Request with{Relations=changed.Placement.Request.Relations.Select(r=>r.Id.StartsWith("child-",StringComparison.Ordinal)?r with{Minimum=1,Maximum=1,Ideal=1}:r).ToArray()}}};
        var accepted=session.Snapshot;try{session.Apply(new("refuse","Keep the accepted world","test:refuse",accepted.World.RecipeSha256,accepted.World.CatalogSha256,invalid,e.Catalog));throw new Exception("Infeasible edit accepted");}catch(MetadataRegenerationException){}
        Check(Json(accepted)==Json(session.Snapshot),"failed dependency edit preserves accepted world");
        foreach(var pair in new[]{("single",e),("focused",(Recipe:focus,Catalog:e.Catalog)),("backtrack",changing),("nested",nested)})
        {
            string directory=Path.Combine(folder,pair.Item1);Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory,"recipe.json"),Json(pair.Item2.Recipe));await File.WriteAllTextAsync(Path.Combine(directory,"catalog.json"),Json(pair.Item2.Catalog));
            await generator.GenerateFilesAsync(Path.Combine(directory,"recipe.json"),Path.Combine(directory,"catalog.json"),Path.Combine(directory,"packet"));
        }
        Console.WriteLine("PASS: provisional two-source relations, reverse direction, pinned-source ordering, source backtracking, branch-local content, repeated nested programs, exact budgets, replay, edits and atomic rollback.");
    }
}
