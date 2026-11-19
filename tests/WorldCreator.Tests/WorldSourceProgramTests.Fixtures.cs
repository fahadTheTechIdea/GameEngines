using System.Text.Json;
using WorldCreator.Core.Geometry;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldSourceProgramTests
{
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) Example(bool smaller=false,bool ownChoices=false)
    {
        var basis=WorldSpatialProgramAnalysisTests.AdaptedSourceExample(smaller);var population=basis.Recipe.PopulationSource!;
        var site=population.Catalog.Archetypes.Single(a=>a.Id=="site");
        var capacity=new MetadataCatalogCapacity("site","units",1,"One complete site program","source-program:capacity");
        var rule=new MetadataProgramMixRule("source-sites","Select complete physically qualified source sites","source-program:sites",
            [new("oversized-site",1,MaximumCount:2),new("site",2,MaximumCount:2)],[new("site","units",2)],"cost",4,2);
        WorldMetadataArchetype Change(WorldMetadataArchetype a)=>a.Id=="site"?a with{Capacities=[capacity]}:
            a.Id!=population.Recipe.RootArchetypeId?a:a with{Parts=a.Parts.Select(p=>p.Id=="sites"?p with{ProgramMix=rule}:p).ToArray()};
        var oversized=site with{Id="oversized-site",Capacities=[capacity],PhysicalFootprint=site.PhysicalFootprint! with{
            Polygon=Box(-50,-3,100,6),Reservation=Box(-51,-4,102,8)},Access=site.Access! with{Ports=site.Access.Ports.Select(p=>p.Id=="east"?
                p with{BoundaryPosition=new(50,0),ApproachPosition=new(51,0)}:p.Id=="west"?p with{BoundaryPosition=new(-50,0),ApproachPosition=new(-51,0)}:p).ToArray()}};
        var sourceCatalog=population.Catalog with{Archetypes=[..population.Catalog.Archetypes.Select(Change),oversized]};
        var sourceRecipe=population.Recipe with{ProgramComposition=new("source-choices","Qualify every complete upstream program","source-program:composition",MaximumAttempts:16)};
        var catalog=basis.Catalog with{Archetypes=[..basis.Catalog.Archetypes.Select(Change),oversized]};
        var recipe=basis.Recipe with{ProgramComposition=null,PopulationSource=new(sourceRecipe,sourceCatalog)};
        if(ownChoices)
        {
            var token=catalog.Archetypes.Single(a=>a.Id=="habitat-token");var unit=new MetadataCatalogCapacity("discovery","units",1,"One complete discovery","source-program:discovery");
            var options=new MetadataProgramMixRule("discoveries","Independent downstream discovery choice","source-program:downstream",
                [new("habitat-token",1,MaximumCount:3),new("alternate-token",2,MaximumCount:3)],[new("discovery","units",2)],"cost",6,3);
            catalog=catalog with{Archetypes=[..catalog.Archetypes.Select(a=>a.Id==token.Id?a with{Capacities=[unit]}:a.Id!=recipe.RootArchetypeId?a:a with{
                Parts=a.Parts.Select(p=>p.Id=="tokens"?p with{ProgramMix=options}:p).ToArray()}),token with{Id="alternate-token",Capacities=[unit]}]};
            recipe=recipe with{ProgramComposition=new("consumer-choices","Choose only newly added complete programs","source-program:consumer",RequiredRanks:[0])};
        }
        return(recipe,catalog);
    }
    internal static (WorldMetadataRecipe Recipe,WorldMetadataCatalog Catalog) RegionExample()
    {
        var source=WorldRegionFeedbackTests.Example(regionOnly:true);var root=source.Catalog.Archetypes.Single(a=>a.Id==source.Recipe.RootArchetypeId);
        var token=new WorldMetadataArchetype("source-discovery","discovery","Required source-habitat discovery",new Dictionary<string,JsonElement>(),[],[],[],
            PhysicalFootprint:new("meters","Unchanged discovery size","source-program:discovery",Box(-.2,-.2,.4,.4)),Access:new(MetadataOccupancyKind.Solid,[]));
        var catalog=source.Catalog with{Archetypes=[..source.Catalog.Archetypes.Select(a=>a.Id!=root.Id?a:a with{
            Parts=[..a.Parts,new("discoveries",token.Id,2,"Every required habitat discovery")]}),token]};
        var request=source.Recipe.Placement!.Request;
        var recipe=source.Recipe with{ProgramComposition=null,PopulationSource=new(source.Recipe,source.Catalog),
            TerrainDemands=new([new("discoveries","Measure the qualified source component","source-program:habitat",source.Recipe.Id,"discoveries","habitat",1000)]),
            Placement=source.Recipe.Placement with{Bindings=[..source.Recipe.Placement.Bindings,new("discoveries",source.Recipe.Id,true,PartId:"discoveries",
                Population:new("Southern approach discovery belt","source-program:belt",Path:new(new([new(62,8.5),new(80,8.5)]),3,1)))],
                Request=request with{Demands=[..request.Demands,new("discoveries","Retain every required discovery","source-program:discovery",null,request.Demands[0].Profile,RegionId:"habitat")]}}};
        return(recipe,catalog);
    }
}
