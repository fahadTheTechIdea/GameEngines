---
name: gameframework-calendar
description: "Use GameFrameWork's CalendarEngine for in-game time: invented or real calendars with whole-number date arithmetic, leap rules, seasons and daylight, moons, holidays and RFC-5545-style recurrence, actor schedules with interrupts, a rational runtime clock with backlog, cal.* facts and save/replay. Use when a game has days, seasons, festivals, shop hours or 'every third day' rules."
---

# CalendarEngine

Reference: `docs/engines/CalendarEngine.html`; design record `docs/CALENDAR_ENGINE.md`.
Code: `src/CalendarEngine/CalendarEngine.Core` (net8.0), CLI, `CalendarEngine.Sim`; component `src/GameRuntime/GameRuntime.Calendar`.

## Boundary

- Owns: calendar declarations, date arithmetic, recurrence, seasons/daylight, moons, holidays, schedules, interrupts, time advancement, facts, save.
- Does not own: display strings (it emits localisation keys), NPC movement, rendering, real-world time.
- Whole numbers only. No `DateTime`, no floating point, no wall clock, no random.

## Document

`CalendarLoader.Load(json)` reads strict format 1: `time`, `epoch`, `weekMode`, `weekdays`, `months`, `leap`, `eras`, `dayParts`, `seasons`, `twilightMinutes`, `moons`, `holidays`, `formats`, `locations`, `activities`, `interrupts`, `facts`, `schedules`.
Every named thing has a stable `id` and a translator-facing `key`. Start from `samples/gregorian.calendar.json` or `valley.calendar.json`.

## Dates

```
long day      = set.System.DayNumber(new CalendarDate(year, monthIndex, day)); // month 0-based
DayInfo info  = set.System.DayOf(day);
Moment moment = set.System.MomentOf(instant);
```

- Years −100,000..100,000; instants outside are refused.
- `set.DaylightAt(season, minuteOfDay)` → `IsDay`, 0–1000 light, sunrise, sunset.
- `set.MoonAt(i, instant)` → 8-phase `MoonPhase` + permille, exact fractions.
- `set.HolidaysStartingOn/DayOn`, `NextOccurrence`, `NextHoliday` for festival logic.
- Recurrence: FREQ, INTERVAL, UNTIL, BYMONTH, BYMONTHDAY (±), BYDAY (ordinal), BYSEASON; COUNT/BYSETPOS refused.

## Runtime clock

```
var rt = CalendarRuntime.Create(set, startInstant, scaleNumerator, scaleDenominator, startTick);
TimeStep? step = rt.Step(gameTick);   // rational scale, exact remainder, backlog beyond 400 days
rt.SkipTo(instant);                    // at most 366 days, ignores pause/scale
rt.AddActor(id, schedule); rt.Interrupt(actor, kind, durationTicks);
var answer = rt.WhereIs(rt.Context(hostFacts), actor);
var changes = rt.Refresh(context, max); // bounded, id order, rest next call
```

- Scale: 1..366 days per game tick, denominator ≤ 10,000; changing scale drops the sub-tick remainder.
- Max 100,000 actors; ids 1..128 chars; interrupt duration 1 tick..366 days; priority outranking refused.
- Monotonic ticks; one Step accepts ≤ 50,000,000 elapsed game ticks.
- `TimeStep` reports clamped, day-part change, every season change, first 64 holiday starts + omitted count.

## Facts

`CalendarFacts.Read(set, moment)` yields `cal.year`, `cal.month_number`, `cal.day_of_month`, `cal.day_of_year`, `cal.weekday_number`, `cal.hour`, `cal.minute`, `cal.minute_of_day`, `cal.light`, `cal.month`, `cal.weekday`, `cal.season`, `cal.daypart`, `cal.era`, `cal.holiday_id`, `cal.is_day`, `cal.is_holiday`, `cal.is_leap_year`, `cal.moon.<id>`.
`CalendarFacts.Definitions(system, moons)` declares them to dialogue or AI documents.

## GameRuntime component

`CalendarChannels.Install(session)` before `Init`. Requests: scale, pause, skip, actor add/remove, interrupt, interrupt-clear, where. Answers: `calendar.applied`, `calendar.refused`, `calendar.where-answered`. Changes: `calendar.time-advanced`, `day-changed`, `day-part-changed`, `season-changed`, `holiday-started`, `schedule-block-changed`. Use `CalendarJournal`/`CalendarTranscript`/`CalendarReplay` for deterministic replay and `CalendarCheckpoint` to save.

## CLI

`check`, `explain <schedule> <date> <H:MM>`, `convert instant|date`, `month-view`, `coverage`.
Exit codes: 0 done, 1 refused/incomplete, 2 usage/file problem.

Tests: `dotnet run --project src/CalendarEngine/CalendarEngine.Tests -c Release`.
