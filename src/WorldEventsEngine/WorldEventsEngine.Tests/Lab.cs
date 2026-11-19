using System.Text.Json.Nodes;

namespace WorldEventsEngine.Tests;

/// <summary>
/// Small worlds built for measuring one rule at a time, from the tests' tiny document: no diseases, no cards, a calm director. The hazard lab has one region and one hazard that lives exactly one step, so the process
/// the tests look at is the bare one (an onset, then at once the next step's chance). The weather lab has regions whose weather is the only thing that moves.
/// </summary>
internal static class Lab
{
    /// <summary>One region 'a', one hazard 'h' of a single rung that lasts one step, with the given rate and excitation; no cooldown, no reach.</summary>
    public static JsonObject Hawkes(long seed, int ratePpm, int jumpPpm, int decayPermille, int capPpm)
    {
        var root = Tiny.Root();
        root["seed"] = seed;
        root["diseases"] = new JsonArray();
        root["interventions"] = new JsonArray();
        root["cards"] = new JsonArray();
        var a = Tiny.Copy(Tiny.Region(root, "a")).AsObject();
        a["immune"] = new JsonArray();
        root["regions"] = new JsonArray(a);
        root["links"] = new JsonArray();
        var hazard = Tiny.Hazard(root);
        hazard["ratePpm"] = ratePpm;
        hazard["excite"] = new JsonObject { ["jumpPpm"] = jumpPpm, ["decayPermille"] = decayPermille, ["capPpm"] = capPpm };
        hazard["cooldown"] = 0;
        hazard["reach"] = new JsonObject { ["hops"] = 0, ["falloff"] = 0 };
        var low = Tiny.Copy(Tiny.Rung(root, 0)).AsObject();
        low["minDuration"] = 1;
        low["maxDuration"] = 1;
        low["onsetSteps"] = 0;
        low["recoverySteps"] = 0;
        low["stress"] = 0;
        low["effects"] = new JsonArray();
        hazard["ladder"] = new JsonArray(low);
        root["start"]!["storyteller"] = "calm";
        return root;
    }

    public static WorldRuntime HawkesWorld(long seed, int ratePpm, int jumpPpm, int decayPermille, int capPpm) =>
        WorldRuntime.Create(WorldLoader.Load(Hawkes(seed, ratePpm, jumpPpm, decayPermille, capPpm).ToJsonString()));

    /// <summary>
    /// Regions with only weather happening: every region in zone 'z' (weights <paramref name="frequency"/>, persistence <paramref name="persistence"/> per mille, spill <paramref name="spill"/>) unless its id begins with 'n' and
    /// <paramref name="frozen"/> is set, which puts it in a zone that never changes its weather. <paramref name="links"/> are pairs of region ids.
    /// </summary>
    public static JsonObject Weather(long seed, int[] frequency, int persistence, int spill, string[] regions, (string A, string B)[] links, bool frozen = false)
    {
        var root = Tiny.Root();
        root["seed"] = seed;
        root["diseases"] = new JsonArray();
        root["interventions"] = new JsonArray();
        root["cards"] = new JsonArray();
        var zone = Tiny.Zone(root);
        zone["spill"] = spill;
        foreach (var season in zone["seasons"]!.AsArray())
        {
            var climate = season!.AsObject();
            climate["persistence"] = persistence;
            climate["frequency"] = new JsonArray(frequency.Select(f => (JsonNode)JsonValue.Create(f)!).ToArray());
        }
        var still = Tiny.Copy(zone).AsObject();
        still["id"] = "still";
        still["key"] = "z.still";
        still["spill"] = 0;
        foreach (var season in still["seasons"]!.AsArray()) season!.AsObject()["persistence"] = 1000;
        root["zones"]!.AsArray().Add(still);
        var template = Tiny.Copy(Tiny.Region(root, "a")).AsObject();
        template["immune"] = new JsonArray();
        template["market"] = "";
        var list = new JsonArray();
        foreach (var id in regions)
        {
            var region = Tiny.Copy(template).AsObject();
            region["id"] = id;
            region["key"] = "g." + id;
            region["zone"] = frozen && id.StartsWith('n') ? "still" : "z";
            list.Add(region);
        }
        root["regions"] = list;
        root["links"] = new JsonArray(links.Select(l => (JsonNode)new JsonObject { ["a"] = l.A, ["b"] = l.B, ["flowPpm"] = 100_000 }).ToArray());
        root["start"]!["storyteller"] = "calm";
        return root;
    }
}
