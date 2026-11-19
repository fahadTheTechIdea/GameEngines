using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldSampledRegionTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(value))));
    private static void Check(bool value,string message){if(!value)throw new Exception("Sampled region: "+message);}
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Sampled region evidence exists");Directory.CreateDirectory(output);
        int samples=PrimitiveChecks(),envelopes=EnvelopeChecks();var e=WorldNetworkPolicyTests.Example();var generator=new WorldMetadataGenerator();
        var seed=generator.Generate(e.Recipe with{NetworkRules=null},e.Catalog);var placement=e.Recipe.Placement!;
        var heights=Enumerable.Range(0,99).Select(i=>(i%11)*10*.025+(i/11)*10*.01).ToArray();
        var field=new WorldHeightField(1,"regional-height","ground","Original lattice for neighboring network programs","sampled:source","meters",new(0,0),10,10,11,9,heights);
        var west=Box(0,0,50,80);var east=new WorldPolygon([new(50,0),new(100,0),new(100,65),new(85,80),new(50,80)]);
        var recipe=e.Recipe with{Surface=e.Recipe.Surface! with{Patches=[],Terrain=null,HeightField=field},
            NetworkRules=e.Recipe.NetworkRules!.Select(r=>r with{RoutingBoundary=r.Id=="planned-supply"?west:east}).ToArray(),
            Placement=placement with{Request=placement.Request with{
                Anchors=[..placement.Request.Anchors,..seed.PlacementPlan!.Placement.Sites.Select(s=>new WorldPlacementAnchor("fixed-"+s.DemandId,s.Position))],
                Relations=[..placement.Request.Relations,..seed.PlacementPlan.Placement.Sites.Select(s=>new WorldPlacementRelation("fixed-"+s.DemandId,s.DemandId,"fixed-"+s.DemandId,"Retain all source programs while sampled terrain changes","sampled:fixed",0,0,0,1))]}}};
        var session=new MetadataGenerationSession(generator,recipe,e.Catalog);var baseline=session.Snapshot;var observations=new List<object>();
        Validate(baseline.World);await Save("baseline",recipe,baseline.World);
        WorldMetadataRecipe Change(WorldMetadataRecipe source,int col,int row,double delta)
        {var h=source.Surface!.HeightField!.Heights.ToArray();h[row*11+col]+=delta;return source with{Surface=source.Surface with{HeightField=source.Surface.HeightField with{Heights=h}}};}
        // This lies in the eastern domain, outside the western dependency halo.
        var eastEdit=Change(recipe,9,4,3);
        Apply("east-height",eastEdit,["field-paths"],["planned-supply"]);
        Check(Costs(session.Snapshot.World,"field-paths")!=Costs(baseline.World,"field-paths"),"eastern terrain changes measured walking costs under the original permissive slope profile");
        var westEdit=Change(eastEdit,2,4,3);
        Apply("both-height",westEdit,["planned-supply"],["field-paths"]);
        Check(Geometry(session.Snapshot.World,"planned-supply")!=Geometry(baseline.World,"planned-supply"),"actual western slope barrier changes paths");
        // Restore the original authored double exactly. Addition/subtraction can
        // leave a different bit pattern and correctly invalidate a dependency key.
        var restoredHeights=westEdit.Surface!.HeightField!.Heights.ToArray();restoredHeights[4*11+9]=field.Heights[4*11+9];
        var onlyWest=westEdit with{Surface=westEdit.Surface with{HeightField=field with{Heights=restoredHeights}}};
        Apply("restore-east",onlyWest,["field-paths"],["planned-supply"]);
        Check(Geometry(session.Snapshot.World,"field-paths")==Geometry(baseline.World,"field-paths"),"restoring east terrain restores its paths");
        Apply("restore-all",recipe,["planned-supply"],["field-paths"]);
        // The region seam is shared, including positive-index triangle ownership.
        var seam=Change(recipe,5,4,.25);
        Apply("shared-seam",seam,["field-paths","planned-supply"],[]);
        var accepted=session.Snapshot;
        var invalid=Change(seam,10,8,100000000);
        Reject(()=>session.Apply(new("invalid-remote","Do not bypass full source validation","sampled:bad",Hash(accepted.Recipe),Hash(accepted.Catalog),invalid,e.Catalog)),"WORLD_HEIGHTFIELD_HEIGHT_INVALID");
        Check(Json(session.Snapshot)==Json(accepted),"invalid remote value cannot enter accepted cache/source");
        using(var cancellation=new CancellationTokenSource())
        {cancellation.Cancel();try{session.Apply(new("cancel","Cancelled edit","sampled:cancel",Hash(accepted.Recipe),Hash(accepted.Catalog),recipe,e.Catalog),cancellation.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}}
        Check(Json(session.Snapshot)==Json(accepted),"cancelled edit preserves accepted state");
        await session.PublishAsync(Path.Combine(output,"session"));var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session","generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"source-history checkpoint replay");
        await File.WriteAllTextAsync(Path.Combine(output,"observations.json"),Json(new{ExactPrimitiveSamples=samples,ExactEnvelopeComparisons=envelopes,Edits=observations}));
        Console.WriteLine("PASS: original-lattice regional field windows, exact sample parity, shared dependency/feature cells, remote/local/seam invalidation, all 24 nodes/21 physical sources, unchanged budgets, exact clean-world/work equality, full source validation, rollback and checkpoint replay.");
        void Apply(string id,WorldMetadataRecipe next,string[] computed,string[] reused)
        {
            var old=session.Snapshot;var result=session.Apply(new(id,"Actual sampled terrain dependency edit","sampled:edit",Hash(old.Recipe),Hash(old.Catalog),next,e.Catalog));
            var records=result.Report.NetworkExecutions??[];
            Check(records.Where(r=>r.Source=="Computed").Select(r=>r.RuleId).Order().SequenceEqual(computed.Order()),id+" computed policies");
            Check(records.Where(r=>r.Source=="Accepted").Select(r=>r.RuleId).Order().SequenceEqual(reused.Order()),id+" accepted policies");
            Check(Json(result.World)==Json(generator.Generate(next,e.Catalog)),id+" exact clean world/work");
            Check(Json(result.World.Nodes)==Json(baseline.World.Nodes)&&Json(result.World.AccessPlan!.Sites)==Json(baseline.World.AccessPlan!.Sites),id+" original physical geometry/poses");
            Check(result.World.Surface!.HeightField!.Heights.Count==99,id+" complete source heights preserved");
            Validate(result.World);observations.Add(new{Edit=id,Records=records,result.Report.WorldSha256});Save(id,next,result.World).GetAwaiter().GetResult();
        }
        async Task Save(string mode,WorldMetadataRecipe r,WorldMetadataDocument expected)
        {
            string folder=Path.Combine(output,mode,"input");Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"recipe.json"),Json(r));await File.WriteAllTextAsync(Path.Combine(folder,"catalog.json"),Json(e.Catalog));
            await generator.GenerateFilesAsync(Path.Combine(folder,"recipe.json"),Path.Combine(folder,"catalog.json"),Path.Combine(output,mode,"world"));
            Check(Json(await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(output,mode,"world","world-metadata.json")))==Json(expected),mode+" exact published read replay");
        }
    }
    private static void Validate(WorldMetadataDocument world)
    {
        Check(world.Nodes.Count==24&&world.AccessPlan!.Sites.Count==21&&world.PlacementPlan!.ProgramInstances!.Count==7,"complete source program");
        var query=new MetadataPhysicalAccessPlanner().RouteSurface(world.Surface!,world.AccessPlan!);
        foreach(var network in world.NetworkPlan!.Networks)
        {
            var rule=world.NetworkPlan.Rules.Single(r=>r.Id==network.RuleId);var local=query.RestrictTo(rule.RoutingBoundary!);
            Check(network.Plan.Analysis.Components.Count==1,"required connectivity");
            foreach(var edge in network.Plan.Edges)foreach(var route in new[]{edge.Forward,edge.Reverse})
                Check(local.ContainsCorridor(route.Path!,network.Plan.Options.Profile.Radius),"complete physical envelope");
        }
    }
    private static string Geometry(WorldMetadataDocument world,string rule)=>Json(world.NetworkPlan!.Networks.Single(n=>n.RuleId==rule).Plan.Edges.Select(e=>new{e.Link,e.Forward.Path,Reverse=e.Reverse.Path}));
    private static string Costs(WorldMetadataDocument world,string rule)=>Json(world.NetworkPlan!.Networks.Single(n=>n.RuleId==rule).Plan.Edges.Select(e=>new{e.Forward.Cost,Reverse=e.Reverse.Cost}));
    private static int PrimitiveChecks()
    {
        int count=0;
        foreach(var origin in new[]{new WorldPoint(-3.1,-7.3),new WorldPoint(0,0),new WorldPoint(.125,-.375)})
        {
            var f=new WorldHeightField(1,"original","ground","Fractional original samples","sampled:test","meters",origin,.7,1.1,19,17,
                Enumerable.Range(0,323).Select(i=>Math.Sin(i*.1)+(i%19)*.07).ToArray());
            var boundary=Box(origin.X+3*.7,origin.Y+4*1.1,5*.7,6*1.1);
            var global=new WorldSurfaceQuery(new(1,"original","meters",Box(origin.X-2,origin.Y-2,20,25),[],[],HeightField:f));var local=global.RestrictTo(boundary);
            for(int y=0;y<=24;y++)for(int x=0;x<=20;x++)
            {var p=new WorldPoint(boundary.Vertices[0].X+x*5*.7/20,boundary.Vertices[0].Y+y*6*1.1/24);Check(Json(global.Sample(p))==Json(local.Sample(p)),"bit-exact original-coordinate sample");count++;}
            var window=local.RegionalFieldWindow!;Check(window.Samples(global.HeightField!,CancellationToken.None).Count<f.Heights.Count,"bounded window reduces sampled dependencies");
            var remote=f.Heights.ToArray();remote[^1]+=100;
            var changed=new WorldSurfaceQuery(global.Definition with{HeightField=f with{Heights=remote}}).RestrictTo(boundary);
            Check(Hash(local.RouteDependencies(default))==Hash(changed.RouteDependencies(default)),"remote sample outside the window preserves dependency hash");
            var near=f.Heights.ToArray();near[window.FirstRow*f.Columns+window.FirstColumn]+=.5;
            var nearQuery=new WorldSurfaceQuery(global.Definition with{HeightField=f with{Heights=near}}).RestrictTo(boundary);
            Check(Hash(local.RouteDependencies(default))!=Hash(nearQuery.RouteDependencies(default)),"halo vertex is included in dependency hash");
            var outside=global.RestrictTo(Box(origin.X+15,origin.Y+20,1,1));
            Check(outside.RegionalFieldWindow!.Samples(global.HeightField!,default).Count==0&&outside.Sample(new(origin.X+15.5,origin.Y+20.5)).Status==WorldSurfaceStatus.Uncovered,"outside field has no sampled dependencies");
            var partial=global.RestrictTo(Box(origin.X-1,origin.Y-1,2,2));
            Check(partial.RegionalFieldWindow!.FirstColumn==0&&partial.RegionalFieldWindow.FirstRow==0,"partly covered region retains first cells");
            var whole=global.RestrictTo(global.Definition.Boundary!);
            Check(whole.RegionalFieldWindow!.Samples(global.HeightField!,default).Count==f.Heights.Count,"containing region retains all source samples");
            Check(local.Definition.HeightField!.Origin==f.Origin&&local.Definition.HeightField.Columns==f.Columns&&local.Definition.HeightField.Heights.SequenceEqual(f.Heights),"source lattice/buffer is never translated or replaced");
            using var cancellation=new CancellationTokenSource();cancellation.Cancel();
            try{local.RouteDependencies(cancellation.Token);throw new Exception("Dependency cancellation ignored");}catch(OperationCanceledException){}
        }
        return count;
    }
    private static int EnvelopeChecks()
    {
        int count=0;
        foreach(bool raised in new[]{false,true})foreach(bool mask in new[]{false,true})foreach(bool regional in new[]{false,true})
        {
            var values=Enumerable.Range(0,81).Select(i=>(i%9)*.2+(i/9)*.3).ToArray();if(raised)values[4*9+4]+=4;
            var field=new WorldHeightField(1,"proof-field","ground","Envelope proof parity","sampled:proof","meters",new(0,0),10,10,9,9,values);
            var definition=new WorldSurfaceDefinition(1,"proof-surface","meters",Box(0,0,80,80),
                mask?[new("protected","Keep protected land","sampled:mask",Box(11,22,7,10))]:[],[],HeightField:field);
            var query=new WorldSurfaceQuery(definition);if(regional)query=query.RestrictTo(new([new(0,0),new(60,0),new(60,25),new(45,25),new(45,80),new(0,80)]));
            var profile=new WorldTraversalProfile("proof","Proof profile","sampled:proof",.6,.1,new Dictionary<string,double>{{"ground",1}});
            var traversal=new WorldSurfaceTraversal(query,profile);var exact=new WorldSurfaceAreaQuery(query,false);
            foreach(double x in new[]{.5,11,29.5,40,60,79.5})foreach(double y in new[]{.5,22,39.5,60})
            {
                var envelope=WorldSurfaceTraversal.Envelope(new(x,y),new(x+3,y+5),profile.Radius);
                bool expected=exact.Matches(envelope,traversal.Allows);
                bool actual=traversal.AllowsEnvelope(envelope,new WorldSpatialWorkBudget(20000000,default),default);
                Check(actual==expected,$"exact sampled envelope parity raised={raised} mask={mask} regional={regional} at {x},{y}");count++;
            }
        }
        return count;
    }
    private static void Reject(Action action,string code)
    {try{action();throw new Exception("Expected refusal: "+code);}catch(ArgumentException e)when(e.Message.Contains(code,StringComparison.Ordinal)||e.InnerException?.Message.Contains(code,StringComparison.Ordinal)==true){}}
}
