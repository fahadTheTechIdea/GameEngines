using System.Text.Json;
using System.Text.Json.Serialization;

namespace EconomyEngine.Tests;

/// <summary>Builds saves that are wrong in one place: with the digest left stale, or recomputed so only the state rules can catch the forgery.</summary>
internal static class Tamper
{
    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };

    /// <summary>The sample town after a trade, a grant, a loan, a shock, a change of growth and twelve periods: shipments on the way, loans, a shock on, taxes accrued.</summary>
    public static EconomyRuntime Played()
    {
        var runtime = Fixture.Runtime();
        runtime.Trade("riverton", "grain", 100);
        runtime.Fund("hillfort", 2000);
        runtime.Lend("aid", "world", "hillfort", 5000, 3000, 6, LoanKind.Bullet, "taxday", 2);
        runtime.ApplyShock("drought", "riverton", 1000);
        runtime.SetGrowth("florin", 1000);
        Fixture.Periods(runtime, 12);
        runtime.ApplyShock("blockade", "", 1000);
        return runtime;
    }

    public static EconomyImage Image(byte[] save) => JsonSerializer.Deserialize<EconomyImage>(save, Options)!;

    /// <summary>The save with the edit applied to its state and the digest recomputed, so only the rules about the state can refuse it.</summary>
    public static byte[] Forge(byte[] save, Action<EconomyImage> edit)
    {
        var image = Image(save);
        edit(image);
        image.Digest = EngineCommon.Digest.Of(EconomyImage.Canonical(image));
        return JsonSerializer.SerializeToUtf8Bytes(image, Options);
    }

    /// <summary>The save with the edit applied and the old digest left, so only the digest can refuse it.</summary>
    public static byte[] Stale(byte[] save, Action<EconomyImage> edit)
    {
        var image = Image(save);
        edit(image);
        return JsonSerializer.SerializeToUtf8Bytes(image, Options);
    }
}
