using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace TutorialEngine;

internal sealed partial class TutorialReader
{
    private IReadOnlyList<StepDef> ReadSteps(JsonElement array)
    {
        var steps = new List<StepDef>();
        var skills = Index(_skills, s => s.Id);
        foreach (var element in array.EnumerateArray())
        {
            var where = "steps[" + steps.Count + "]";
            _json.Object(element, where, ["id", "key", "body", "target", "motion", "skippable", "skill", "requires", "requiresAny", "complete", "hints"], "when");
            var requires = ReadPrerequisites(element, "requires", where, steps.Count);
            var requiresAny = ReadPrerequisites(element, "requiresAny", where, steps.Count);
            Condition? when = null;
            var whenFacts = (IReadOnlyList<string>)Array.Empty<string>();
            if (_json.Has(element, "when"))
            {
                when = ReadScoped(element.GetProperty("when"), where + ".when");
                whenFacts = Conditions.FactsOf(when);
            }
            var complete = _conditions.Read(element.GetProperty("complete"), where + ".complete", _facts);
            var since = SinceFacts(complete).Select(f => _bindings[f].Index).ToList();       // each fact once, and one fact per event, so each event once
            var target = _json.Text(element, "target", where, 128);
            if (target.Length > 0) _json.CheckId(target, where + ".target");
            steps.Add(new StepDef(steps.Count, _json.Id(element, "id", where), _json.Id(element, "key", where), _json.Id(element, "body", where), target, Choice(element, "motion", where, Motions),
                _json.Bool(element, "skippable", where), OptionalRef(element, "skill", where, skills, "skill"), requires, requiresAny, when, whenFacts, complete, Conditions.FactsOf(complete), since,
                ReadHints(element, where)));
        }
        return steps;
    }

    /// <summary>Prerequisites name steps declared earlier in the document, each once; so a step can never wait for itself or for a cycle.</summary>
    private IReadOnlyList<int> ReadPrerequisites(JsonElement owner, string field, string where, int self)
    {
        var list = new List<int>();
        foreach (var item in _json.Array(owner, field, where, 0, MaxRequires).EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) throw _json.Refuse(TutorialCodes.Format, where + "." + field + " must hold texts");
            var id = _json.CheckId(item.GetString()!, where + "." + field);
            if (!_stepIndex.TryGetValue(id, out var index)) throw _json.Refuse(TutorialCodes.Reference, where + "." + field + " '" + id + "' is not a declared step");
            if (index >= self) throw _json.Refuse(TutorialCodes.Prereq, where + "." + field + " '" + id + "' is not declared before this step");
            if (list.Contains(index)) throw _json.Refuse(TutorialCodes.Duplicate, where + "." + field + " repeats '" + id + "'");
            list.Add(index);
        }
        return list;
    }

    private IReadOnlyList<HintDef> ReadHints(JsonElement owner, string where)
    {
        var hints = new List<HintDef>();
        foreach (var item in _json.Array(owner, "hints", where, 0, MaxHints).EnumerateArray())
        {
            var at = where + ".hints[" + hints.Count + "]";
            _json.Object(item, at, ["idle", "kind", "key"]);
            var idle = _json.Int(item, "idle", at, 1, TimeCeiling);
            if (hints.Count > 0 && idle <= hints[^1].Idle) throw _json.Refuse(TutorialCodes.Hint, at + ".idle " + idle + " must be above the hint before it (" + hints[^1].Idle + "): hints escalate");
            var kind = Choice(item, "kind", at, HintKinds);
            if (hints.Count > 0 && kind < hints[^1].Kind) throw _json.Refuse(TutorialCodes.Hint, at + ".kind must not be gentler than the hint before it: nudge, then explain, then show");
            hints.Add(new HintDef(idle, kind, _json.Id(item, "key", at)));
        }
        return hints;
    }

    private IReadOnlyList<TipDef> ReadTips(JsonElement array)
    {
        var tips = new List<TipDef>();
        var skills = Index(_skills, s => s.Id);
        foreach (var element in array.EnumerateArray())
        {
            var where = "tips[" + tips.Count + "]";
            _json.Object(element, where, ["id", "key", "when", "cooldown", "maxShows", "duration", "priority", "skill", "afterStep", "motion"]);
            var when = ReadScoped(element.GetProperty("when"), where + ".when");
            tips.Add(new TipDef(tips.Count, _json.Id(element, "id", where), _json.Id(element, "key", where), when, Conditions.FactsOf(when), _json.Int(element, "cooldown", where, 0, TimeCeiling),
                _json.Int(element, "maxShows", where, 1, MaxTipShows), _json.Int(element, "duration", where, 1, TimeCeiling), _json.Int(element, "priority", where, -1000, 1000),
                OptionalRef(element, "skill", where, skills, "skill"), OptionalRef(element, "afterStep", where, _stepIndex, "step"), Choice(element, "motion", where, Motions)));
        }
        return tips;
    }

    private JourneyDef ReadJourney(JsonElement element)
    {
        _json.Object(element, "journey", ["languages", "defaultLanguage", "pages", "entries"]);
        var languages = new List<string>();
        foreach (var item in _json.Array(element, "languages", "journey", 1, MaxLanguages).EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) throw _json.Refuse(TutorialCodes.Format, "journey.languages must hold texts");
            var tag = item.GetString()!;
            if (!LanguageTag.IsMatch(tag)) throw _json.Refuse(TutorialCodes.Journey, "journey.languages '" + tag + "' is not a language tag such as en or fr-CA");
            if (languages.Contains(tag, StringComparer.Ordinal)) throw _json.Refuse(TutorialCodes.Duplicate, "journey.languages repeats '" + tag + "'");
            languages.Add(tag);
        }
        var defaultLanguage = _json.Text(element, "defaultLanguage", "journey", 32);
        if (!languages.Contains(defaultLanguage, StringComparer.Ordinal)) throw _json.Refuse(TutorialCodes.Journey, "journey.defaultLanguage '" + defaultLanguage + "' is not one of the languages");
        var pages = new List<PageDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in _json.Array(element, "pages", "journey", 1, MaxPages).EnumerateArray())
        {
            var where = "journey.pages[" + pages.Count + "]";
            _json.Object(item, where, ["id", "key", "body", "art"]);
            var id = _json.Id(item, "id", where);
            Unique(seen, id, "story page");
            pages.Add(new PageDef(id, _json.Id(item, "key", where), _json.Id(item, "body", where), _json.Id(item, "art", where)));
        }
        var entries = new List<string>();
        foreach (var item in _json.Array(element, "entries", "journey", 1, EntryKinds.Count).EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) throw _json.Refuse(TutorialCodes.Format, "journey.entries must hold texts");
            var kind = item.GetString()!;
            if (!EntryKinds.Contains(kind)) throw _json.Refuse(TutorialCodes.Journey, "journey.entries '" + kind + "' must be one of " + string.Join(", ", EntryKinds));
            if (entries.Contains(kind)) throw _json.Refuse(TutorialCodes.Duplicate, "journey.entries repeats '" + kind + "'");
            entries.Add(kind);
        }
        return new JourneyDef(languages, defaultLanguage, pages, entries);
    }
}
