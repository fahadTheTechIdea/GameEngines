using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Xunit;

namespace EconomyEngine.Tests;

/// <summary>
/// What a save costs when thousands of production batches are in flight, in the world of <see cref="ProductionScaleTests"/> (200 markets of 100 goods, 1,500 lines). The same save is cut into its parts so the cost of the
/// batches can be told from the cost of everything else, and the batches are also written the way format 3 wrote them (one object a batch) beside the grouped form, so the saving is measured in one process on one load.
/// A run writes <c>save-cost-*.csv</c> beside the test assembly; the assertions are the existing budgets of <see cref="Budgets"/> for a save, not the measurements.
/// </summary>
public sealed class SaveCostTests
{
    private static string Ms(double value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static double Median(List<double> values)
    {
        values.Sort();
        return values[values.Count / 2];
    }

    private static List<double> Time(int repeats, Action work)
    {
        var times = new List<double>();
        for (var i = 0; i < repeats; i++)
        {
            var watch = Stopwatch.StartNew();
            work();
            times.Add(watch.Elapsed.TotalMilliseconds);
        }
        return times;
    }

    [Fact]
    public void ASaveWithTwoThousandBatchesInFlightIsCutIntoItsPartsAndTheBatchesAreMeasuredInBothForms()
    {
        var set = EconomyLoader.Load(ProductionScaleTests.WithProduction("direct", lines: true));
        var runtime = EconomyRuntime.Create(set);
        var facts = Fixture.Host("autumn", 2);
        for (var i = 0; i < 22; i++) runtime.Advance(runtime.Step + set.PeriodSteps, facts);
        Assert.InRange(runtime.Batches.Count, 1500, 2600);
        const int repeats = 7;

        var image = EconomyImage.Of(runtime);
        var options = new JsonSerializerOptions { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
        byte[] bytes = [];
        var write = Time(repeats, () => bytes = EconomySave.Write(runtime));
        var open = Time(repeats, () => EconomySave.Open(set, bytes));
        var build = Time(repeats, () => EconomyImage.Of(runtime));
        var canonical = Time(repeats, () => EconomyImage.Canonical(image));
        var serialize = Time(repeats, () => JsonSerializer.SerializeToUtf8Bytes(image, options));
        var deserialize = Time(repeats, () => JsonSerializer.Deserialize<EconomyImage>(bytes, options));
        var ranges = Time(repeats, () => EconomyStateRules.CheckRanges(set, image));
        var digest = Time(repeats, () => EngineCommon.Digest.Of(EconomyImage.Canonical(image)));
        var restore = Time(repeats, () => EconomyStateRules.CheckAgainstDocument(EconomyRuntime.Restore(set, image)));

        // The parts, as the bytes each takes in the save.
        int Size<T>(T part) => JsonSerializer.SerializeToUtf8Bytes(part, options).Length;
        var grouped = Size(image.Batches);
        var perBatch = Size(runtime.Batches.Select(b => new { b.Market, b.Recipe, b.Runs, b.Completes }).ToList());     // format 3: one object a batch
        var pairs = image.Markets.Sum(m => Size(m.Pairs));
        var pairsAsObjects = image.Markets.Sum(m => Size(m.Pairs.Select(p => new { p.Price, p.Stock, p.LastChange, p.Supply, p.Demand, p.Met, p.Band, p.Short, p.SpotDemand, p.SpotSupply }).ToList()));   // format 3: one object a pair
        var history = image.Markets.Sum(m => Size(m.History));
        var lines = image.Markets.Sum(m => Size(m.Lines));
        var groupedTime = Time(repeats, () => JsonSerializer.SerializeToUtf8Bytes(image.Batches, options));
        var perBatchRows = runtime.Batches.Select(b => new { b.Market, b.Recipe, b.Runs, b.Completes }).ToList();
        var perBatchTime = Time(repeats, () => JsonSerializer.SerializeToUtf8Bytes(perBatchRows, options));
        var canonicalGrouped = image.Batches.Sum(b => Encoding.UTF8.GetByteCount("\nbatches " + b.Market + "|" + b.Recipe + "|" + string.Join(',', b.Completes.Zip(b.Runs, (c, r) => c + ":" + r))));
        var canonicalPerBatch = runtime.Batches.Sum(b => Encoding.UTF8.GetByteCount("\nbatch " + b.Market + "|" + b.Recipe + "|" + b.Runs + "|" + b.Completes));

        var csv = new StringBuilder("measurement,value,unit,note\n");
        csv.Append("batches in flight,").Append(runtime.Batches.Count).Append(",count,\n");
        csv.Append("lines with a batch in flight,").Append(image.Batches.Count).Append(",count,\n");
        csv.Append("save size,").Append(bytes.Length / 1024).Append(",KB,whole save\n");
        csv.Append("  batches grouped (format 4),").Append(grouped).Append(",bytes,\n");
        csv.Append("  batches one to an entry (format 3),").Append(perBatch).Append(",bytes,the same batches written the old way\n");
        csv.Append("  pairs as arrays (format 4),").Append(pairs).Append(",bytes,\n");
        csv.Append("  pairs as objects (format 3),").Append(pairsAsObjects).Append(",bytes,the same pairs written the old way\n");
        csv.Append("  price history,").Append(history).Append(",bytes,\n");
        csv.Append("  lines,").Append(lines).Append(",bytes,\n");
        csv.Append("batches share of the save grouped,").Append(Ms(100.0 * grouped / bytes.Length)).Append(",percent,\n");
        csv.Append("batches share of the save one to an entry,").Append(Ms(100.0 * perBatch / (bytes.Length - grouped + perBatch))).Append(",percent,\n");
        csv.Append("digest text of the batches grouped,").Append(canonicalGrouped).Append(",bytes,\n");
        csv.Append("digest text of the batches one to an entry,").Append(canonicalPerBatch).Append(",bytes,\n");
        csv.Append("serialize the batches grouped,").Append(Ms(Median(groupedTime))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("serialize the batches one to an entry,").Append(Ms(Median(perBatchTime))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("build the image and digest,").Append(Ms(Median(build))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("  canonical text alone,").Append(Ms(Median(canonical))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("  serialize the whole image alone,").Append(Ms(Median(serialize))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("open: deserialize alone,").Append(Ms(Median(deserialize))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("open: ranges alone,").Append(Ms(Median(ranges))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("open: canonical text and digest alone,").Append(Ms(Median(digest))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("open: restore and check against the document alone,").Append(Ms(Median(restore))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("save write,").Append(Ms(Median(write))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("save write worst,").Append(Ms(write[^1])).Append(",ms,max of ").Append(repeats).Append('\n');
        csv.Append("save open,").Append(Ms(Median(open))).Append(",ms,median of ").Append(repeats).Append('\n');
        csv.Append("save open worst,").Append(Ms(open[^1])).Append(",ms,max of ").Append(repeats).Append('\n');
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "save-cost.csv"), csv.ToString());

        Assert.True(grouped < perBatch, "grouped " + grouped + " bytes, one to an entry " + perBatch);
        Assert.Equal(runtime.StateDigest(), EconomySave.Open(set, bytes).StateDigest());
    }
}
