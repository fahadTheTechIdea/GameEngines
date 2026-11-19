using System.Text.Json;
using WorldCreator.Core.Metadata;
using WorldCreator.Core.Spatial;

internal static partial class WorldRegionalJointTests
{
    internal static MetadataRegionalOwnershipRequest Example(bool larger=false,bool terrainEdit=false)
    {
        var input=WorldRegionalCouplingTests.Example(larger:larger,movedVoid:terrainEdit);
        var chooseParent=new MetadataProgramMixRule("discovery-parents","Complete parent programs for every habitat-driven discovery","joint:parents",
            [new("landmark",1),new("landmark-alt",2)],[new("function","units",1)],"parent-budget",256,128);
        var chooseMarker=new MetadataProgramMixRule("marker-option","One full original marker in every discovery","joint:marker",
            [new("marker",1,MaximumCount:1)],[new("marker","units",1)],"marker-budget",1,1);
        var marker=new WorldMetadataArchetype("marker","marker","Required2.4m nested discovery marker",new Dictionary<string,JsonElement>(),[],[],[],
            PhysicalFootprint:new("meters","Original2.4m marker in both parent alternatives","joint:marker",Box(-1.2,-1.2,2.4,2.4)),
            Access:new(MetadataOccupancyKind.Reservation,[]),Capacities:[new("marker","units",1,"Required complete marker","joint:marker")]);
        return input with{ProgramComposition=new("Resolve whole source programs and actual habitat counts","joint:choices"),Regions=input.Regions.Select(region=>{
            var recipe=region.Recipe;var profile=region.Profile;
            var original=region.Catalog.Archetypes.Single(a=>a.Id=="landmark") with{Parts=[new("marker","marker",1,"Every required complete marker",ProgramMix:chooseMarker)],
                ChildRequirements=[new("marker",1,1)],Capacities=[new("function","units",1,"One complete discovery program","joint:discovery")]};
            var alternative=original with{Id="landmark-alt",PhysicalFootprint=new("meters","Declared3.2m parent for the same original marker","joint:larger-parent",
                Box(-1.6,-1.6,3.2,3.2),Box(-2.1,-2.1,4.2,4.2))};
            var catalog=region.Catalog with{Archetypes=[..region.Catalog.Archetypes.Select(a=>a.Id=="landmark"?original:a.Id!=recipe.RootArchetypeId?a:a with{
                Parts=a.Parts.Select(p=>p.Id!="landmarks"?p:p with{ProgramMix=chooseParent}).ToArray()}),alternative,marker]};
            var children=new WorldPlacementRequest("markers","Retain each complete nested marker","joint:marker",null,
                [new("marker","Complete marker","joint:marker",null,profile)],[new("marker-origin",new(0,0))],
                [new("marker-pin","marker","marker-origin","Source-local discovery center","joint:marker",0,0,0,1)],Execution:WorldPlacementExecution.Prescribed);
            var programs=new[]{"landmark","landmark-alt"}.Select(id=>new MetadataNestedProgram("markers-"+id,"Expand every actual discovery parent","joint:markers",recipe.Id,id,0,128,
                children,[new("marker","marker",true)])).ToArray();
            return region with{Catalog=catalog,Recipe=recipe with{Placement=recipe.Placement! with{Programs=[..recipe.Placement!.Programs!,..programs]},
                ProgramComposition=new("dynamic-choices","Complete choices follow actual habitat","joint:choices",MaximumAttempts:64)}};
        }).ToArray()};
    }
}
