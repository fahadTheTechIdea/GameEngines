using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EconomyEngine;

/// <summary>
/// One good in one market as the save holds it. In the file it is an array of ten whole numbers in the order of the properties (the short flag as 0 or 1), not an object: a world of tens of thousands of pairs spent
/// more than half of its bytes on the property names. See <see cref="ImagePairConverter"/>.
/// </summary>
[JsonConverter(typeof(ImagePairConverter))]
internal sealed class ImagePair
{
    [JsonRequired] public long Price { get; set; }
    [JsonRequired] public long Stock { get; set; }
    [JsonRequired] public long LastChange { get; set; }
    [JsonRequired] public long Supply { get; set; }
    [JsonRequired] public long Demand { get; set; }
    [JsonRequired] public long Met { get; set; }
    [JsonRequired] public int Band { get; set; }
    [JsonRequired] public bool Short { get; set; }
    [JsonRequired] public long SpotDemand { get; set; }
    [JsonRequired] public long SpotSupply { get; set; }
}

/// <summary>Reads and writes a pair as <c>[price, stock, lastChange, supply, demand, met, band, short, spotDemand, spotSupply]</c>. A pair that is not an array of exactly ten whole numbers, or whose band or flag does not fit, is not a save.</summary>
internal sealed class ImagePairConverter : JsonConverter<ImagePair>
{
    private const int Fields = 10;

    /// <summary>A null where a pair belongs is handed to <see cref="Read"/>, which refuses it, instead of leaving a null in the list for the rules to trip over.</summary>
    public override bool HandleNull => true;

    public override ImagePair Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("a pair is an array of ten numbers");
        Span<long> values = stackalloc long[Fields];
        for (var i = 0; i < Fields; i++)
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt64(out values[i])) throw new JsonException("a pair is an array of ten whole numbers");
        }
        if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray) throw new JsonException("a pair is an array of exactly ten numbers");
        if (values[6] < int.MinValue || values[6] > int.MaxValue) throw new JsonException("a pair's band is not a whole number of this size");
        if (values[7] is not (0 or 1)) throw new JsonException("a pair's short flag is 0 or 1");
        return new ImagePair
        {
            Price = values[0], Stock = values[1], LastChange = values[2], Supply = values[3], Demand = values[4], Met = values[5], Band = (int)values[6], Short = values[7] == 1, SpotDemand = values[8], SpotSupply = values[9],
        };
    }

    public override void Write(Utf8JsonWriter writer, ImagePair value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Price);
        writer.WriteNumberValue(value.Stock);
        writer.WriteNumberValue(value.LastChange);
        writer.WriteNumberValue(value.Supply);
        writer.WriteNumberValue(value.Demand);
        writer.WriteNumberValue(value.Met);
        writer.WriteNumberValue(value.Band);
        writer.WriteNumberValue(value.Short ? 1 : 0);
        writer.WriteNumberValue(value.SpotDemand);
        writer.WriteNumberValue(value.SpotSupply);
        writer.WriteEndArray();
    }
}

internal sealed class ImageLine
{
    [JsonRequired] public long LastRuns { get; set; }
    [JsonRequired] public int Limit { get; set; }
    [JsonRequired] public int LimitGood { get; set; }

    /// <summary>For each good the line's recipe requires, in the recipe's order: the runs done towards the next loss of it (0 for one that does not wear out).</summary>
    [JsonRequired] public List<long> Wear { get; set; } = new();
}

/// <summary>
/// The batches of one line, grouped: <see cref="Completes"/> are the periods the outputs are due, strictly ascending (a line starts one batch a period and a recipe takes a fixed time, so a line has at most one batch
/// due in a period), and <see cref="Runs"/> the runs of each, in the same order. One group names the market and the recipe once instead of once a batch.
/// </summary>
internal sealed class ImageBatchGroup
{
    [JsonRequired] public int Market { get; set; }
    [JsonRequired] public int Recipe { get; set; }
    [JsonRequired] public List<long> Completes { get; set; } = new();
    [JsonRequired] public List<long> Runs { get; set; } = new();
}

internal sealed class ImageMarket
{
    [JsonRequired] public long Treasury { get; set; }
    [JsonRequired] public List<long> Accrued { get; set; } = new();
    [JsonRequired] public List<long> Arrears { get; set; } = new();
    [JsonRequired] public List<ImagePair> Pairs { get; set; } = new();
    [JsonRequired] public List<long> History { get; set; } = new();
    [JsonRequired] public List<ImageLine> Lines { get; set; } = new();
}

internal sealed class ImageCurrency
{
    [JsonRequired] public long Level { get; set; }
    [JsonRequired] public long Rate { get; set; }
    [JsonRequired] public long Money { get; set; }
    [JsonRequired] public long Growth { get; set; }
    [JsonRequired] public long World { get; set; }
    [JsonRequired] public long Minted { get; set; }
}

internal sealed class ImageShipment
{
    [JsonRequired] public int Route { get; set; }
    [JsonRequired] public int Good { get; set; }
    [JsonRequired] public long Quantity { get; set; }
    [JsonRequired] public long Arrives { get; set; }
}

internal sealed class ImageLoan
{
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public int Lender { get; set; }
    [JsonRequired] public int Borrower { get; set; }
    [JsonRequired] public int Schedule { get; set; }
    [JsonRequired] public int Kind { get; set; }
    [JsonRequired] public long RatePpm { get; set; }
    [JsonRequired] public long Payment { get; set; }
    [JsonRequired] public int DefaultAfter { get; set; }
    [JsonRequired] public long Balance { get; set; }
    [JsonRequired] public int Remaining { get; set; }
    [JsonRequired] public int Missed { get; set; }
    [JsonRequired] public int Status { get; set; }
}

internal sealed class ImageShock
{
    [JsonRequired] public int Shock { get; set; }
    [JsonRequired] public int Market { get; set; }
    [JsonRequired] public long Start { get; set; }
    [JsonRequired] public int Scale { get; set; }
}

/// <summary>
/// The state of a runtime as plain data, exactly what a save holds. Every field is required: a save with a field missing is refused, never completed with a default. The digest is taken over this data and nothing
/// else, so it can be checked before the data is understood, and the data is checked against the document afterwards.
/// </summary>
internal sealed class EconomyImage
{
    [JsonRequired] public int Format { get; set; }
    [JsonRequired] public string Document { get; set; } = "";
    [JsonRequired] public string Id { get; set; } = "";
    [JsonRequired] public long Step { get; set; }
    [JsonRequired] public List<ImageCurrency> Currencies { get; set; } = new();
    [JsonRequired] public List<ImageMarket> Markets { get; set; } = new();
    [JsonRequired] public List<ImageShipment> Shipments { get; set; } = new();
    [JsonRequired] public List<ImageLoan> Loans { get; set; } = new();
    [JsonRequired] public List<ImageShock> Shocks { get; set; } = new();
    [JsonRequired] public List<bool> Unlocked { get; set; } = new();
    [JsonRequired] public List<ImageBatchGroup> Batches { get; set; } = new();
    [JsonRequired] public string Digest { get; set; } = "";

    public static EconomyImage Of(EconomyRuntime runtime)
    {
        var image = new EconomyImage { Format = EconomySave.Format, Document = runtime.Set.Digest, Id = runtime.Set.Id, Step = runtime.Step };
        foreach (var currency in runtime.Currencies)
            image.Currencies.Add(new ImageCurrency { Level = currency.Level, Rate = currency.Rate, Money = currency.Money, Growth = currency.Growth, World = currency.World, Minted = currency.Minted });
        foreach (var market in runtime.Markets)
        {
            var saved = new ImageMarket { Treasury = market.Treasury, Accrued = market.Accrued.ToList(), Arrears = market.Arrears.ToList(), History = market.History.ToList() };
            foreach (var pair in market.Pairs)
                saved.Pairs.Add(new ImagePair
                {
                    Price = pair.Price, Stock = pair.Stock, LastChange = pair.LastChange, Supply = pair.Supply, Demand = pair.Demand, Met = pair.Met, Band = pair.Band, Short = pair.Short,
                    SpotDemand = pair.SpotDemand, SpotSupply = pair.SpotSupply,
                });
            foreach (var line in market.Lines) saved.Lines.Add(new ImageLine { LastRuns = line.LastRuns, Limit = (int)line.Limit, LimitGood = line.LimitGood, Wear = line.Wear.ToList() });
            image.Markets.Add(saved);
        }
        foreach (var shipment in runtime.Shipments) image.Shipments.Add(new ImageShipment { Route = shipment.Route, Good = shipment.Good, Quantity = shipment.Quantity, Arrives = shipment.Arrives });
        foreach (var loan in runtime.Loans)
            image.Loans.Add(new ImageLoan
            {
                Id = loan.Id, Lender = loan.Lender, Borrower = loan.Borrower, Schedule = loan.Schedule, Kind = (int)loan.Kind, RatePpm = loan.RatePpm, Payment = loan.Payment, DefaultAfter = loan.DefaultAfter,
                Balance = loan.Balance, Remaining = loan.Remaining, Missed = loan.Missed, Status = (int)loan.Status,
            });
        foreach (var shock in runtime.ActiveShocks) image.Shocks.Add(new ImageShock { Shock = shock.Shock, Market = shock.Market, Start = shock.Start, Scale = shock.Scale });
        for (var r = 0; r < runtime.Set.Recipes.Count; r++) image.Unlocked.Add(runtime.IsUnlocked(r));
        // Grouped by line in the order of market then recipe, each group by completion period: the canonical order of the batches. Delivery adds the same units to the same stock whatever order the batches are
        // visited in, so the order the runtime happens to hold them in is not state, and the digest does not depend on it.
        foreach (var group in runtime.Batches.GroupBy(b => (b.Market, b.Recipe)).OrderBy(g => g.Key.Market).ThenBy(g => g.Key.Recipe))
        {
            var ordered = group.OrderBy(b => b.Completes).ToList();
            image.Batches.Add(new ImageBatchGroup { Market = group.Key.Market, Recipe = group.Key.Recipe, Completes = ordered.Select(b => b.Completes).ToList(), Runs = ordered.Select(b => b.Runs).ToList() });
        }
        image.Digest = EngineCommon.Digest.Of(Canonical(image));
        return image;
    }

    private static string Text(string value) => JsonSerializer.Serialize(value);

    private static string Join(IEnumerable<long> values) => string.Join(',', values.Select(v => v.ToString(CultureInfo.InvariantCulture)));

    /// <summary>The text the digest is taken over: every part in a fixed order, numbers in the invariant culture, texts escaped. Shipments, loans and shocks are in the order the state holds them, which is part of the state; production batches are grouped by line (market, then recipe) and ordered by completion period.</summary>
    public static string Canonical(EconomyImage image)
    {
        var text = new StringBuilder("economy-state-6");
        text.Append(CultureInfo.InvariantCulture, $"\ndoc={image.Document}\nid={image.Id}\nstep={image.Step}");
        foreach (var c in image.Currencies) text.Append(CultureInfo.InvariantCulture, $"\ncurrency {c.Level}|{c.Rate}|{c.Money}|{c.Growth}|{c.World}|{c.Minted}");
        foreach (var market in image.Markets)
        {
            text.Append(CultureInfo.InvariantCulture, $"\nmarket {market.Treasury}|accrued {Join(market.Accrued)}|arrears {Join(market.Arrears)}|history {Join(market.History)}");
            foreach (var p in market.Pairs)
                text.Append(CultureInfo.InvariantCulture, $"\n pair {p.Price}|{p.Stock}|{p.LastChange}|{p.Supply}|{p.Demand}|{p.Met}|{p.Band}|{p.Short}|{p.SpotDemand}|{p.SpotSupply}");
            foreach (var l in market.Lines) text.Append(CultureInfo.InvariantCulture, $"\n line {l.LastRuns}|{l.Limit}|{l.LimitGood}|wear {Join(l.Wear)}");
        }
        foreach (var s in image.Shipments) text.Append(CultureInfo.InvariantCulture, $"\nshipment {s.Route}|{s.Good}|{s.Quantity}|{s.Arrives}");
        foreach (var l in image.Loans)
            text.Append(CultureInfo.InvariantCulture, $"\nloan {Text(l.Id)}|{l.Lender}|{l.Borrower}|{l.Schedule}|{l.Kind}|{l.RatePpm}|{l.Payment}|{l.DefaultAfter}|{l.Balance}|{l.Remaining}|{l.Missed}|{l.Status}");
        foreach (var s in image.Shocks) text.Append(CultureInfo.InvariantCulture, $"\nshock {s.Shock}|{s.Market}|{s.Start}|{s.Scale}");
        text.Append(CultureInfo.InvariantCulture, $"\nunlocked {string.Join(',', image.Unlocked.Select(u => u ? "1" : "0"))}");
        foreach (var b in image.Batches) text.Append(CultureInfo.InvariantCulture, $"\nbatches {b.Market}|{b.Recipe}|{string.Join(',', b.Completes.Zip(b.Runs, (c, r) => c.ToString(CultureInfo.InvariantCulture) + ":" + r.ToString(CultureInfo.InvariantCulture)))}");
        return text.ToString();
    }
}
