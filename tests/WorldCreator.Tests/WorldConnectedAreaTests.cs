using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;
internal static class WorldConnectedAreaTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Connected area: "+message);}
    private static void Near(double a,double b,string message)=>Check(Math.Abs(a-b)<1e-6*Math.Max(1,Math.Abs(b)),message+": "+a+" / "+b);
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Missing "+code);}
    private static (WorldEnvironmentQuery Query,WorldRegionPlan Regions,WorldSuitabilityProfile Profile) Region(double degrees=0,double x=0,double y=0)
    {
        double a=degrees*Math.PI/180;WorldPoint P(double px,double py)=>new(x+px*Math.Cos(a)-py*Math.Sin(a),y+px*Math.Sin(a)+py*Math.Cos(a));
        WorldPolygon Box(double bx,double by,double w,double h)=>new([P(bx,by),P(bx+w,by),P(bx+w,by+h),P(bx,by+h)]);
        var boundary=Box(0,0,10,8);var surface=new WorldSurfaceQuery(new(1,"land","meters",boundary,[new("cutout","Protected C-shaped cutout","connected:cutout",Box(2,2,8,4))],
            [new("ground","ground","Purposeful C-shaped support","connected:support",boundary,0,new(0,0),0,0,0)]));
        var query=new WorldEnvironmentQuery(surface,new(1,"fields",[new("density","Uniform declared density","connected:density","relative",1,[])]));
        var profile=new WorldSuitabilityProfile("usable","Required C-shaped support","connected:support",["ground"],[],[]);
        var regions=new WorldRegionPlanner().Plan(query,new("region","Required support","connected:region",boundary,profile,[new("region","Required support","connected:region",P(.5,4),1)]));
        return(query,regions,profile);
    }
    internal static WorldAreaPopulationPattern Pattern()=>new(Interior:new(.02,MinimumClearance:.25),PartitionSearch:new([0,90,30,-30,60,-60],128));
    private static void Proof(WorldAreaPopulationPlan plan,int count)
    {
        var attempts=plan.PartitionAttempts!;Check(attempts.Count<=plan.Pattern.PartitionSearch!.MaximumAttempts,"finite attempt count");
        Check(attempts.Select(a=>a.Ordinal).SequenceEqual(Enumerable.Range(1,attempts.Count)),"complete attempt sequence");
        var selected=attempts.Where(a=>a.Status==WorldAreaPartitionStatus.Selected).ToArray();Check(selected.Length==count-1,"exact selected binary tree");
        void Tree(int first,int members,int depth)
        {
            if(members==1)return;var cut=selected.Single(a=>a.FirstMember==first&&a.Members==members&&a.Depth==depth);
            Near(cut.LeftMass,cut.TargetMass,"solved weighted target");Near(cut.LeftMass+cut.RightMass,plan.Strata.Skip(first).Take(members).Sum(s=>s.Mass),"all descendant mass retained");
            int left=members/2;Near(cut.LeftMass,plan.Strata.Skip(first).Take(left).Sum(s=>s.Mass),"left leaf accounting");
            double angle=cut.AngleDegrees*Math.PI/180,dx=Math.Cos(angle),dy=Math.Sin(angle);
            foreach(var s in plan.Strata.Skip(first).Take(left))foreach(var p in s.Cells.SelectMany(c=>c.Vertices))Check((p.X-cut.Origin.X)*dx+(p.Y-cut.Origin.Y)*dy<=cut.Cut+1e-7,"left geometry obeys selected cut");
            foreach(var s in plan.Strata.Skip(first+left).Take(members-left))foreach(var p in s.Cells.SelectMany(c=>c.Vertices))Check((p.X-cut.Origin.X)*dx+(p.Y-cut.Origin.Y)*dy>=cut.Cut-1e-7,"right geometry obeys selected cut");
            Tree(first,left,depth+1);Tree(first+left,members-left,depth+1);
        }
        Tree(0,count,0);Check(plan.Strata.Count==count,"every declared member exists exactly once");Near(plan.Strata.Sum(s=>s.Area),48,"whole support area");Near(plan.Strata.Sum(s=>s.Mass),48,"whole support mass");
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) MetadataExample()
    {
        var e=WorldTerrainPartitionTests.Example(true);
        return(e.Recipe with{LocalLayouts=e.Recipe.LocalLayouts! with{Layouts=e.Recipe.LocalLayouts!.Layouts.Select(l=>l with{
            AreaPattern=l.AreaPattern! with{PartitionSearch=new([0,90,30,-30,60,-60],256)}}).ToArray()}},e.Catalog);
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) FullExample(bool richer=false)
    {
        var e=WorldLayeredLandformTests.Example("ridge");var source=WorldTerrainPartitionTests.SteepSource();
        WorldEnvironmentDefinition Fields(WorldEnvironmentDefinition fields)=>fields with{Fields=fields.Fields.Select(f=>f.Id=="landscape-density"?f with{
            Terms=f.Terms.Select(t=>t with{Coefficient=richer?.003:.002}).ToArray()}:f).ToArray()};
        var sourceRecipe=source.Recipe with{Environment=Fields(source.Recipe.Environment!)};
        return(e.Recipe with{Surface=sourceRecipe.Surface,Environment=sourceRecipe.Environment,LocalLayouts=sourceRecipe.LocalLayouts,LayoutAdaptation=sourceRecipe.LayoutAdaptation,
            PopulationSource=new(sourceRecipe,source.Catalog),Placement=e.Recipe.Placement! with{AllocationAnchors=sourceRecipe.Placement!.AllocationAnchors,
                Bindings=e.Recipe.Placement!.Bindings.Select(b=>b.Population?.Region?.Area is {} area?b with{Population=b.Population with{Region=b.Population.Region with{
                    Area=area with{AxisRotationDegrees=30,Interior=new(.1,MinimumClearance:.75),PartitionSearch=new([0,90,30,-30,60,-60],256)}}}}:b).ToArray()},
            RemainingScopes=["Complete declared seven-compound and density-funded landscape reference on protected terrain; broader world variety, scale, design acceptance and Godot remain open."]},e.Catalog);
    }
    public static async Task WriteExamplesAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("Example exists");
        foreach(string name in new[]{"metadata","steep-world","richer-world"})
        {
            var e=name=="metadata"?MetadataExample():FullExample(name=="richer-world");string folder=Path.Combine(directory,name);Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(e.Recipe));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
        }
        var geometry=Region();var pattern=Pattern();
        await File.WriteAllTextAsync(Path.Combine(directory,"geometry-input.json"),Json(new{Surface=geometry.Query.Surface.Definition,Environment=geometry.Query.Definition,Regions=geometry.Regions.Request,Pattern=pattern,Count=2}));
        await File.WriteAllTextAsync(Path.Combine(directory,"geometry-plan.json"),Json(WorldPopulationGeometry.Area(new(geometry.Regions,"region"),geometry.Query,pattern,2)));
    }
    public static async Task FullAsync()
    {
        var input=FullExample();var generator=new WorldMetadataGenerator();var session=new MetadataGenerationSession(generator,input.Recipe,input.Catalog);
        var baseline=session.Snapshot;WorldLayeredIntegrationTests.Qualify(baseline.World,input.Catalog);
        var area=baseline.World.PlacementPlan!.PopulationAreas!.Single().Plan;Check(area.Strata.Count==16,"all16 members from unchanged base density rule");
        Check(area.PartitionAttempts!.Count(a=>a.Status==WorldAreaPartitionStatus.Selected)==15,"full selected landscape tree");
        Check(baseline.World.LayoutAdaptationPlan!.Attempts.Any(a=>a.Rejection=="METADATA_POPULATION_LAYOUT_CANDIDATE_CHANGED"),"changed source-layout candidates refused before reanchoring fixed compounds");
        var richer=FullExample(true);var accepted=session.Apply(new("richer","Regenerate complete landscape from changed density","connected:edit",baseline.World.RecipeSha256,baseline.World.CatalogSha256,richer.Recipe,richer.Catalog));
        WorldLayeredIntegrationTests.Qualify(accepted.World,richer.Catalog);
        Check(Json(accepted.World)==Json(generator.Generate(richer.Recipe,richer.Catalog)),"full edit equals clean generation");
        var failed=richer.Recipe with{Placement=richer.Recipe.Placement! with{Bindings=richer.Recipe.Placement!.Bindings.Select(b=>b.Population?.Region?.Area is {} a?b with{
            Population=b.Population with{Region=b.Population.Region with{Area=a with{PartitionSearch=a.PartitionSearch! with{MaximumAttempts=1}}}}}:b).ToArray()}};
        try{session.Apply(new("bounded","Do not omit landscape on partition exhaustion","connected:rollback",accepted.World.RecipeSha256,accepted.World.CatalogSha256,failed,richer.Catalog));throw new Exception("Missing bounded failure");}catch(MetadataRegenerationException){}
        Check(Json(accepted)==Json(session.Snapshot),"full-world failed edit rolls back accepted state");
        string output=Path.Combine(Path.GetTempPath(),"connected-world-"+Guid.NewGuid().ToString("N"));await session.PublishAsync(output);
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"generation-session.json"));Check(Json(restored.Snapshot)==Json(accepted),"full source history checkpoint replay");
        Console.WriteLine($"Qualified full worlds: baseline={baseline.World.Nodes.Count}nodes/{area.Strata.Count}landscape, richer={accepted.World.Nodes.Count}nodes/{accepted.World.PlacementPlan!.PopulationAreas!.Single().Plan.Strata.Count}landscape; all compounds, source geometry and82paths preserved; edit/clean, refusal rollback and checkpoint pass.");
    }
    public static async Task RunAsync()
    {
        foreach(var (angle,x,y) in new[]{(0d,0d,0d),(17d,-80d,40d),(71d,120d,-90d)})
        {
            var e=Region(angle,x,y);var domain=new WorldAreaPopulationDomain(e.Regions,"region");var pattern=Pattern();
            Reject(()=>WorldPopulationGeometry.Area(domain,e.Query,pattern with{PartitionSearch=null},2),"WORLD_LAYOUT_DISCONNECTED_ALLOCATION");
            var plan=WorldPopulationGeometry.Area(domain,e.Query,pattern,2);Proof(plan,2);
            Check(plan.PartitionAttempts!.Count==2&&plan.PartitionAttempts[0].Status==WorldAreaPartitionStatus.Rejected&&plan.PartitionAttempts[1].Status==WorldAreaPartitionStatus.Selected,"preferred disconnected cut is retained before connected alternative");
            foreach(var s in plan.Strata){Near(s.Mass,24,"equal mass");Check(s.Interior!.Clearance>=.25,"declared representative clearance");}
            Check(Json(plan)==Json(WorldPopulationGeometry.Area(domain,e.Query,pattern,2)),"exact deterministic source replay");
            Console.WriteLine($"Qualified C-shaped rotation={angle}:48area/mass,2members,2attempts,work={plan.Work}.");
        }
        var input=Region();var support=new WorldAreaPopulationDomain(input.Regions,"region");var policy=Pattern();
        foreach(int count in new[]{3,4,5,6})
        {
            var plan=WorldPopulationGeometry.Area(support,input.Query,policy,count);Proof(plan,count);foreach(var s in plan.Strata)Near(s.Mass,48d/count,"odd/even mass");
            Console.WriteLine($"Qualified nested count={count}: attempts={plan.PartitionAttempts!.Count}, superseded={plan.PartitionAttempts.Count(a=>a.Status==WorldAreaPartitionStatus.Superseded)}.");
        }
        try{WorldPopulationGeometry.AreaVerifiedRegion(input.Regions.Regions[0],input.Query,input.Profile,
            policy with{Interior=new(.02,MinimumClearance:.9),PartitionSearch=policy.PartitionSearch! with{MaximumAttempts=512}},4,new(5000000,CancellationToken.None),CancellationToken.None,relativeWeights:[4,4,1,3]);throw new Exception("Missing infeasible clearance refusal");}
        catch(ArgumentException error)when(error.Message.StartsWith("WORLD_AREA_PARTITION_UNRESOLVED",StringComparison.Ordinal))
        {
            var history=(IReadOnlyList<WorldAreaPartitionAttempt>)error.Data["AreaPartitionAttempts"]!;
            Console.WriteLine($"Rollback probe: attempts={history.Count}, superseded={history.Count(a=>a.Status==WorldAreaPartitionStatus.Superseded)}, retained={error.Data["AreaRetainedStrata"]}.");
            Check(history.Any(a=>a.Status==WorldAreaPartitionStatus.Superseded)&&history.All(a=>a.Status!=WorldAreaPartitionStatus.Selected),"previous complete subtrees superseded on parent reconsideration");
            Check((int)error.Data["AreaRetainedStrata"]! ==0&&(int)error.Data["AreaRetainedCells"]! ==0,"all provisional leaf and cell state restored on full refusal");
        }
        var weighted=WorldPopulationGeometry.AreaVerifiedRegion(input.Regions.Regions[0],input.Query,input.Profile,policy,3,new(5000000,CancellationToken.None),CancellationToken.None,relativeWeights:[1,2,1]);
        Proof(weighted,3);Near(weighted.Strata[0].Mass,12,"first unequal weight");Near(weighted.Strata[1].Mass,24,"middle unequal weight");Near(weighted.Strata[2].Mass,12,"last unequal weight");
        Reject(()=>WorldPopulationGeometry.Area(support,input.Query,policy with{PartitionSearch=new([0],128)},2),"WORLD_AREA_PARTITION_UNRESOLVED:");
        Reject(()=>WorldPopulationGeometry.Area(support,input.Query,policy with{PartitionSearch=new([0,90],1)},2),"WORLD_AREA_PARTITION_ATTEMPT_BUDGET");
        foreach(var bad in new[]{new WorldAreaPartitionSearch([],128),new([0,180],128),new([0,double.NaN],128),new([0,90],0)})
            Reject(()=>WorldPopulationGeometry.Area(support,input.Query,policy with{PartitionSearch=bad},2),"WORLD_AREA_PARTITION_SEARCH_INVALID");
        var exact=WorldPopulationGeometry.Area(support,input.Query,policy,2);Check(WorldPopulationGeometry.Area(support,input.Query,policy,2,exact.Work).Work==exact.Work,"exact aggregate work accepted");
        Reject(()=>WorldPopulationGeometry.Area(support,input.Query,policy,2,exact.Work-1),"WORLD_ROUTE_WORK_BUDGET");
        using(var c=new CancellationTokenSource()){c.Cancel();try{WorldPopulationGeometry.Area(support,input.Query,policy,2,token:c.Token);throw new Exception("Ignored cancellation");}catch(OperationCanceledException){}}
        var metadata=MetadataExample();var generator=new WorldMetadataGenerator();var document=generator.Generate(metadata.Recipe,metadata.Catalog);
        var stored=document.LocalLayoutPlan!;var generated=stored.GeneratedAreas!.Single();
        var tampered=document with{LocalLayoutPlan=stored with{GeneratedAreas=[generated with{AreaPlan=generated.AreaPlan with{
            PartitionAttempts=generated.AreaPlan.PartitionAttempts!.Select(a=>a with{Cut=a.Cut+1}).ToArray()}}]}};
        Reject(()=>new MetadataLocalLayoutPlanner().ValidateDocument(tampered),"METADATA_LOCAL_LAYOUT_PLAN_MISMATCH");
        WorldMetadataGenerator.ValidateDocument(document);
        await Task.CompletedTask;
        Console.WriteLine("PASS: connected mass partitions preserve full support, exact odd/even/weighted counts, preferred-axis rejection and selected geometry/tree, deterministic replay and finite input/attempt/work/cancel refusals.");
    }
}
