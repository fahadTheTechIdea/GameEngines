using System.Text.Json;
using EngineCommon;
using ScenarioEngine.Contracts;

namespace CalendarEngine;

internal sealed partial class CalendarReader
{
    private IReadOnlyList<PlaceDef> ReadPlaces(JsonElement array, string what)
    {
        var places = new List<PlaceDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = what + "[" + places.Count + "]";
            _json.Object(element, where, ["id", "key"]);
            var id = _json.Id(element, "id", where);
            if (!seen.Add(id)) throw _json.Refuse(CalendarCodes.Duplicate, what + " '" + id + "' is declared twice");
            places.Add(new PlaceDef(id, _json.Id(element, "key", where)));
        }
        return places;
    }

    private IReadOnlyList<InterruptDef> ReadInterrupts(JsonElement array, IReadOnlyList<PlaceDef> locations, IReadOnlyList<PlaceDef> activities)
    {
        var interrupts = new List<InterruptDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "interrupts[" + interrupts.Count + "]";
            _json.Object(element, where, ["id", "priority", "location", "activity"]);
            var id = _json.Id(element, "id", where);
            if (!seen.Add(id)) throw _json.Refuse(CalendarCodes.Duplicate, "interrupt '" + id + "' is declared twice");
            interrupts.Add(new InterruptDef(id, _json.Int(element, "priority", where, 0, 1000), Place(element, "location", where, locations), Place(element, "activity", where, activities)));
        }
        return interrupts;
    }

    private string Place(JsonElement owner, string field, string where, IReadOnlyList<PlaceDef> declared)
    {
        var id = _json.Id(owner, field, where);
        return declared.Any(p => p.Id == id) ? id : throw _json.Refuse(CalendarCodes.Reference, where + "." + field + " '" + id + "' is not declared");
    }

    private IReadOnlyDictionary<string, FactDefinition> ReadHostFacts(JsonElement array)
    {
        var facts = new SortedDictionary<string, FactDefinition>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "facts[" + facts.Count + "]";
            _json.Object(element, where, ["id", "type"], "min", "max");
            var id = _json.Id(element, "id", where);
            if (id.StartsWith(CalendarFacts.Prefix, StringComparison.Ordinal)) throw _json.Refuse(CalendarCodes.Reserved, where + " '" + id + "': the prefix 'cal.' is the calendar own");
            if (facts.ContainsKey(id)) throw _json.Refuse(CalendarCodes.Duplicate, "fact '" + id + "' is declared twice");
            var type = _json.Text(element, "type", where, 8);
            var hasRange = _json.Has(element, "min") || _json.Has(element, "max");
            if (type is not ("bool" or "string" or "int")) throw _json.Refuse(CalendarCodes.Format, where + ".type '" + type + "' must be int, bool or string");
            if (type == "int")
            {
                if (!_json.Has(element, "min") || !_json.Has(element, "max")) throw _json.Refuse(CalendarCodes.MissingField, where + " is a whole number and needs both min and max");
                var min = _json.Long(element, "min", where, -1_000_000_000, 1_000_000_000);
                var max = _json.Long(element, "max", where, -1_000_000_000, 1_000_000_000);
                if (min > max) throw _json.Refuse(CalendarCodes.Range, where + " has min above max");
                facts[id] = new FactDefinition(FactKind.Integer, min, max);
            }
            else
            {
                if (hasRange) throw _json.Refuse(CalendarCodes.Format, where + " is not a whole number and has no range");
                facts[id] = new FactDefinition(type == "bool" ? FactKind.Boolean : FactKind.String);
            }
        }
        return facts;
    }

    private IReadOnlyList<ScheduleDef> ReadSchedules(JsonElement array, IReadOnlyList<PlaceDef> locations, IReadOnlyList<PlaceDef> activities, IReadOnlyDictionary<string, FactDefinition> hostFacts)
    {
        var known = new Dictionary<string, FactDefinition>(CalendarFacts.Definitions(_system, _moons), StringComparer.Ordinal);
        foreach (var (id, def) in hostFacts) known[id] = def;
        var schedules = new List<ScheduleDef>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in array.EnumerateArray())
        {
            var where = "schedules[" + schedules.Count + "]";
            _json.Object(element, where, ["id", "routines"]);
            var id = _json.Id(element, "id", where);
            if (!seen.Add(id)) throw _json.Refuse(CalendarCodes.Duplicate, "schedule '" + id + "' is declared twice");
            var routines = new List<RoutineDef>();
            var routineIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var routine in _json.Array(element, "routines", where, 1, 64).EnumerateArray())
            {
                var routineWhere = "schedule '" + id + "' routines[" + routines.Count + "]";
                routines.Add(ReadRoutine(routine, routines.Count, routineWhere, routineIds, locations, activities, known));
            }
            CheckSchedule(id, routines);
            schedules.Add(new ScheduleDef(schedules.Count, id, routines));
        }
        return schedules;
    }

    private RoutineDef ReadRoutine(JsonElement element, int index, string where, HashSet<string> ids, IReadOnlyList<PlaceDef> locations, IReadOnlyList<PlaceDef> activities, Dictionary<string, FactDefinition> facts)
    {
        _json.Object(element, where, ["id", "priority", "blocks"], "when");
        var id = _json.Id(element, "id", where);
        if (!ids.Add(id)) throw _json.Refuse(CalendarCodes.Duplicate, where + " repeats routine id '" + id + "'");
        var priority = _json.Int(element, "priority", where, 0, 1000);
        Selector? selector = null;
        if (_json.Has(element, "when")) selector = ReadSelector(element.GetProperty("when"), where + ".when", facts);
        var blocks = new List<BlockDef>();
        var blockIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var block in _json.Array(element, "blocks", where, 1, 96).EnumerateArray())
        {
            var blockWhere = where + " blocks[" + blocks.Count + "]";
            _json.Object(block, blockWhere, ["id", "from", "to", "location", "activity"]);
            var blockId = _json.Id(block, "id", blockWhere);
            if (!blockIds.Add(blockId)) throw _json.Refuse(CalendarCodes.Duplicate, where + " repeats block id '" + blockId + "'");
            var from = ReadClock(block, "from", blockWhere, false);
            var to = ReadClock(block, "to", blockWhere, true);
            if (from >= to) throw _json.Refuse(CalendarCodes.Order, blockWhere + " ends at or before it starts");
            if (blocks.Count > 0 && from < blocks[^1].To)
                throw _json.Refuse(from >= blocks[^1].From ? CalendarCodes.Overlap : CalendarCodes.Order, blockWhere + (from >= blocks[^1].From ? " overlaps the block before it" : " is not in time order"));
            blocks.Add(new BlockDef(blockId, from, to, Place(block, "location", blockWhere, locations), Place(block, "activity", blockWhere, activities)));
        }
        return new RoutineDef(index, id, priority, selector, blocks);
    }

    private Selector ReadSelector(JsonElement element, string where, Dictionary<string, FactDefinition> facts)
    {
        _json.Object(element, where, [], "weekdays", "months", "seasons", "holidays", "dates", "condition");
        var weekdays = _json.Has(element, "weekdays") ? IdList(element, "weekdays", where, _weekdayIndex, "weekday") : [];
        var months = _json.Has(element, "months") ? IdList(element, "months", where, _monthIndex, "month") : [];
        var seasons = _json.Has(element, "seasons") ? IdList(element, "seasons", where, _seasonIndex, "season") : [];
        var holidays = _json.Has(element, "holidays") ? IdList(element, "holidays", where, _holidayIndex, "holiday") : [];
        var dates = _json.Has(element, "dates") ? ReadRule(element.GetProperty("dates"), where + ".dates") : null;
        var condition = _json.Has(element, "condition") ? _conditions.Read(element.GetProperty("condition"), where + ".condition", facts) : null;
        var selector = new Selector(weekdays, months, seasons, holidays, dates, condition, condition is null ? [] : Conditions.FactsOf(condition).Where(f => !f.StartsWith(CalendarFacts.Prefix, StringComparison.Ordinal)).ToList());
        return selector.IsEmpty ? throw _json.Refuse(CalendarCodes.Format, where + " names nothing; leave 'when' out for the routine that always applies") : selector;
    }

    /// <summary>A schedule has exactly one routine that always applies, with the lowest priority and every minute of the day covered, and no two routines share a priority, so a question about any actor at any time has one answer.</summary>
    private void CheckSchedule(string id, List<RoutineDef> routines)
    {
        var bases = routines.Where(r => r.Selector is null).ToList();
        if (bases.Count != 1) throw _json.Refuse(CalendarCodes.Base, "schedule '" + id + "' must have exactly one routine without 'when' (it has " + bases.Count + ")");
        var baseRoutine = bases[0];
        var tied = routines.GroupBy(r => r.Priority).FirstOrDefault(g => g.Count() > 1);
        if (tied is not null) throw _json.Refuse(CalendarCodes.Tie, "schedule '" + id + "' has routines '" + tied.First().Id + "' and '" + tied.Last().Id + "' with the same priority " + tied.Key);
        if (routines.Any(r => r.Priority < baseRoutine.Priority)) throw _json.Refuse(CalendarCodes.Base, "schedule '" + id + "': the routine that always applies must have the lowest priority");
        var next = 0;
        foreach (var block in baseRoutine.Blocks)
        {
            if (block.From != next) throw _json.Refuse(CalendarCodes.BaseCoverage, "schedule '" + id + "': the base routine '" + baseRoutine.Id + "' leaves minutes " + next + " to " + block.From + " without a block");
            next = block.To;
        }
        if (next != _time.MinutesPerDay) throw _json.Refuse(CalendarCodes.BaseCoverage, "schedule '" + id + "': the base routine '" + baseRoutine.Id + "' stops at minute " + next + ", the day has " + _time.MinutesPerDay);
    }
}
