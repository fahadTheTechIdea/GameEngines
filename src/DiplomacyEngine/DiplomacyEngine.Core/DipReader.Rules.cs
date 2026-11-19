using System.Text.Json;

namespace DiplomacyEngine;

internal sealed partial class DipReader
{
    private int[] IntList(JsonElement owner, string field, string where, int count, int minimum, int maximum)
    {
        var array = _json.Array(owner, field, where, count, count);
        var values = new int[count];
        var i = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetInt32(out var number)) throw _json.Refuse(DipCodes.Format, where + "." + field + "[" + i + "] must be a whole number");
            if (number < minimum || number > maximum) throw _json.Refuse(DipCodes.Range, where + "." + field + "[" + i + "] must be " + minimum + " to " + maximum + ", not " + number);
            values[i++] = number;
        }
        return values;
    }

    private void ReadRules(JsonElement element)
    {
        _json.Object(element, "rules", ["regard", "standing", "war", "acceptance", "balance", "politics", "espionage"]);
        ReadRegardRules(element.GetProperty("regard"));
        ReadStandingRules(element.GetProperty("standing"));
        ReadWarRules(element.GetProperty("war"));
        ReadAcceptanceRules(element.GetProperty("acceptance"));
        ReadBalanceRules(element.GetProperty("balance"));
        ReadPoliticsRules(element.GetProperty("politics"));
        ReadEspionageRules(element.GetProperty("espionage"));
    }

    private void ReadRegardRules(JsonElement element)
    {
        const string where = "rules.regard";
        _json.Object(element, where, ["ideology", "leader", "trust", "honour", "coalition"]);
        _parts.Regard = new RegardRules(_json.Int(element, "ideology", where, 0, 2000), _json.Int(element, "leader", where, 0, 2000), _json.Int(element, "trust", where, 0, 2000), _json.Int(element, "honour", where, 0, 2000),
            _json.Int(element, "coalition", where, 0, 2000));
    }

    private void ReadStandingRules(JsonElement element)
    {
        const string where = "rules.standing";
        _json.Object(element, where, ["trustDecayPpm", "fearDecayPpm", "honourDecayPpm", "infamyDecayPpm", "witnessDeed", "expireTrust", "paidTrust", "honouredTrust", "honouredHonour"]);
        _parts.Standing = new StandingRules(_json.Int(element, "trustDecayPpm", where, 0, 1_000_000), _json.Int(element, "fearDecayPpm", where, 0, 1_000_000), _json.Int(element, "honourDecayPpm", where, 0, 1_000_000),
            _json.Int(element, "infamyDecayPpm", where, 0, 1_000_000), OptionalRef(element, "witnessDeed", where, _deedIndex, "deed"), _json.Int(element, "expireTrust", where, 0, 1000), _json.Int(element, "paidTrust", where, 0, 1000),
            _json.Int(element, "honouredTrust", where, 0, 1000), _json.Int(element, "honouredHonour", where, 0, 1000));
    }

    private void ReadWarRules(JsonElement element)
    {
        const string where = "rules.war";
        _json.Object(element, where, ["allowNoCb", "noCbInfamy", "noCbHonour", "declaredDeed", "warOnUsDeed", "exhaustPpm", "callBase", "fearPerScore", "sourceWeights"]);
        var weights = element.GetProperty("sourceWeights");
        _json.Object(weights, where + ".sourceWeights", ["battle", "occupation", "blockade", "event"]);
        const string at = where + ".sourceWeights";
        _parts.War = new WarRules(_json.Bool(element, "allowNoCb", where), _json.Int(element, "noCbInfamy", where, 0, 1000), _json.Int(element, "noCbHonour", where, 0, 1000), OptionalRef(element, "declaredDeed", where, _deedIndex, "deed"),
            _json.Int(element, "exhaustPpm", where, 0, 1_000_000), _json.Int(weights, "battle", at, 0, 2000), _json.Int(weights, "occupation", at, 0, 2000), _json.Int(weights, "blockade", at, 0, 2000), _json.Int(weights, "event", at, 0, 2000),
            _json.Int(element, "callBase", where, 0, 1000), _json.Int(element, "fearPerScore", where, 0, 1000), OptionalRef(element, "warOnUsDeed", where, _deedIndex, "deed"));
    }

    private void ReadAcceptanceRules(JsonElement element)
    {
        const string where = "rules.acceptance";
        _json.Object(element, where, ["acceptAt", "rejectAt", "termUnit"]);
        var accept = _json.Int(element, "acceptAt", where, -100_000, 100_000);
        var reject = _json.Int(element, "rejectAt", where, -100_000, 100_000);
        if (reject > accept) throw _json.Refuse(DipCodes.Rules, where + ".rejectAt is above acceptAt");
        _parts.Acceptance = new AcceptanceRules(accept, reject, _json.Int(element, "termUnit", where, 1, 1_000_000_000));
    }

    private void ReadBalanceRules(JsonElement element)
    {
        const string where = "rules.balance";
        _json.Object(element, where, ["interval", "threatLine", "relaxLine", "joinRegard", "minMembers", "bandwagonFear", "casusBelli", "cbDays"]);
        var threat = _json.Int(element, "threatLine", where, 1, 2000);
        var relax = _json.Int(element, "relaxLine", where, 0, 2000);
        if (relax > threat) throw _json.Refuse(DipCodes.Rules, where + ".relaxLine is above threatLine");
        _parts.Balance = new BalanceRules(_json.Int(element, "interval", where, 1, 100_000), threat, relax, _json.Int(element, "joinRegard", where, -1000, 1000), _json.Int(element, "minMembers", where, 1, 63),
            _json.Int(element, "bandwagonFear", where, -1000, 1000), OptionalRef(element, "casusBelli", where, _cbIndex, "casus belli"), _json.Int(element, "cbDays", where, 1, MaxDays));
        if (_parts.Balance.Cb >= 0 && _parts.Balance.CbDays < 1) throw _json.Refuse(DipCodes.Rules, where + " grants a casus belli for no days");
    }

    private void ReadPoliticsRules(JsonElement element)
    {
        const string where = "rules.politics";
        _json.Object(element, where, ["inertiaPpm", "legitimacyWeight", "exhaustionWeight", "unrestLine", "revoltLine", "hysteresis", "driftPpm", "legitimacyBase", "legitimacyDriftPpm", "succession", "signals"]);
        var succession = element.GetProperty("succession");
        const string sw = where + ".succession";
        _json.Object(succession, sw, ["legitimacy", "contestedBase", "contestedPerLack", "contestedStability", "casusBelli", "claimDays"]);
        var signals = element.GetProperty("signals");
        const string gw = where + ".signals";
        _json.Object(signals, gw, ["unrest", "revolt", "calm", "succession", "contested"]);
        var unrest = _json.Int(element, "unrestLine", where, 1, 1000);
        var revolt = _json.Int(element, "revoltLine", where, 0, 1000);
        if (revolt >= unrest) throw _json.Refuse(DipCodes.Politics, where + ".revoltLine must be below unrestLine");
        var hysteresis = _json.Int(element, "hysteresis", where, 0, 500);
        if (unrest + hysteresis > 1000) throw _json.Refuse(DipCodes.Politics, where + ".unrestLine plus hysteresis passes 1000");
        _parts.Politics = new PoliticsRules(_json.Int(element, "inertiaPpm", where, 0, 1_000_000), _json.Int(element, "legitimacyWeight", where, 0, 1000), _json.Int(element, "exhaustionWeight", where, 0, 1000), unrest, revolt, hysteresis,
            _json.Int(element, "driftPpm", where, 0, 1_000_000), _json.Int(element, "legitimacyBase", where, 0, 1000), _json.Int(element, "legitimacyDriftPpm", where, 0, 1_000_000),
            _json.Int(succession, "legitimacy", sw, 0, 1000), _json.Int(succession, "contestedBase", sw, 0, 1000), _json.Int(succession, "contestedPerLack", sw, 0, 2000), _json.Int(succession, "contestedStability", sw, 0, 1000),
            OptionalRef(succession, "casusBelli", sw, _cbIndex, "casus belli"), _json.Int(succession, "claimDays", sw, 1, MaxDays), Signal(signals, "unrest", gw), Signal(signals, "revolt", gw), Signal(signals, "calm", gw),
            Signal(signals, "succession", gw), Signal(signals, "contested", gw));
    }

    private string Signal(JsonElement owner, string field, string where)
    {
        var name = _json.Text(owner, field, where, 128);
        if (name.Length > 0) _json.CheckId(name, where + "." + field);
        return name;
    }

    private void ReadEspionageRules(JsonElement element)
    {
        const string where = "rules.espionage";
        _json.Object(element, where, ["counterWeight", "exposedOnSuccess", "captureDays", "exposedHonour", "intelDecayPpm", "counterDecayPpm", "levels", "errors", "epochDays"]);
        var levels = IntList(element, "levels", where, 4, 1, 1000);
        for (var i = 1; i < levels.Length; i++)
            if (levels[i] <= levels[i - 1]) throw _json.Refuse(DipCodes.Espionage, where + ".levels must rise");
        var errors = IntList(element, "errors", where, 5, 0, 1000);
        for (var i = 1; i < errors.Length; i++)
            if (errors[i] > errors[i - 1]) throw _json.Refuse(DipCodes.Espionage, where + ".errors must not rise with the level of knowledge");
        _parts.Espionage = new EspionageRules(_json.Int(element, "counterWeight", where, 0, 2000), _json.Int(element, "exposedOnSuccess", where, 0, 1000), _json.Int(element, "captureDays", where, 1, MaxDays),
            _json.Int(element, "exposedHonour", where, 0, 1000), _json.Int(element, "intelDecayPpm", where, 0, 1_000_000), _json.Int(element, "counterDecayPpm", where, 0, 1_000_000), levels, errors,
            _json.Int(element, "epochDays", where, 1, MaxDays));
    }
}
