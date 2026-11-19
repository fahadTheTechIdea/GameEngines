using System.Text.Json;
using ScenarioEngine.Contracts;

namespace DiplomacyEngine;

internal sealed partial class DipReader
{
    private IReadOnlyList<CasusBelliDef> ReadCasusBelli(JsonElement array)
    {
        var list = new List<CasusBelliDef>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "casusBelli[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "source", "infamy", "honour"], "requires");
            var source = Word<CbSource>(element, "source", where);
            var requires = OptionalWhen(element, "requires", where);
            if (source == CbSource.Standing && requires is null) throw _json.Refuse(DipCodes.MissingField, where + " holds whenever its condition does, and has no 'requires'");
            if (source == CbSource.Granted && requires is not null) throw _json.Refuse(DipCodes.Format, where + " is granted, and has no 'requires'");
            list.Add(new CasusBelliDef(list.Count, _json.Id(element, "id", where), _json.Id(element, "key", where), source, requires, _json.Int(element, "infamy", where, 0, 1000),
                _json.Int(element, "honour", where, 0, 1000)));
        }
        return list;
    }

    private IReadOnlyList<MissionDef> ReadMissions(JsonElement array)
    {
        var list = new List<MissionDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "missions[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "kind", "days", "difficulty", "exposure", "gain", "shock", "shockScale", "stabilityHit", "deed", "casusBelli"]);
            var id = _json.Id(element, "id", where);
            Unique(seen, id, "mission");
            var kind = Word<MissionKind>(element, "kind", where);
            var shock = _json.Text(element, "shock", where, 128);
            if (shock.Length > 0) _json.CheckId(shock, where + ".shock");
            var stabilityHit = _json.Int(element, "stabilityHit", where, 0, 1000);
            if (shock.Length > 0 && kind != MissionKind.Sabotage) throw _json.Refuse(DipCodes.Espionage, where + " asks for a shock and is not a sabotage");
            if (stabilityHit > 0 && kind is not (MissionKind.Sabotage or MissionKind.Incite)) throw _json.Refuse(DipCodes.Espionage, where + " hurts stability and is neither a sabotage nor an incitement");
            var cb = OptionalRef(element, "casusBelli", where, _cbIndex, "casus belli");
            if (cb >= 0 && _parts.CasusBelli[cb].Source != CbSource.Granted) throw _json.Refuse(DipCodes.Reference, where + ".casusBelli '" + _parts.CasusBelli[cb].Id + "' is not a granted casus belli");
            list.Add(new MissionDef(list.Count, id, _json.Id(element, "key", where), kind, _json.Int(element, "days", where, 1, MaxDays), _json.Int(element, "difficulty", where, 0, 1000), _json.Int(element, "exposure", where, 0, 1000),
                _json.Int(element, "gain", where, 0, 1000), shock, _json.Int(element, "shockScale", where, 0, 10_000), stabilityHit, OptionalRef(element, "deed", where, _deedIndex, "deed"), cb));
        }
        return list;
    }

    private IReadOnlyList<TreatyTypeDef> ReadTypes(JsonElement array)
    {
        var list = new List<TreatyTypeDef>();
        foreach (var element in array.EnumerateArray())
        {
            var where = "types[" + list.Count + "]";
            _json.Object(element, where, ["id", "key", "consent", "clauses", "permanent", "minDays", "maxDays", "considerDays", "expireDays", "regard", "excludes", "duringWar", "endsWar", "endsOnSuccession", "cancel", "breach",
                "signDeed", "bias", "weights", "modifiers", "onSign", "onEnd", "onBreach"], "requires");
            var id = _json.Id(element, "id", where);
            var consent = Word<Consent>(element, "consent", where);
            var clauses = ReadClauses(_json.Array(element, "clauses", where, 1, MaxClauses), where + ".clauses");
            var permanent = _json.Bool(element, "permanent", where);
            var minDays = _json.Int(element, "minDays", where, 0, MaxDays);
            var maxDays = _json.Int(element, "maxDays", where, 0, MaxDays);
            if (maxDays < minDays) throw _json.Refuse(DipCodes.Range, where + ".maxDays is below minDays");
            if (!permanent && minDays < 1) throw _json.Refuse(DipCodes.Range, where + " is not permanent and needs minDays of at least 1");
            var consider = _json.Int(element, "considerDays", where, 0, MaxDays);
            var expire = _json.Int(element, "expireDays", where, 0, MaxDays);
            if (consent == Consent.Imposed && (consider != 0 || expire != 0)) throw _json.Refuse(DipCodes.Clause, where + " is imposed and is never considered or left waiting");
            if (consent == Consent.Mutual && expire < 1) throw _json.Refuse(DipCodes.Range, where + ".expireDays of a proposal is at least 1");
            if (consent == Consent.Mutual && consider > expire) throw _json.Refuse(DipCodes.Range, where + ".considerDays is after expireDays");
            var duringWar = _json.Bool(element, "duringWar", where);
            var endsWar = _json.Bool(element, "endsWar", where);
            var truce = clauses.Any(c => c.Kind == ClauseKind.Truce);
            if ((truce || endsWar) && !duringWar) throw _json.Refuse(DipCodes.Clause, where + " ends or pauses a war and must be allowed during one (duringWar)");
            if (truce && endsWar) throw _json.Refuse(DipCodes.Clause, where + " cannot both pause a war and end it");
            if ((truce || endsWar) && consent != Consent.Mutual) throw _json.Refuse(DipCodes.Clause, where + " ends or pauses a war and needs the other realm's consent");
            var breach = element.GetProperty("breach");
            const string bw = "breach";
            var at = where + "." + bw;
            _json.Object(breach, at, ["trust", "honour", "deed", "casusBelli", "cbDays"]);
            var grants = OptionalRef(breach, "casusBelli", at, _cbIndex, "casus belli");
            var cbDays = _json.Int(breach, "cbDays", at, 0, MaxDays);
            if (grants >= 0)
            {
                if (_parts.CasusBelli[grants].Source != CbSource.Granted) throw _json.Refuse(DipCodes.Reference, at + ".casusBelli '" + _parts.CasusBelli[grants].Id + "' is not a granted casus belli");
                if (cbDays < 1) throw _json.Refuse(DipCodes.Range, at + " grants a casus belli for no days");
            }
            var excludes = new List<int>();
            foreach (var item in _json.Array(element, "excludes", where, 0, MaxTypes).EnumerateArray())
            {
                var name = item.ValueKind == JsonValueKind.String ? item.GetString()! : "";
                if (!_typeIndex.TryGetValue(name, out var other)) throw _json.Refuse(DipCodes.Reference, where + ".excludes '" + name + "' is not a declared type");
                if (other == list.Count) throw _json.Refuse(DipCodes.Clause, where + ".excludes names itself");
                if (excludes.Contains(other)) throw _json.Refuse(DipCodes.Duplicate, where + ".excludes names '" + name + "' twice");
                excludes.Add(other);
            }
            list.Add(new TreatyTypeDef(list.Count, id, _json.Id(element, "key", where), consent, clauses, permanent, minDays, maxDays, consider, expire, _json.Int(element, "regard", where, -1000, 1000),
                OptionalWhen(element, "requires", where), excludes, duringWar, endsWar, _json.Bool(element, "endsOnSuccession", where), Word<CancelRule>(element, "cancel", where),
                _json.Int(breach, "trust", at, 0, 1000), _json.Int(breach, "honour", at, 0, 1000), OptionalRef(breach, "deed", at, _deedIndex, "deed"), grants, cbDays, OptionalRef(element, "signDeed", where, _deedIndex, "deed"),
                _json.Int(element, "bias", where, -100_000, 100_000), ReadWeights(element.GetProperty("weights"), where + ".weights"), ReadModifiers(_json.Array(element, "modifiers", where, 0, MaxModifiers), where + ".modifiers"),
                ReadEffects(_json.Array(element, "onSign", where, 0, MaxEffects), where + ".onSign"), ReadEffects(_json.Array(element, "onEnd", where, 0, MaxEffects), where + ".onEnd"),
                ReadEffects(_json.Array(element, "onBreach", where, 0, MaxEffects), where + ".onBreach")));
        }
        return list;
    }

    private IReadOnlyList<ClauseDef> ReadClauses(JsonElement array, string where)
    {
        var list = new List<ClauseDef>();
        foreach (var element in array.EnumerateArray())
        {
            var at = where + "[" + list.Count + "]";
            _json.Object(element, at, ["kind"], "intervalDays", "graceDays", "maxMissed", "dueSeason", "optional", "mutual");
            var kind = Word<ClauseKind>(element, "kind", at);
            if (list.Any(c => c.Kind == kind)) throw _json.Refuse(DipCodes.Duplicate, at + " repeats the clause '" + EngineCommon.EnumNames.Name(kind) + "'");
            string[] allowed = [];
            if (kind == ClauseKind.Tribute) allowed = ["intervalDays", "graceDays", "maxMissed", "dueSeason", "optional"];
            else if (kind == ClauseKind.Defend) allowed = ["graceDays", "mutual"];
            foreach (var property in element.EnumerateObject())
                if (property.Name != "kind" && !allowed.Contains(property.Name)) throw _json.Refuse(DipCodes.Clause, at + " is a '" + EngineCommon.EnumNames.Name(kind) + "' clause and has no '" + property.Name + "'");
            foreach (var name in allowed)
                if (!_json.Has(element, name)) throw _json.Refuse(DipCodes.Clause, at + " is a '" + EngineCommon.EnumNames.Name(kind) + "' clause and needs '" + name + "'");
            if (kind == ClauseKind.Tribute)
            {
                var season = _json.Text(element, "dueSeason", at, 128);
                var seasonIndex = -1;
                if (season.Length > 0 && !_seasonIndex.TryGetValue(season, out seasonIndex)) throw _json.Refuse(DipCodes.Reference, at + ".dueSeason '" + season + "' is not a declared season");
                list.Add(new ClauseDef(kind, _json.Int(element, "intervalDays", at, 1, MaxDays), _json.Int(element, "graceDays", at, 0, MaxDays), _json.Int(element, "maxMissed", at, 1, 1000), season.Length == 0 ? -1 : seasonIndex,
                    _json.Bool(element, "optional", at), false));
            }
            else if (kind == ClauseKind.Defend) list.Add(new ClauseDef(kind, 0, _json.Int(element, "graceDays", at, 0, MaxDays), 0, -1, false, _json.Bool(element, "mutual", at)));
            else list.Add(new ClauseDef(kind, 0, 0, 0, -1, false, false));
        }
        return list;
    }

    private int[] ReadWeights(JsonElement element, string where)
    {
        if (element.ValueKind != JsonValueKind.Object) throw _json.Refuse(DipCodes.Format, where + " must be an object");
        var weights = new int[Enum.GetValues<FactorSource>().Length];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            var source = EngineCommon.EnumNames.Parse<FactorSource>(property.Name);
            if (source is null) throw _json.Refuse(DipCodes.UnknownField, where + " has '" + property.Name + "', which is none of " + EngineCommon.EnumNames.Choices<FactorSource>());
            if (!seen.Add(property.Name)) throw _json.Refuse(DipCodes.Duplicate, where + " repeats '" + property.Name + "'");
            weights[(int)source.Value] = _json.Int(element, property.Name, where, -5000, 5000);
        }
        return weights;
    }

    private IReadOnlyList<ModifierDef> ReadModifiers(JsonElement array, string where)
    {
        var list = new List<ModifierDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var at = where + "[" + list.Count + "]";
            _json.Object(element, at, ["id", "key", "when", "value"]);
            var id = _json.Id(element, "id", at);
            Unique(seen, id, "modifier");
            list.Add(new ModifierDef(id, _json.Id(element, "key", at), _conditions.Read(element.GetProperty("when"), at + ".when", _parts.PairFacts), _json.Int(element, "value", at, -100_000, 100_000)));
        }
        return list;
    }

    private IReadOnlyList<EffectDef> ReadEffects(JsonElement array, string where)
    {
        var list = new List<EffectDef>();
        foreach (var element in array.EnumerateArray())
        {
            var at = where + "[" + list.Count + "]";
            _json.Object(element, at, ["kind", "name", "scale", "target"]);
            var kind = Word<EffectKind>(element, "kind", at);
            if (kind == EffectKind.Payment) throw _json.Refuse(DipCodes.Format, at + ".kind 'payment' is made by tribute clauses and cannot be declared");
            list.Add(new EffectDef(kind, _json.Id(element, "name", at), _json.Int(element, "scale", at, 0, 10_000), Word<EffectTarget>(element, "target", at)));
        }
        return list;
    }
}
