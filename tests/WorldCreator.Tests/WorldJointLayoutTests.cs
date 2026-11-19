using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldJointLayoutTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Joint layouts: "+message);}
    private static void Reject(Action action,string prefix){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(prefix,StringComparison.Ordinal)){return;}throw new Exception("Expected "+prefix);}
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool mixed=false)
    {
        var e=WorldLayoutAdaptationTests.Example();var first=e.Recipe.LocalLayouts!.Layouts[0];
        var second=first with{Id="east-layout",RegionId="east",Programs=first.Programs.Select(p=>p with{Id=p.Id.Replace("west","east"),Anchor=p.Anchor.Offset(50,0)}).ToArray(),Grid=first.Grid! with{Origin=new(50,0)}};
        var domain=Box(0,0,90,20);var surface=e.Recipe.Surface!;var placement=e.Recipe.Placement!;var request=placement.Request;
        var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId);
        var catalog=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id==root.Id?a with{Parts=[root.Parts[0] with{Count=4}],ChildRequirements=[new("site",4,4)]}:a).ToArray()};
        var east=second with{Strategy=WorldLocalLayoutStrategy.PowerDiagram,Grid=null,Programs=second.Programs.Select((p,i)=>p with{Influence=i==0?96:0,Column=null,Row=null}).ToArray()};
        var additional=new MetadataLayoutAdaptationScope(second.Id,"Fit all required eastern contents","joint:east",
            mixed?[]:[new("expand",MetadataLayoutDialKind.GridWidth,2,0,1)],mixed?[new("weighted-east","Compatible eastern grammar","joint:weighted",east)]:null);
        return(e.Recipe with{Surface=surface with{Boundary=domain,Exclusions=[new("gap","Separate content domains","joint:gap",Box(40,0,10,20))],Patches=[surface.Patches[0] with{Boundary=domain}]},
            Regions=e.Recipe.Regions! with{Boundary=domain,Seeds=[..e.Recipe.Regions.Seeds,new("east","Second required content domain","joint:east",new(58,10),700)]},
            LocalLayouts=e.Recipe.LocalLayouts with{Layouts=[first,second],Interfaces=[..e.Recipe.LocalLayouts.Interfaces,new("east-link","east-a","east-b","Required eastern passage","joint:east-link",10)]},
            Placement=placement with{Request=request with{Boundary=domain,
                Demands=[..request.Demands,..request.Demands.Select(d=>d with{Id=d.Id.Replace("west","east"),RegionId="east",AllocationId=d.AllocationId!.Replace("west","east")})],
                Anchors=[..request.Anchors,new("c",new(63,10)),new("d",new(81,10))],
                Relations=[..request.Relations,new("east-a","east-a","c","Keep required first eastern position","joint:c",0,0,0,1),new("east-b","east-b","d","Keep required second eastern position","joint:d",0,0,0,1)]},
                Bindings=[..placement.Bindings,..placement.Bindings.Select((b,i)=>b with{DemandId=b.DemandId.Replace("west","east"),NodeId="local-layouts/sites-"+(i+2)})]},
            AdditionalConnections=[..e.Recipe.AdditionalConnections!,new("east","west-travel","local-layouts/sites-2","local-layouts/sites-3",true)],
            RouteRules=[e.Recipe.RouteRules![0] with{Interfaces=[..e.Recipe.RouteRules[0].Interfaces!,new("local-layouts/explicit/east","east-link")]}],
            LayoutAdaptation=e.Recipe.LayoutAdaptation! with{Dials=[new("expand",MetadataLayoutDialKind.GridWidth,2,0,1)],AdditionalScopes=[additional]}},catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(bool mixed in new[]{false,true}){var e=Example(mixed);string folder=Path.Combine(directory,mixed?"mixed-modes":"two-dials");Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));}
    }
    public static async Task RunAsync()
    {
        var generator=new WorldMetadataGenerator();
        foreach(bool mixed in new[]{false,true})
        {
            var e=Example(mixed);var policy=e.Recipe.LayoutAdaptation!;
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=policy with{AdditionalScopes=null}},e.Catalog),"METADATA_LAYOUT_ADAPTATION_UNRESOLVED");
            var other=policy.AdditionalScopes!.Single();
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=new(other.LayoutId,other.Purpose,other.SourceRuleId,other.Dials,Alternatives:other.Alternatives)},e.Catalog),"METADATA_LAYOUT_ADAPTATION_UNRESOLVED");
            var world=generator.Generate(e.Recipe,e.Catalog);var plan=world.LayoutAdaptationPlan!;
            Check(plan.Attempts.Count==4&&plan.Attempts.Take(3).All(a=>!a.Accepted)&&plan.Attempts[3].Accepted,"two independent required repairs qualified jointly");
            int[][] oracle=[[0,0],[0,1],[1,0],[1,1]];
            for(int i=0;i<4;i++){
                var setting=plan.Attempts[i].ScopeSettings!;Check(setting.Count==2&&setting[0].LayoutId==policy.LayoutId&&setting[1].LayoutId==other.LayoutId,"all scope identities retained");
                Check(setting[0].Steps["expand"]==oracle[i][0]&&(mixed?(setting[1].AlternativeId is null?0:1):setting[1].Steps["expand"])==oracle[i][1],"independent complete combination order");
            }
            Check(world.Nodes.Count==13&&world.PlacementPlan!.Placement.Sites.Count==4&&world.RoutePlan!.Routes.Count==2,"all four structures, eight constituents and both physical passages");
            foreach(var allocation in world.LocalLayoutPlan!.Allocations)Check(Math.Abs(allocation.Shape.Area-(allocation.Id.EndsWith("a",StringComparison.Ordinal)?440:360))<1e-6,"independent area oracle");
            foreach(var passage in world.RoutePlan!.Routes)Check(Math.Abs(passage.Interface!.Center.X-(passage.ConnectionId.EndsWith("east",StringComparison.Ordinal)?72:22))<1e-6,"independent recomputed physical interface");
            foreach(var site in world.PlacementPlan!.Placement.Sites){Check(Math.Abs(site.Footprint!.Footprint.SignedArea()-84)<1e-6&&Math.Abs(site.Reservation!.Footprint.SignedArea()-128)<1e-6,"source dimensions unchanged");}
            foreach(var anchor in e.Recipe.Placement!.Request.Anchors){var resolved=world.PlacementPlan.Definition.Request.Anchors.Single(a=>a.Id==anchor.Id);Check(resolved.Position==anchor.Position,"protected anchors unchanged");}
            WorldMetadataGenerator.ValidateDocument(world);
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=policy with{MaximumAttempts=3}},e.Catalog),"METADATA_LAYOUT_ADAPTATION_ATTEMPT_BUDGET");
            Reject(()=>generator.Generate(e.Recipe with{LayoutAdaptation=policy with{MaximumWork=plan.Work-1}},e.Catalog),"WORLD_ROUTE_WORK_BUDGET");
            Check(generator.Generate(e.Recipe with{LayoutAdaptation=policy with{MaximumWork=plan.Work,MaximumAttempts=4}},e.Catalog).LayoutAdaptationPlan!.Work==plan.Work,"exact shared budget");
            Reject(()=>new MetadataLocalLayoutPlanner().ValidateDocument(world with{LayoutAdaptationPlan=plan with{Attempts=plan.Attempts.Select((a,i)=>i==3?a with{ScopeSettings=null}:a).ToArray()}}),"METADATA_LAYOUT_ADAPTATION_PLAN_MISMATCH");
        }
        var input=Example();var rule=input.Recipe.LayoutAdaptation!;
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=rule with{AdditionalScopes=[rule.AdditionalScopes![0] with{LayoutId=rule.LayoutId}]}},input.Catalog),"METADATA_LAYOUT_SCOPE_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=rule with{AdditionalScopes=[]}},input.Catalog),"METADATA_LAYOUT_SCOPE_INVALID");
        Reject(()=>generator.Generate(input.Recipe with{LayoutAdaptation=rule with{Dials=[new("expand",MetadataLayoutDialKind.GridWidth,.25,0,8)],AdditionalScopes=[rule.AdditionalScopes![0] with{Dials=[new("expand",MetadataLayoutDialKind.GridWidth,.25,0,8)]}]}},input.Catalog),"METADATA_LAYOUT_VARIANT_BUDGET");
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();try{generator.Generate(input.Recipe,input.Catalog,cancelled.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);var before=session.Snapshot;var mixedInput=Example(true);
        try{session.Apply(new("invalid-scopes","Preserve accepted complete world on invalid scope edit","joint:rollback",before.World.RecipeSha256,before.World.CatalogSha256,
            input.Recipe with{LayoutAdaptation=rule with{AdditionalScopes=[rule.AdditionalScopes![0] with{LayoutId=rule.LayoutId}]}},input.Catalog));throw new Exception("Missing refusal");}
        catch(MetadataRegenerationException error)when(error.InnerException is ArgumentException cause&&cause.Message=="METADATA_LAYOUT_SCOPE_INVALID"){}
        Check(Json(before)==Json(session.Snapshot),"invalid joint edit rolls back");
        var changed=session.Apply(new("mixed-modes","Use declared grammar alternatives for one owner","joint:edit",before.World.RecipeSha256,before.World.CatalogSha256,mixedInput.Recipe,mixedInput.Catalog));
        Check(Json(changed.World)==Json(generator.Generate(mixedInput.Recipe,mixedInput.Catalog)),"joint edit equals clean generation");
        string directory=Path.Combine(Path.GetTempPath(),"joint-layout-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(directory);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(directory,"generation-session.json"));Check(Json(restored.Snapshot)==Json(changed),"joint source/receipt checkpoint replay");
        Console.WriteLine("PASS: complete 13-node/four-structure/two-route world needs two simultaneous repairs; numeric/numeric and numeric/strategy scopes, all four oracle combinations, source geometry/anchors/area/interfaces, exact shared budgets, Cartesian/scope/cancel/tamper refusal and edit/checkpoint replay.");
    }
}
