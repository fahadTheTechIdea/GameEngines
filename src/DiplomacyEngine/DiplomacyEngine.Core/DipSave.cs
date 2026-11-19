using System.Globalization;
using System.Text;
using System.Text.Json;
using EngineCommon;

namespace DiplomacyEngine;

/// <summary>
/// The save of a diplomacy runtime: its format, the digest of the document it was taken from, the whole state and a digest of the state. Loading checks them in this order: the format, the document, the ranges of the
/// state (before the digest is read, so a forged value is refused as a state even when its digest was recomputed), the digest, and last what the state says about the document. It never repairs or migrates; an old format is refused.
/// </summary>
public static class DipSave
{
    public const int Format = 1;
    public const int MaxBytes = 64 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow, WriteIndented = false };

    public static byte[] Write(DipRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        var state = runtime.State;
        state.Digest = Digest.Of(Canonical(state));
        return JsonSerializer.SerializeToUtf8Bytes(state, Options);
    }

    public static DipRuntime Open(DipSet set, ReadOnlyMemory<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (bytes.Length > MaxBytes) throw new EngineRefusedException(DipCodes.SaveFormat, "a diplomacy save is at most " + MaxBytes + " bytes");
        DipState? state;
        try { state = JsonSerializer.Deserialize<DipState>(bytes.Span, Options); }
        catch (JsonException exception) { throw new EngineRefusedException(DipCodes.SaveFormat, "not a diplomacy save: " + exception.Message); }
        if (state is null || state.Format != Format) throw new EngineRefusedException(DipCodes.SaveFormat, "unsupported diplomacy save format");
        if (state.Document != set.Digest || state.Id != set.Id) throw new EngineRefusedException(DipCodes.SaveDocument, "the save was taken from another document");
        DipStateRules.CheckRanges(set, state);
        if (state.Digest != Digest.Of(Canonical(state))) throw new EngineRefusedException(DipCodes.SaveDigest, "the save digest does not match its state");
        DipStateRules.CheckAgainstDocument(set, state);
        return DipRuntime.Restore(set, state);
    }

    /// <summary>The digest of the state of a runtime: equal for equal states, and what a save carries.</summary>
    public static string StateDigest(DipRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return Digest.Of(Canonical(runtime.State));
    }

    private static string Join<T>(IEnumerable<T> values) where T : IFormattable => string.Join(',', values.Select(v => v.ToString(null, CultureInfo.InvariantCulture)));

    /// <summary>The text the digest is taken over: every part in a fixed order, numbers in the invariant culture, texts escaped.</summary>
    internal static string Canonical(DipState s)
    {
        var text = new StringBuilder("diplomacy-state-1");
        text.Append(CultureInfo.InvariantCulture, $"\ndoc={s.Document}\nid={JsonSerializer.Serialize(s.Id)}\nstep={s.Step}\nseason={s.Season}\nnext {s.NextTreaty}|{s.NextProposal}|{s.NextWar}|{s.NextMission}|{s.NextCall}");
        foreach (var r in s.Realms) text.Append(CultureInfo.InvariantCulture, $"\nrealm {r.Might}|{r.Stability}|{r.Legitimacy}|{r.Band}|{r.Trust}|{r.Fear}|{r.Honour}|{r.Infamy}|{r.CounterIntel}|{r.Generation}|{Join(r.Satisfaction)}");
        text.Append("\nleader ").Append(Join(s.Leader)).Append("\nintel ").Append(Join(s.Intel));
        foreach (var d in s.Deeds) text.Append(CultureInfo.InvariantCulture, $"\ndeed {d.Holder}|{d.Subject}|{d.Deed}|{d.Start}|{d.Scale}");
        foreach (var t in s.Treaties) text.Append(CultureInfo.InvariantCulture, $"\ntreaty {t.Id}|{t.Type}|{t.First}|{t.Second}|{t.Start}|{t.End}|{t.Amount}|{t.NextDue}|{(t.Awaiting ? 1 : 0)}|{t.Deadline}|{t.Missed}|{t.War}");
        foreach (var p in s.Proposals) text.Append(CultureInfo.InvariantCulture, $"\nproposal {p.Id}|{p.Type}|{p.From}|{p.To}|{p.Term}|{p.Amount}|{p.Made}|{p.DecideAt}|{p.ExpireAt}");
        foreach (var w in s.Wars) text.Append(CultureInfo.InvariantCulture, $"\nwar {w.Id}|{w.Attacker}|{w.Defender}|{w.AttackerSide}|{w.DefenderSide}|{w.Cb}|{w.Start}|{w.Score}|{w.AttackerExhaustion}|{w.DefenderExhaustion}|{(w.Ceasefire ? 1 : 0)}");
        foreach (var c in s.Calls) text.Append(CultureInfo.InvariantCulture, $"\ncall {c.Id}|{c.War}|{c.Caller}|{c.Callee}|{c.Treaty}|{c.Due}");
        foreach (var g in s.Grants) text.Append(CultureInfo.InvariantCulture, $"\ngrant {g.Holder}|{g.Target}|{g.Cb}|{g.Until}");
        foreach (var c in s.Coalitions) text.Append(CultureInfo.InvariantCulture, $"\ncoalition {c.Target}|{c.Members}|{c.Since}");
        foreach (var a in s.Agents) text.Append(CultureInfo.InvariantCulture, $"\nagent {JsonSerializer.Serialize(a.Id)}|{a.Owner}|{a.Skill}|{a.State}|{a.Mission}|{a.FreeAt}");
        foreach (var m in s.Missions) text.Append(CultureInfo.InvariantCulture, $"\nmission {m.Id}|{m.Agent}|{m.Target}|{m.Type}|{m.Start}|{m.Due}");
        foreach (var i in s.Incidents) text.Append(CultureInfo.InvariantCulture, $"\nincident {i.Step}|{i.Kind}|{i.A}|{i.B}|{i.Index}|{i.Amount}");
        return text.ToString();
    }
}
