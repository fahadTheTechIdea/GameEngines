using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldLocalConstraintSearchTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool value,string reason){if(!value)throw new Exception("Local constraints: "+reason);}
    private static void Near(double a,double b,string reason)=>Check(Math.Abs(a-b)<1e-6,reason);
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Missing "+prefix);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static (WorldEnvironmentQuery Query,WorldRegionPlan Regions) Region(WorldPolygon boundary,bool two=false)
    {
        var surface=new WorldSurfaceDefinition(1,"local-source","meters",boundary,[],[new("ground","ground","Actual program domain","local:source",boundary,0,new(0,0),0,0,0)]);
        var environment=new WorldEnvironmentDefinition(1,"local-fields",[new("density","Declared program mass","local:density","relative",1,[new(WorldFieldInput.X,1)])]);
        var query=new WorldEnvironmentQuery(new WorldSurfaceQuery(surface),environment);var profile=new WorldSuitabilityProfile("ground","Required land","local:ground",["ground"],[],[]);
        var request=two?new WorldRegionRequest("regions","Two adjoining semantic regions","local:regions",boundary,profile,[],Intents:[new("left","First region","local:left",1),new("right","Second region","local:right",1)],IntentPartition:new(new())):
            new WorldRegionRequest("regions","Actual connected program region","local:region",boundary,profile,[new("region","Program region","local:region",boundary.Centroid(),1)]);
        return(query,new WorldRegionPlanner().Plan(query,request));
    }
    private static WorldLocalLayout Area(string id,string region,string a,string b)=>new(id,region,"Required connected program allocation","local:area",WorldLocalLayoutStrategy.AreaBalanced,[],
        ProgramIntents:[new(a,"First complete program","local:"+a,10),new(b,"Second complete program","local:"+b,10)],AreaPattern:new(PartitionSearch:new([0,90,45])));
    internal static async Task RunAsync(string output,bool control)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Output exists");Directory.CreateDirectory(output);
        var planner=new WorldLocalLayoutPlanner();var source=Region(Box(0,0,24,12));
        var definition=new WorldLocalLayoutDefinition([Area("first","region","a","b")],[new("wide","a","b","Required wide connection between programs","local:wide",18)]);
        if(control)
        {
            var forced=definition with{Layouts=[definition.Layouts[0] with{AreaPattern=definition.Layouts[0].AreaPattern! with{AxisRotationDegrees=90}}]};
            var result=planner.Plan(source.Query,source.Regions,forced);Near(Span(result),24,"forced axis gives a feasible24m interface");await Save("control.json",result);Console.WriteLine("PASS: prior-Core forced-axis control retains both complete areas and24m shared boundary.");return;
        }
        var plan=planner.Plan(source.Query,source.Regions,definition);Near(Span(plan),24,"required interface drives an alternative cut");
        Check(plan.Allocations.Count==2&&plan.GeneratedAreas!.Single().AreaPlan.PartitionAttempts!.Any(a=>a.Status==WorldAreaPartitionStatus.Rejected&&a.Rejection!.Contains("local-interface:wide")),"initial12m interface rejection retained");
        Near(plan.Allocations.Sum(a=>a.Shape.Area),288,"complete source area retained");
        Check(Json(plan)==Json(planner.Plan(source.Query,source.Regions,definition with{Layouts=[definition.Layouts[0] with{ProgramIntents=definition.Layouts[0].ProgramIntents!.Reverse().ToArray()}]})),"canonical program order");
        Check(planner.Plan(source.Query,source.Regions,definition with{MaximumWork=plan.Work}).Work==plan.Work,"exact aggregate work boundary");
        Reject(()=>planner.Plan(source.Query,source.Regions,definition with{MaximumWork=plan.Work-1}),"WORLD_ROUTE_WORK_BUDGET");
        Reject(()=>planner.Plan(source.Query,source.Regions,definition with{Layouts=[definition.Layouts[0] with{AreaPattern=new(PartitionSearch:new([0,90],1))}]}),"WORLD_AREA_PARTITION_ATTEMPT_BUDGET");
        Reject(()=>planner.Plan(source.Query,source.Regions,definition with{Interfaces=[definition.Interfaces[0] with{MinimumSpan=100}]}),"WORLD_AREA_PARTITION_UNRESOLVED");
        Reject(()=>planner.Plan(source.Query,source.Regions,definition with{Interfaces=[definition.Interfaces[0] with{ToId="missing"}]}),"WORLD_LAYOUT_INTERFACE_INVALID");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.Plan(source.Query,source.Regions,definition,cancel.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var weightedDefinition=definition with{Layouts=[definition.Layouts[0] with{ProgramIntents=definition.Layouts[0].ProgramIntents!.Select(i=>i with{MinimumArea=100}).ToArray(),AreaPattern=new("density",PartitionSearch:new([0,90]))}],Interfaces=[]};
        var weighted=planner.Plan(source.Query,source.Regions,weightedDefinition);Check(weighted.Allocations.All(a=>a.Shape.Area>=100),"actual minimum area participates in weighted cut search");
        foreach(var a in weighted.Allocations)Near(a.Shape.Area,144,"perpendicular weighted cut preserves physical area");
        var jointSource=Region(Box(0,0,48,12),true);var jointDefinition=new WorldLocalLayoutDefinition([Area("first","left","a","b"),Area("second","right","c","d")],
            [new("across","a","c","Required shared interface across regions","local:across",5)]);
        var joint=planner.Plan(jointSource.Query,jointSource.Regions,jointDefinition);Check(joint.Allocations.Count==4&&Span(joint)>=5,"later layout can reconsider earlier area choice");
        Near(joint.Allocations.Sum(a=>a.Shape.Area),576,"both complete semantic regions retained");
        Check(joint.GeneratedAreas!.First().AreaPlan.PartitionAttempts!.Any(a=>a.Status==WorldAreaPartitionStatus.Rejected),"earlier layout retains downstream failure");
        var nestedDefinition=definition with{Layouts=[definition.Layouts[0],new("nested","region","Fit declared nested program","local:nested",WorldLocalLayoutStrategy.PowerDiagram,
            [new("inside","Required nested content","local:inside",new(20,3),1)],ParentAllocationId:"a")],Interfaces=[]};
        var nested=planner.Plan(source.Query,source.Regions,nestedDefinition);Check(nested.Allocations.Single(a=>a.Id=="inside").Shape.Cells.Any(c=>c.ContainsPoint(new(20,3))),"nested fixed anchor drives parent area search");
        var chain=Enumerable.Range(0,32).Select(i=>new WorldLocalLayout("chain-"+i.ToString("D2"),"region","Retain current composition bound","local:chain",WorldLocalLayoutStrategy.AreaBalanced,[],
            ParentAllocationId:i==0?null:"host-"+(i-1),ProgramIntents:[new("host-"+i,"Required complete nested domain","local:host",1)],AreaPattern:new())).ToArray();
        var deep=planner.Plan(source.Query,source.Regions,new(chain.Reverse().ToArray(),[]));Check(deep.Areas.Count==32&&deep.Allocations.Count==32,"existing32-layout bound retained");
        foreach(var a in deep.Areas)Near(a.AllocatedArea,288,"nested accounting retains source area without claiming extra ground");
        await Save("analytic.json",plan);await Save("weighted.json",weighted);await Save("joint.json",joint);await Save("joint-regions.json",jointSource.Regions);await Save("nested.json",nested);await Save("chain.json",deep);

        var input=WorldProgramSpaceTests.Example();var larger=Box(0,0,40,30);var original=input.Recipe;
        var recipe=original with{Surface=original.Surface! with{Boundary=larger,Patches=original.Surface!.Patches.Select(p=>p with{Boundary=larger}).ToArray()},
            Placement=original.Placement! with{Request=original.Placement!.Request with{Boundary=larger}},
            Regions=original.Regions! with{Boundary=larger},LocalLayouts=original.LocalLayouts! with{Layouts=original.LocalLayouts!.Layouts.Select(l=>l with{AreaPattern=new(PartitionSearch:new([0,90,45]))}).ToArray(),
                Interfaces=original.LocalLayouts.Interfaces.Select(i=>i with{MinimumSpan=35}).ToArray()}};
        var generator=new WorldMetadataGenerator();var world=generator.Generate(recipe,input.Catalog);Complete(world);Near(Span(world.LocalLayoutPlan!),40,"physical world uses an actual40m interface");
        await generator.GenerateFilesAsync(await Save("recipe.json",recipe),await Save("catalog.json",input.Catalog),Path.Combine(output,"world"));
        var changed=recipe with{LocalLayouts=recipe.LocalLayouts! with{Interfaces=recipe.LocalLayouts!.Interfaces.Select(i=>i with{MinimumSpan=20}).ToArray()}};
        var session=new MetadataGenerationSession(generator,recipe,input.Catalog);var before=session.Snapshot;
        var accepted=await session.ApplyAndPublishAsync(new("interface-edit","Allow a narrower shared gameplay boundary","local:edit",before.World.RecipeSha256,before.World.CatalogSha256,changed,input.Catalog),Path.Combine(output,"edited"));
        Complete(accepted.World);Near(Span(accepted.World.LocalLayoutPlan!),30,"interface source edit changes the selected layout");
        Check(Json(accepted.World)==Json(generator.Generate(changed,input.Catalog)),"edited complete world equals clean generation");
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"edited/generation-session.json"));Check(Json(restored.Snapshot)==Json(accepted),"complete source history replays local constraint search");
        var blocked=changed with{LocalLayouts=changed.LocalLayouts! with{Interfaces=changed.LocalLayouts!.Interfaces.Select(i=>i with{MinimumSpan=100}).ToArray()}};
        try{await session.ApplyAndPublishAsync(new("blocked","Retain complete required program","local:blocked",accepted.World.RecipeSha256,accepted.World.CatalogSha256,blocked,input.Catalog),Path.Combine(output,"refused"));throw new Exception("Missing impossible interface refusal");}catch(MetadataRegenerationException){}
        Check(Json(session.Snapshot)==Json(accepted)&&!Directory.Exists(Path.Combine(output,"refused")),"failed edit preserves accepted state and output");
        await Save("changed-recipe.json",changed);await Save("blocked-recipe.json",blocked);
        await Save("observations.json",new{Nodes=world.Nodes.Count,PhysicalSources=world.AccessPlan!.Sites.Count,InitialSpan=Span(world.LocalLayoutPlan!),EditedSpan=Span(accepted.World.LocalLayoutPlan!),AnalyticSpan=Span(plan),JointSpan=Span(joint),LocalLayouts=deep.Areas.Count});
        Console.WriteLine("PASS: actual area/interface/nested constraints drive local cut search; complete physical sources/travel, source edit/clean/replay,32-layout bound and refusal/cancellation/work preservation.");
        async Task<string> Save(string name,object value){string p=Path.Combine(output,name);await File.WriteAllTextAsync(p,Json(value));return p;}
        double Span(WorldLocalLayoutPlan value)=>value.Interfaces.Single().SelectedSpan.From.DistanceTo(value.Interfaces.Single().SelectedSpan.To);
        void Complete(WorldMetadataDocument value)=>Check(value.Nodes.Count==7&&value.Nodes.Count(n=>n.Kind=="station")==4&&value.AccessPlan!.Sites.Count==2&&value.PlacementPlan!.Placement.Sites.Count==2&&value.RoutePlan!.Routes.Count==1,"all original structures, four declared station metadata children, exact physical sources and travel retained");
    }
}
