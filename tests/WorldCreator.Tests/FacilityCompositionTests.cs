using System.Text.Json;
using WorldCreator.Catalogs.Modern;
using WorldCreator.Core;
using WorldCreator.Core.Planning;
using WorldCreator.Worlds;

internal static class FacilityCompositionTests
{
    public static Task RunAsync(string? cli=null)
    {
        var modern=new ModernCatalog();
        var registry=new SettlementCatalogRegistry(CatalogContentAuthoring.Apply([modern],[CatalogContentAuthoringTests.Content()]));
        var planner=new FacilityCompositionPlanner(registry);
        foreach(var id in new[]{"modern.factory-campus","modern.football-club","user.learning-and-care","user.home-program"})
        {
            var plan=planner.PlanGeometry(ModernCatalog.Selection,id);
            Check(plan.Buildings.Count>=2&&plan.Network.IsConnected(),"reusable nested facility and connected roads: "+id);
            Check(JsonSerializer.Serialize(plan)==JsonSerializer.Serialize(planner.PlanGeometry(ModernCatalog.Selection,id)),"determinism");
            foreach(var member in plan.Buildings)
            {
                var group=plan.Groups.Single(g=>g.Id==member.OwnerId);
                var slot=plan.Templates.Single(t=>t.Id==group.TemplateId).Structures.Single(s=>s.Id==member.SlotId);
                var origin=group.Boundary.Vertices[0];
                Check(member.Site.Vertices.SequenceEqual(slot.Site.Vertices.Select(p=>p.Offset(origin.X,origin.Y))),"exact source slot with nested offset");
                Check(member.Components.SequenceEqual(slot.Components),"component facts preserved");
                Check(Math.Abs(member.Footprint.Vertices[0].DistanceTo(member.Footprint.Vertices[1])-member.Source.Variant.WidthMeters)<1e-7,"unscaled source width");
                Check(Math.Abs(member.Footprint.Vertices[1].DistanceTo(member.Footprint.Vertices[2])-member.Source.Variant.DepthMeters)<1e-7,"unscaled source depth");
                Check(group.Boundary.ContainsPolygon(member.Site)&&member.Site.ContainsPolygon(member.Footprint),"nested ownership and envelope containment");
            }
        }
        var imports=new CatalogImport[]{new("modern",["office-building","clinic","football-training-ground","warehouse","factory","workshop"],modern.Facilities.Select(f=>f.Id).ToArray())};
        var composition=new CatalogCompositionDefinition("test.composed","Positive source imports",imports);
        var combined=new SettlementCatalogRegistry(SettlementCatalogComposition.Register([modern],[composition]));
        var descriptor=combined.Catalogs.Single(c=>c.Id==composition.Id);var selection=new SettlementCatalogSelection(descriptor.Id,descriptor.Capabilities);
        var composed=new FacilityCompositionPlanner(combined).PlanGeometry(selection,"modern.football-club");
        Check(composed.Catalog.Composition!.FacilityOrigins["modern.football-club"]=="modern"&&composed.Buildings.All(b=>b.Source.Catalog.Id==composition.Id),"composed source provenance retained by actual geometry");
        Reject(()=>planner.PlanGeometry(ModernCatalog.Selection,"missing"),"FACILITY_TEMPLATE_UNKNOWN");
        using(var cancel=new CancellationTokenSource()){cancel.Cancel();try{planner.PlanGeometry(ModernCatalog.Selection,"modern.factory-campus",cancel.Token);throw new Exception("Cancellation missing");}catch(OperationCanceledException){}}
        var invalid=MockupGenerationFixture.Create();
        invalid=invalid with{Design=invalid.Design! with{BuildingCatalog=[],BuildingCatalogIds=["missing.part"]}};
        Check(new MockupDesignValidator(registry).Validate(invalid).Any(i=>i.Code=="CATALOG_STRUCTURE_REFERENCE_UNKNOWN:missing.part"),"design validation preserves structured catalog errors");
        Check(new SettlementPlanner(registry).Plan(invalid).Diagnostics.Any(i=>i.Code=="CATALOG_STRUCTURE_REFERENCE_UNKNOWN:missing.part"),"settlement boundary preserves catalog errors");
        Check(new SettlementDesignPipeline(registry).Build(invalid).Diagnostics.Contains("CATALOG_STRUCTURE_REFERENCE_UNKNOWN:missing.part"),"design pipeline preserves catalog errors");
        try{new MockupWorldGenerator(registry).GenerateAsync(invalid).GetAwaiter().GetResult();throw new Exception("Missing compiler refusal");}
        catch(WorldGenerationException ex){Check(ex.Issues.Contains("CATALOG_STRUCTURE_REFERENCE_UNKNOWN:missing.part"),"compiler error type preserved");}
        foreach(string removed in new[]{"FacilityPlacements","PlannedFacilities"})
        {
            var input=JsonSerializer.SerializeToNode(MockupGenerationFixture.Create().Design,WorldJson.CreateOptions())!.AsObject();input[removed]=null;
            try{JsonSerializer.Deserialize<MockupWorldDesign>(input,WorldJson.CreateOptions());throw new Exception("Removed facility state accepted");}catch(JsonException){}
        }
        Console.WriteLine("PASS: one facility geometry compiler, nested ownership, exact variants/components, explicit imports/provenance, unknown templates, cancellation and removed mockup-state refusal.");
        return Task.CompletedTask;
    }
    private static void Check(bool ok,string message){if(!ok)throw new Exception("Composition: "+message);}
    private static void Reject(Action act,string code){try{act();}catch(ArgumentException e)when(e.Message.StartsWith(code,StringComparison.Ordinal)){return;}throw new Exception("Expected "+code);}
}
