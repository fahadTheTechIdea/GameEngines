using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionDemandTests
{
    private static async Task BoundariesAsync(string output,(WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) input,WorldMetadataDocument world)
    {
        var planner=new MetadataRegionPlanner();var plan=world.RegionDemandPlan!;var definition=plan.Definition;
        MetadataRegionDemandPlan Measure(MetadataRegionDemandDefinition d,MetadataPlacementDefinition? p=null)=>planner.Measure(d,input.Recipe.Regions!,p??input.Recipe.Placement!,world.Nodes,world.Space,world.FootprintDefinitions!,world.StructureDefinitions,world.AccessPlan!.Definitions,CancellationToken.None);
        Check(Json(Measure(definition))==Json(plan),"source measurements replay from original catalog geometry");
        Reject(()=>Measure(definition with{Rules=[definition.Rules[0],definition.Rules[0]]}),"METADATA_REGION_DEMAND_RULE_INVALID");
        foreach(double fraction in new[]{0d,-.1,1.01,double.NaN,double.PositiveInfinity})Reject(()=>Measure(definition with{Rules=[definition.Rules[0] with{MaximumCoverage=fraction}]}),"METADATA_REGION_DEMAND_RULE_INVALID");
        Reject(()=>Measure(definition with{MaximumWork=plan.Work-1}),"METADATA_REGION_DEMAND_WORK_BUDGET");
        Check(Measure(definition with{MaximumWork=plan.Work}).Work==plan.Work,"exact measured work boundary");
        Reject(()=>Measure(definition with{Rules=[definition.Rules[0] with{RegionId="absent"}]}),"METADATA_REGION_DEMAND_RULE_INVALID");
        Reject(()=>Measure(definition,input.Recipe.Placement! with{Request=input.Recipe.Placement.Request with{Demands=input.Recipe.Placement.Request.Demands.Select(d=>d with{RegionId=null}).ToArray()}}),"METADATA_REGION_DEMAND_SOURCE_REQUIRED");
        using(var c=new CancellationTokenSource())
        {c.Cancel();try{planner.Measure(definition,input.Recipe.Regions!,input.Recipe.Placement!,world.Nodes,world.Space,world.FootprintDefinitions!,world.StructureDefinitions,world.AccessPlan!.Definitions,c.Token);throw new Exception("Missing cancellation");}catch(OperationCanceledException){}}
        foreach(var altered in new[]{world with{RegionDemandPlan=null},world with{RegionDemandId="forged"},
            world with{RegionDemandPlan=plan with{Results=[]}},world with{RegionDemandPlan=plan with{Work=plan.Work+1}},
            world with{RegionDemandPlan=plan with{DerivedRequest=plan.DerivedRequest with{Intents=plan.DerivedRequest.Intents!.Select(i=>i with{MinimumArea=1}).ToArray()}}}})
            Reject(()=>planner.ValidateDocument(altered),"METADATA_REGION_DEMAND_");
        var source=plan.Results[0].Sources[0];
        var shifted=source.Source with{Polygon=new(source.Source.Polygon.Vertices.Select(p=>p.Offset(7,-3)).ToArray()),Reservation=Box(-12,-8,30,20)};
        // Direct owner qualification isolates source geometry from later access/packing rules.
        var definitions=world.FootprintDefinitions!.ToDictionary(p=>p.Key,p=>p.Value);
        definitions["compound"]=definitions["compound"] with{Polygon=shifted.Polygon,Reservation=shifted.Reservation};
        var rotated=input.Recipe.Placement! with{Bindings=input.Recipe.Placement.Bindings.Select(b=>b with{RotationDegrees=90}).ToArray()};
        var offset=planner.Measure(definition,input.Recipe.Regions!,rotated,world.Nodes,world.Space,definitions,world.StructureDefinitions,null,CancellationToken.None);
        var measured=offset.Results.Single(r=>r.Rule.RegionId=="alpha").Sources.Single();
        Near(measured.EnvelopeArea,600,"reservation owns complete required envelope area");Near(measured.AnchorRadius,Math.Sqrt(18*18+12*12),"rotation preserves offset envelope's radius about actual origin");
        await File.WriteAllTextAsync(Path.Combine(output,"offset-rotated-reservation.json"),Json(offset));
        var root=world.Nodes.Single(n=>n.ParentId is null);
        var repeatedNodes=Enumerable.Range(0,3).Select(i=>world.Nodes.Single(n=>n.Id.EndsWith("/first-0",StringComparison.Ordinal)) with{Id=root.Id+"/repeated-"+i}).Prepend(root).ToArray();
        var repeated=input.Recipe.Placement! with{Bindings=[new("all",root.Id,true,PartId:"repeated")],
            Request=input.Recipe.Placement.Request with{Demands=[input.Recipe.Placement.Request.Demands[0] with{Id="all",RegionId="alpha"}],Relations=[]}};
        var expanded=planner.Measure(definition with{Rules=[definition.Rules[0]]},input.Recipe.Regions!,repeated,repeatedNodes,world.Space,world.FootprintDefinitions!,world.StructureDefinitions,null,CancellationToken.None);
        Check(expanded.Results.Single().Sources.Count==3,"part binding counts actual expanded root members");Near(expanded.Results.Single().RequiredArea,3000,"all repeated source demand retained");
        await File.WriteAllTextAsync(Path.Combine(output,"repeated-measurement.json"),Json(expanded));
    }
}
