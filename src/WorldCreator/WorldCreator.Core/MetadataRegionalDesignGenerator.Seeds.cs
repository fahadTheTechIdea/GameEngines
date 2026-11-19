using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using WorldCreator.Core.Spatial;

namespace WorldCreator.Core.Metadata;

public sealed partial class MetadataRegionalDesignGenerator
{
    private sealed record SeededWorld(MetadataRegionalOwnershipRequest Template,string? SelectionSeed,
        IReadOnlyList<MetadataRegionalDesignSeedDerivation>? Derivations,long? CaptureValidationWork=null);

    private static void ValidateSeedPlan(MetadataRegionalDesignRequest source)
    {
        if(source.SeedPlan is not {} plan)return;
        if(!WorldEnvironmentQuery.Text(plan.Seed)||plan.Seed.Length>256||!plan.TerrainDetail&&!plan.OwnershipPartition&&!plan.ProgramSelection)
            throw new ArgumentException("METADATA_REGIONAL_DESIGN_SEED_PLAN_INVALID");
        if(plan.ProgramSelection&&(source.SelectionSeed is not null||!source.Slots.Any(s=>s.Options.GroupBy(o=>o.Priority).Any(g=>g.Count()>1))))
            throw new ArgumentException("METADATA_REGIONAL_DESIGN_SEED_PROGRAM_TARGET_INVALID");
        if(plan.TerrainDetail&&(source.Template.Surface?.Terrain?.Detail is not {} detail||!double.IsFinite(detail.Amplitude)||detail.Amplitude<=0||source.Template.Surface.HeightField is not null))
            throw new ArgumentException("METADATA_REGIONAL_DESIGN_SEED_TERRAIN_TARGET_INVALID");
        if(plan.OwnershipPartition&&(source.Template.Pattern?.PartitionSearch is not {} partition||partition.AxisOffsetsDegrees is not {} offsets||
            offsets.Distinct().Count()<2&&partition.AlternateBends is not {Count:>0}&&partition.AlternateBendOffsets is not {Count:>0}))
            throw new ArgumentException("METADATA_REGIONAL_DESIGN_SEED_PARTITION_TARGET_INVALID");
    }

    private static SeededWorld DeriveWorldSeeds(MetadataRegionalDesignRequest source,CancellationToken token)
    {
        if(source.SeedPlan is not {} plan)return new(source.Template,source.SelectionSeed,null);
        byte[] Digest(string target)=>SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{plan.Seed,WorldId=source.Id,Target=target}));
        string Text(string target)=>Convert.ToHexString(Digest(target));
        var template=source.Template;var derivations=new List<MetadataRegionalDesignSeedDerivation>();long? captureWork=null;
        if(plan.TerrainDetail)
        {
            int seed=BinaryPrimitives.ReadInt32LittleEndian(Digest("terrain-detail"));
            var surface=template.Surface;var terrain=surface.Terrain!;
            if(surface.TerrainCapturePlan is not null)
            {
                var budget=new WorldSpatialWorkBudget(template.MaximumWork,token,failureCode:"METADATA_REGIONAL_DESIGN_SEED_CAPTURE_WORK_BUDGET");
                WorldSurfaceQuery.ChargeCaptureWork(surface,budget,token);
                _=new WorldSurfaceQuery(surface,token);
                captureWork=budget.Used;
            }
            template=template with{Surface=surface with{Terrain=terrain with{Detail=terrain.Detail! with{Seed=seed}},TerrainCapturePlan=null}};
            derivations.Add(new("terrain-detail",seed.ToString(CultureInfo.InvariantCulture)));
        }
        if(plan.OwnershipPartition)
        {
            string seed=Text("ownership-partition");
            template=template with{Pattern=template.Pattern with{PartitionSearch=template.Pattern.PartitionSearch! with{Seed=seed}}};
            derivations.Add(new("ownership-partition",seed));
        }
        string? selection=source.SelectionSeed;
        if(plan.ProgramSelection)
        {
            selection=Text("program-selection");derivations.Add(new("program-selection",selection));
        }
        return new(template,selection,derivations.AsReadOnly(),captureWork);
    }
}
