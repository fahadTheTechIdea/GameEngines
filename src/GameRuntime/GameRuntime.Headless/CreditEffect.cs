using System.Globalization;
using System.Text.Json;
using Beep.SimEngine;
using Beep.SimEngine.Kernel;
using Beep.SimEngine.Standard;

namespace GameRuntime.Headless;

/// <summary>Adds whole currency units to company 0's account. The payload is a JSON integer, or a string holding one.</summary>
public sealed class CreditEffect : IDirectorEffect
{
    public const string EffectName = "credit";
    public string Name => EffectName;

    public Code Check(World world, string payloadJson) =>
        world.Services.TryGet<IEconomy>(out _) && TryAmount(payloadJson, out var amount) && amount > 0 ? Code.None : Codes.NotAllowed;

    public void Apply(World world, string payloadJson)
    {
        if (!TryAmount(payloadJson, out var amount)) throw new InvalidOperationException("credit payload was checked but is not an amount: " + payloadJson);
        world.Services.Get<IEconomy>().Credit(new PlayerId(0), Fix.FromInt(amount), new Code("gf.director.credit"));
    }

    private static bool TryAmount(string payloadJson, out int amount)
    {
        amount = 0;
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Number) return root.TryGetInt32(out amount);
        if (root.ValueKind == JsonValueKind.String) return int.TryParse(root.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out amount);
        return false;
    }
}
