using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static class WorldNetworkPolicyTests
{
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value,WorldJson.CreateOptions());
    private static string Hash<T>(T value)=>Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Json(value))));
    private static WorldPolygon Box(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)]);
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example()
    {
        var e=WorldMixedRouteRegionTests.Example();string id=e.Recipe.Id;
        var mapping=Enumerable.Range(0,7).ToDictionary(i=>id+"/sites-"+i,i=>id+(i<4?"/planned-0/sites-"+i:"/field-0/sites-"+(i-4)),StringComparer.Ordinal);
        var root=e.Catalog.Archetypes.Single(a=>a.Id==e.Recipe.RootArchetypeId);var part=root.Parts.Single();
        WorldMetadataArchetype Scope(string name,string region,int count)=>new(name,"policy-region","Own the declared "+region+" program and its network policy",
            new Dictionary<string,JsonElement>{{"region-id",JsonSerializer.SerializeToElement(region)}},[part with{Count=count}],[],[new("site",count,count)],LayerId:"intent");
        var catalog=e.Catalog with{Archetypes=[..e.Catalog.Archetypes.Select(a=>a.Id==root.Id?a with{Parts=[
                new("planned","planned-scope",1,"Local planned program scope",Layout:MetadataLineLayout.Parameters([0,0],[0,0])),
                new("field","field-scope",1,"Irregular field program scope",Layout:MetadataLineLayout.Parameters([60,0],[0,0]))],ChildRequirements=[new("policy-region",2,2)]}:
            a.Id=="site"?a with{Access=a.Access! with{Ports=a.Access.Ports.Select(p=>p with{Width=1.6}).ToArray()}}:a),Scope("planned-scope","west",4),Scope("field-scope","east",3)]};
        var route=e.Recipe.RouteRules![0];
        var supply=route.Profile with{Id="supply-access",Purpose="Deliver to every required planned destination after one logical connection loss",SourceRuleId="policy:supply",Radius=.6,MaximumSlope=.1};
        var foot=route.Profile with{Id="field-foot",Purpose="Reach every field destination on foot",SourceRuleId="policy:foot",Guidance=null};
        var rules=new[]{new MetadataNetworkRule("planned-supply",id+"/planned-0","site",4,4,"street",new("Supply access within the planned program","policy:supply","local",supply,route.Search,
                Connectivity:WorldNetworkConnectivity.SingleConnectionFailure),"intent","east"),
            new("field-paths",id+"/field-0","site",3,3,"footpath",new("Foot access within the irregular program","policy:foot","landscape",foot,route.Search),"intent","west")};
        return(e.Recipe with{Placement=e.Recipe.Placement! with{Bindings=e.Recipe.Placement.Bindings.Select(b=>b with{NodeId=mapping[b.NodeId]}).ToArray()},
            AdditionalConnections=e.Recipe.AdditionalConnections!.Select(c=>c with{FromId=mapping[c.FromId],ToId=mapping[c.ToId]}).ToArray(),NetworkRules=rules,
            RemainingScopes=["Complete declared mixed-network policy reference: two explicit region scopes, seven physical programs and fourteen source-sized stations. Scale and semantic kind are policy labels; actual network behavior comes from profiles, demand and connectivity settings. No automatic urban land-use or traffic simulation. Artwork and Godot deferred."]},catalog);
    }
    public static async Task RunAsync(string output)
    {
        if(Directory.Exists(output)||File.Exists(output))throw new IOException("Policy evidence exists");Directory.CreateDirectory(output);
        var e=Example();var generator=new WorldMetadataGenerator();
        // Establish source-owned poses before applying the optional network policies.
        var initial=generator.Generate(e.Recipe with{NetworkRules=null},e.Catalog);var placement=e.Recipe.Placement!;
        var sites=initial.PlacementPlan!.Placement.Sites;
        var fixedRecipe=e.Recipe with{Placement=placement with{Request=placement.Request with{
            Anchors=[..placement.Request.Anchors,..sites.Select(s=>new WorldPlacementAnchor("fixed-"+s.DemandId,s.Position))],
            Relations=[..placement.Request.Relations,..sites.Select(s=>new WorldPlacementRelation("fixed-"+s.DemandId,s.DemandId,"fixed-"+s.DemandId,"Preserve source program pose across policy counterfactuals","policy:fixed",0,0,0,1))]}}};
        await WriteInput("baseline",fixedRecipe);
        var baseline=generator.Generate(fixedRecipe,e.Catalog);Validate(baseline,true);
        var supply=baseline.NetworkPlan!.Networks.Single(n=>n.RuleId=="planned-supply").Plan;
        var edge=supply.Edges.OrderBy(e=>e.Forward.Cost!.Distance).First();var points=edge.Forward.Path!.Points;
        int segment=Enumerable.Range(0,points.Count-1).OrderByDescending(i=>points[i].DistanceTo(points[i+1])).First();
        var center=new WorldPoint((points[segment].X+points[segment+1].X)/2,(points[segment].Y+points[segment+1].Y)/2);
        var water=fixedRecipe.Surface!.Patches[0] with{Id="supply-water",Kind="water",Priority=1,Boundary=Box(center.X-.5,center.Y-.5,1,1),Purpose="Block the prior selected supply passage",SourceRuleId="policy:water"};
        var wetRecipe=fixedRecipe with{Surface=fixedRecipe.Surface with{Patches=[..fixedRecipe.Surface.Patches,water]}};await WriteInput("water",wetRecipe);
        var wet=generator.Generate(wetRecipe,e.Catalog);Validate(wet,true);
        Check(Json(wet.NetworkPlan!.Networks.Single(n=>n.RuleId=="planned-supply").Plan.Edges.Select(e=>e.Forward.Path))!=Json(supply.Edges.Select(e=>e.Forward.Path)),"water changes actual supply passage geometry or selected links");
        var roadlessRecipe=fixedRecipe with{NetworkRules=fixedRecipe.NetworkRules!.Where(r=>r.Id!="planned-supply").ToArray()};await WriteInput("roadless",roadlessRecipe);
        var roadless=generator.Generate(roadlessRecipe,e.Catalog);Validate(roadless,false);
        Check(roadless.Connections.All(c=>c.Kind!="street")&&!roadless.NetworkPlan!.Rules.Any(r=>r.Id=="planned-supply"),"roadless variant emits no supply streets");
        var renamedRecipe=fixedRecipe with{NetworkRules=fixedRecipe.NetworkRules!.Select(r=>r with{ConnectionKind=r.Id=="planned-supply"?"supply-lane":"scout-trail"}).ToArray()};await WriteInput("renamed",renamedRecipe);
        var renamed=generator.Generate(renamedRecipe,e.Catalog);Validate(renamed,true);
        Check(Json(renamed.NetworkPlan!.Networks)==Json(baseline.NetworkPlan.Networks),"semantic connection kinds do not select another geometry algorithm");
        foreach(var world in new[]{wet,roadless,renamed})
        {
            Check(Json(world.Nodes)==Json(baseline.Nodes),"all hierarchy/poses/source facts unchanged");
            Check(Json(world.AccessPlan!.Sites)==Json(baseline.AccessPlan!.Sites),"all physical source geometry unchanged");
        }
        var wrongScope=fixedRecipe with{NetworkRules=fixedRecipe.NetworkRules!.Select(r=>r.Id=="planned-supply"?r with{RegionNodeId=fixedRecipe.Id}:r).ToArray()};
        Reject(()=>generator.Generate(wrongScope,e.Catalog),"METADATA_NETWORK_DESTINATION_COUNT");
        var narrow=e.Catalog with{Archetypes=e.Catalog.Archetypes.Select(a=>a.Id=="site"?a with{Access=a.Access! with{Ports=a.Access.Ports.Select(p=>p.Id=="east"?p with{Width=.8}:p).ToArray()}}:a).ToArray()};
        Reject(()=>generator.Generate(fixedRecipe,narrow),"METADATA_ACCESS_ROUTE_TOO_WIDE");
        var session=new MetadataGenerationSession(generator,fixedRecipe,e.Catalog);var old=session.Snapshot;
        var changed=session.Apply(new("roadless","Remove only the optional supply network","policy:edit",Hash(old.Recipe),Hash(old.Catalog),roadlessRecipe,e.Catalog));
        Check(Json(changed.World)==Json(roadless),"policy edit equals clean generation");
        try{session.Apply(new("wrong-scope","Reject unsatisfied scoped demand","policy:bad-edit",Hash(changed.Recipe),Hash(changed.Catalog),wrongScope,e.Catalog));throw new Exception("Wrong scope accepted");}
        catch(MetadataRegenerationException error)when(error.EditId=="wrong-scope"&&error.ReasonCode=="METADATA_NETWORK_DESTINATION_COUNT:planned-supply")
        {Check(error.StageId=="allocation"&&error.InnerException is ArgumentException,"failed policy edit retains exact stage and original error");}
        Check(Json(session.Snapshot)==Json(changed),"failed scoped edit preserves accepted world/report");
        foreach(var item in new[]{(Id:"baseline",World:baseline),(Id:"water",World:wet),(Id:"roadless",World:roadless),(Id:"renamed",World:renamed)})
        {
            string input=Path.Combine(output,item.Id,"input"),target=Path.Combine(output,item.Id,"world");
            await generator.GenerateFilesAsync(Path.Combine(input,"recipe.json"),Path.Combine(input,"catalog.json"),target);
            var saved=await WorldMetadataGenerator.ReadAsync<WorldMetadataDocument>(Path.Combine(target,"world-metadata.json"));Check(Json(saved)==Json(item.World),"exact policy file replay "+item.Id);
        }
        await session.PublishAsync(Path.Combine(output,"session"));
        var restored=await MetadataGenerationSession.RestoreAsync(generator,Path.Combine(output,"session","generation-session.json"));
        Check(Json(restored.Snapshot)==Json(session.Snapshot),"published policy checkpoint restores exact accepted world and edit report");
        Console.WriteLine("PASS: hierarchy-scoped supply/footpath data policies, retained 24-node/21-physical-source program, distinct radii/entrances/connectivity, exact cross-policy passage endpoints, fixed-pose water/roadless/semantic-rename counterfactuals, wrong-scope/narrow-port refusal, API/file/session replay, checkpoint restoration and rollback.");
        async Task WriteInput(string mode,WorldMetadataRecipe recipe)
        {
            string input=Path.Combine(output,mode,"input");Directory.CreateDirectory(input);
            await File.WriteAllTextAsync(Path.Combine(input,"recipe.json"),Json(recipe));await File.WriteAllTextAsync(Path.Combine(input,"catalog.json"),Json(e.Catalog));
        }
    }
    private static void Validate(WorldMetadataDocument world,bool withSupply)
    {
        Check(world.Nodes.Count==24&&world.AccessPlan!.Sites.Count==21&&world.PlacementPlan!.ProgramInstances!.Count==7,"complete hierarchy and all physical parent/child source members");
        Check(world.NetworkPlan!.Networks.Count==(withSupply?2:1),"exact selected policy count");
        foreach(var network in world.NetworkPlan.Networks)
        {
            var rule=world.NetworkPlan.Rules.Single(r=>r.Id==network.RuleId);int count=rule.Id=="planned-supply"?4:3;
            Check(network.Plan.Destinations.Count==count&&network.Plan.Analysis.Components.Count==1,"all required scoped destinations connected");
            if(rule.Id=="planned-supply")Check(network.Plan.Analysis.Bridges.Count==0&&network.Plan.Options.Profile.Radius==.6,"declared supply width and single-logical-link failure obligation");
            else Check(network.Plan.Options.Profile.Radius==.3&&network.Plan.Options.Profile.Guidance is null,"independent footpath policy");
            Check(network.Plan.Destinations.All(d=>d.Id.StartsWith(rule.RegionNodeId+"/",StringComparison.Ordinal)),"network cannot select another scope's destinations");
        }
        var regional=world.RoutePlan!.Routes.Single(r=>r.RuleId=="regional-route");
        var connection=world.Connections.Single(c=>c.Id==regional.ConnectionId);
        var to=world.NetworkPlan.Networks.Single(n=>n.RuleId=="field-paths").Plan.Destinations.Single(d=>d.Id==connection.ToId);
        Check(regional.Forward.Path!.Points[^1]==to.Position&&regional.Reverse!.Path!.Points[0]==to.Position,"cross-region passage exactly joins footpath entrance");
        if(withSupply)
        {
            var from=world.NetworkPlan.Networks.Single(n=>n.RuleId=="planned-supply").Plan.Destinations.Single(d=>d.Id==connection.FromId);
            Check(regional.Forward.Path.Points[0]==from.Position&&regional.Reverse!.Path!.Points[^1]==from.Position,"cross-region passage exactly joins supply entrance");
        }
        Check(world.CorridorPlan is null,"geometric crossings do not invent shared physical junctions");
        new MetadataNetworkPlanner().ValidateDocument(world);new MetadataRoutePlanner().ValidateDocument(world);new MetadataPlacementPlanner().ValidateDocument(world);
    }
    private static void Check(bool value,string message){if(!value)throw new Exception("Network policies: "+message);}
    private static void Reject(Action action,string code){try{action();}catch(ArgumentException error)when(error.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
