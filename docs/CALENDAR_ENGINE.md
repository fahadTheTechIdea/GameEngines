# CalendarEngine: research, boundary, contract, criteria and result

Pass 34, 2026-10-28. The first of six planned engines (Calendar, Relationship, Economy, Tutorial/Onboarding, WorldEvents, Diplomacy). Written before the code; section 10 records what was built and measured.

## 1. What already owns time (evidence, and the decision)

Read in the source on 2026-10-28:

| Owner | What it holds | Where |
|---|---|---|
| `GameRuntime.Core` | The one session clock: a tick counter, an exact rational speed (`n/d`, remainder carried), pause, a catch-up cap; components receive `UpdateContext.Tick` and own no counter | `RuntimeClock.cs`, `RuntimeSession.cs`, `Component.cs` |
| SimEngine | `ISimulationTime` (a tick is a rational number of seconds), `GameDuration`, and an optional `ICalendar` that projects a tick to a `CalendarReading` of named fields (`CycleCalendarDef`: radix cycles, no months, leap rules, weekdays, seasons or eras) | `Core/GameTime.cs`, `Core/CalendarReading.cs`, `Content/SimulationTimeDef.cs` |
| ScenarioEngine, DialogueEngine, AIEngine | Nothing about dates. Dialogue and AI read the world through facts (`ext.*`) and percepts | |
| nobody | Months, weeks, leap years, eras, day parts, seasons with daylight, holidays, recurring dates, and "where is X at time T" | |

Decision: **the calendar owns what no one owns and owns no clock.** It is a pure function of a count of ticks, a document, and a few facts the host supplies. The game layer's session clock stays the only clock; the calendar component converts game ticks to calendar ticks with an exact rational scale and advances only inside its `Update`. SimEngine keeps its tick and its `ICalendar` seam; the calendar plugs into that seam (`CalendarEngine.Sim`) instead of competing with it. Nothing reads `DateTime`, a stopwatch, a random generator or a float; a source guard fails if any of them appears.

## 2. Research (searched 2026-10-28)

P means a primary source reached; the others are said to be secondary, partial or not reached.

- **RFC 5545 (iCalendar) section 3.3.10, recurrence rule.** P, first 100,000 of 378,733 characters only. Read: rule parts are `NAME=VALUE`; FREQ is required; INTERVAL defaults to 1; UNTIL is inclusive and mutually exclusive with COUNT; `BYDAY` takes an optional `+n` or `-n` ordinal that means the nth in the month or year for MONTHLY and YEARLY rules (`-1MO` is the last Monday). The excerpt stopped before BYMONTH/BYMONTHDAY/BYSETPOS expansion rules; those are taken from my memory of the RFC (negative `BYMONTHDAY` counts from the end; BYxxx parts limit or expand depending on FREQ; defaults come from DTSTART). Not checked.
- **ISO 8601.** Secondary only (Wikipedia, "ISO 8601"; the standard is paywalled). Read: the Gregorian calendar extended backwards is the proleptic Gregorian calendar and is allowed by agreement; year 0000 is 1 BC (astronomical numbering, expanded years carry a sign); week dates (week 01 contains the first Thursday); ordinal dates `YYYY-DDD`; `24:00` was removed in 2019 and reinstated by the 2022 amendment. I use astronomical years and the Gregorian leap rule; ISO week numbering is not implemented (section 10.3).
- **Unicode CLDR / UTS #35 Dates (`tr35-dates.html`).** P, first 100,000 of 198,864 characters. Read: each calendar has its own era, month and weekday data; names have a *format* and a *stand-alone* context and *wide / abbreviated / narrow* widths; `dayPeriods` replace am/pm, with rule sets per locale; date patterns come from skeletons (`yMMMMd`); era dates are given as proleptic Gregorian dates. I take: names are keys resolved per locale (not text in the document), eras are a first-class list, day parts are a first-class list. I avoid: contexts and widths (a game format chooses its own words), skeleton matching, non-Gregorian CLDR calendars.
- **Godot, "Idle and physics processing".** P. `_physics_process` runs at a fixed rate, independent of frame rate; `_process` varies; delta scales speed. The game layer maps to the physics loop; the calendar sees ticks, never delta.
- **Gaffer On Games, "Fix Your Timestep".** P. A fixed step with an accumulator, the leftover carried; the *spiral of death* when simulation costs more than it advances, answered by capping steps per frame; a fixed step makes the simulation deterministic and floating-point-free. Matches `RuntimeClock` (cap, remainder) and my bounded step with a backlog.
- **RimWorld wiki, "Time".** P, partial. 60,000 ticks a day, 2,500 an hour, 15-day quadrums, a 60-day year. The page does not cover the work/sleep/joy schedule grid; I do not claim it.
- **Stardew Valley.** Wikipedia (secondary): four 28-day seasons, open-ended. The wiki pages "Calendar" and "Modding:Schedule data" returned HTTP 403. From a search snippet of the schedule page only: schedule keys exist for a date (season and day of month), a day of the month, a day of the week (with a season and a heart level), rain (`rain`, and `rain2` at 50%), and a few special keys; when several heart-gated keys match the highest heart count wins; with no matching key the character has no schedule that day. The full precedence order between non-heart keys was **not** confirmed.
- **Dwarf Fortress wiki, "Time".** The page returned HTTP 429. From search snippets: fortress mode 1,200 ticks a day and 403,200 a year; 28 days a month, 12 months a year. Not verified on the page.
- **Animal Crossing: New Horizons.** Wikipedia (secondary): runs on the real clock; seasons follow the hemisphere of the player's real location; events follow national holidays; changing the console clock skips waits and the developers reduced the incentive rather than banning it.
- **Calendar of Harptos.** Secondary only (fan wikis and campaign sites in a search): twelve 30-day months in three ten-day weeks, five festival days between months, and Shieldmeet every fourth year after Midsummer. The Wikipedia page returned 404. Used for the fictional-calendar sample, which says it is illustrative.
- **The Sims.** Search snippets only (motives such as hunger and energy drive autonomy; a community request for schedules/routines because autonomy alone does not do timed tasks). No source for the day length. Nothing adopted from it except the observation that need-driven choice and a schedule are different things (see section 3).
- **Not reached at all:** Crusader Kings (no source read; I use nothing from it), Harvest Moon (none), ICU documentation, the rest of RFC 5545 and UTS 35, the ISO 8601 text itself.

## 3. Adopt, adapt, avoid

| Source idea | Decision |
|---|---|
| RFC 5545: FREQ/INTERVAL/BYMONTH/BYMONTHDAY/BYDAY with ordinals/UNTIL/DTSTART | **Adopt** as the rule language for holidays and schedule dates, day-granular, in the document's own months and weekdays |
| RFC 5545: COUNT, BYSETPOS, time-of-day parts, time zones | **Avoid.** COUNT needs enumeration from the start (a refusal as an unknown field, not a silent ignore); no time zones: the calendar is the world's own |
| RFC 5545: SEASON | Not in the RFC. **Added** `byseason` as a documented extension |
| ISO 8601 / proleptic Gregorian: astronomical years, one leap rule, ordinal day of year | **Adopt.** Years -100,000 to 100,000; the leap rule is data (the Gregorian rule is three entries) |
| Stardew: 4 x 28 days, weeks restart each month | **Adapt.** `weekMode: restart`; the sample `valley.calendar.json` |
| Harptos: festival days outside months and weeks, a leap festival | **Adapt.** `intercalary` months (no weekday) and a month with 0 common days and some leap days |
| Stardew schedule keys by season, weekday, rain, heart level, highest wins; no match = no schedule | **Adapt.** Routines with a selector (weekdays, months, seasons, holidays, date rule, a condition over facts) and a **distinct priority** per schedule; the highest applicable priority that has a block at that minute answers. **Avoid** "no schedule that day": every schedule has a base routine covering the whole day, so a question always has an answer |
| RimWorld: an hour grid per colonist | **Not adopted.** Blocks are arbitrary intervals; a grid is a document that has 24 blocks |
| Sims: needs drive choice | **Out of scope here.** A schedule says where an actor is expected to be; whether an actor obeys is the AI engine's decision (the AI reads the time as a percept) |
| Dwarf Fortress / RimWorld: ticks per day as a constant | **Adopt** that a day is a whole number of ticks; **adapt** that it is the document's (`hoursPerDay x minutesPerHour x ticksPerMinute`, any shape) |
| Animal Crossing: real clock, time travel | **Avoid** the wall clock entirely; **adopt** that skipping is a first-class, capped, announced request (sleep until morning) rather than a hack on the clock |
| CLDR: names are locale data, with plural and ordinal categories | **Adopt** keys only in the document; text from the dialogue engine's locale tables (CLDR plural and ordinal categories, fallback chains) |
| CLDR: format vs stand-alone contexts, widths, skeletons | **Avoid.** A format is one locale message with placeholders; a translator writes the word order |
| Fix Your Timestep: accumulator with carried remainder, step cap | **Adopt** (exact rational scale, remainder carried; one step crosses at most 400 days, the rest is a backlog) |
| Godot: physics loop owns the tick | **Adopt.** The calendar has no clock; the component's `Update` is the only place time moves |
| Implicit defaults (no block means idle, a missing fact is false) | **Avoid.** A missing fact a routine reads is a refusal (`CAL_FACT_MISSING`); a missing field is a refusal |

## 4. Layer boundary

- `CalendarEngine.Core` (pure library): document format 1, strict loader, `CalendarSystem` (dates), `Recurrence`, `CalendarSet` (seasons, daylight, moons, holidays), `CalendarContext` (facts and schedule answers), `CalendarRuntime` (instant, scale, backlog, actors, interrupts), save/open, `CalendarNames`, `CalendarExplain`. No clock, no files, no Godot, no world.
- `GameRuntime.Calendar` (game layer): requests as events, answers in `Update`, one clock, channels, checkpoint, journal and replay.
- `GameRuntime.Calendar.Ai`: the time as an AI percept. `CalendarEngine.Sim`: the SimEngine `ICalendar`. `GameRuntime.Calendar.Headless`: the demo.
- The host (Godot or headless) renders: it gets ids and keys, and asks `CalendarNames.Format` for text in the player's locale.

## 5. The contract later engines depend on

These are the promises the next engines may rely on and must not work around.

1. **One time coordinate: the instant.** A `long` count of calendar ticks since the first moment of the epoch year (`CalendarSystem.MinInstant` to `MaxInstant`, years -100,000 to 100,000). Everything that must remember "when" stores an instant (or a day number, `instant / TicksPerDay`) and nothing else. No engine reads or stores `DateTime`, and no engine keeps its own tick counter for calendar purposes.
2. **Time moves in one place.** Only `CalendarComponent.Update` advances the instant (`Step`) and `SkipTo` for an explicit skip request. Engines learn about it through events (below) or by reading `CalendarRuntime.Instant` from the same Update.
3. **Events, in this order within an Update:** answers to requests in arrival order (`CalendarApplied` / `CalendarRefused` / `CalendarWhereAnswered`), then `CalendarTimeAdvanced`, `CalendarDayChanged`, `CalendarSeasonChanged` (in time order), `CalendarHolidayStarted` (in time order, at most 64 per step, the rest counted in `HolidaysOmitted`), `CalendarDayPartChanged`, then `CalendarScheduleBlockChanged`. All fields are flat primitives; ids, never text.
4. **Day-level work keys off `CalendarDayChanged.DayNumber` and `DaysCrossed`.** A step may cross up to 400 days (a skip or a fast scale), so a consumer that does work "per day" (Relationship decay, Economy interest, WorldEvents rolls) must loop over `DaysCrossed` or, better, compute from the day number difference. `DaysCrossed == 1` is the ordinary case.
5. **Dates in documents are data.** A later engine's document that says "every third market day" or "on the first Monday of harvest" writes a `Recurrence` rule (`freq`, `interval`, `bymonth`, `bymonthday`, `byday`, `byseason`, `start`, `until`) and asks `CalendarSet` with `Recurrence.Matches` / `NextOccurrence`. It does not write its own date arithmetic.
6. **Facts are the read interface.** `CalendarFacts` (`cal.year`, `cal.month`, `cal.weekday`, `cal.season`, `cal.daypart`, `cal.hour`, `cal.minute_of_day`, `cal.is_day`, `cal.light`, `cal.is_holiday`, `cal.holiday_id`, `cal.is_leap_year`, `cal.moon.<id>`, ...) are in the shared fact model with declared types and ranges. Dialogue documents declare them as external facts; AI behaviours receive them as a percept; later engines read them in conditions of the shared condition grammar.
7. **Requests are events with a channel.** A later engine that needs the calendar to do something (`CalendarSkipRequested`, `CalendarInterruptRequested`, `CalendarWhereRequested`) emits the request; it is answered in the calendar's next Update, or refused with a code. An engine never calls the runtime directly during a tick.
8. **Determinism.** Same document, same start, same requests and facts at the same ticks give the same events and the same state digest. The journal (`CalendarJournal`) and `CalendarReplay` prove it per run.
9. **Saves are the engine's.** A later engine's save never copies the calendar's state; it stores instants and asks the calendar. The calendar's own save is digest-checked and bound to its document.
10. **What a later engine must add itself:** nothing about dates. If it needs a date feature the calendar lacks (ISO week numbers, a lunar month rule, business days, a countdown that skips holidays), it is added here once, not beside the engine.

Per engine, the first use I expect: **Relationship** (memory age and decay in days; birthdays and anniversaries as recurrence rules; "is the actor free" via `WhereIs`), **Economy** (market days, seasonal price curves from `cal.season`, wages and interest on `DayChanged` or a monthly rule, shop hours from schedules), **Tutorial/Onboarding** (time-gated hints from facts, "wait until morning" as a skip request), **WorldEvents** (season and holiday triggers from `SeasonChanged` / `HolidayStarted`, `NextOccurrence` for forecast), **Diplomacy** (treaty terms as instants and `AddYears`/`AddMonths`, eras for dating a chronicle). These are expectations, not code.

## 6. Document format 1 (summary)

One strict JSON document: `format, id, time, epoch, weekMode, weekdays, months, leap, eras, dayParts, seasons, twilightMinutes, moons, holidays, formats, locations, activities, interrupts, facts, schedules`. Every field is present (arrays may be empty where the bounds say so); an unknown, missing or repeated field, a number out of range, a bad id, a bad clock, a bad rule, a reference that does not resolve are each refused with a distinct code and a message naming the place. Ids are `a-z 0-9 - . _`.

- `time`: hours per day (1..48), minutes per hour (1..120), ticks per minute (1..1000); a day of at most 1,000,000 ticks.
- `epoch`: the year of instant 0 and, when weeks run on, the weekday of its first day.
- `weekMode`: `continuous` (the weekday count runs through the year, intercalary days included, as in the Gregorian calendar) or `restart` (day 1 of each month is the first weekday; intercalary months have no weekday).
- `months`: id, key, common days, leap days, intercalary. A month of 0 days and some leap days exists only in leap years.
- `leap`: rules `{every, offset, leap}`; the last matching rule decides; the pattern repeats every lcm (at most 100,000 years), so leap years before a year are one lookup. Leap days without a rule that ever makes a year leap, and rules without leap days, are refused.
- `eras`: `{from, direction}` up or down (BCE counts down to the next era's start); the first era starts at or before -100,000.
- `dayParts`: from a clock time, the first at 0:00. `seasons`: start on a month and day of a common year (wrapping over the year end), with sunrise and sunset; `twilightMinutes` ramps light at dawn and dusk.
- `moons`: an exact fractional period and new moon (in days from the epoch), eight phase keys; phases by integer arithmetic on 128-bit integers.
- `holidays`: id, key, days (1..31), tags, a recurrence rule. `formats`: keys of locale messages.
- `locations`, `activities`, `interrupts` (a priority, a location, an activity), `facts` the host supplies (`bool`, `int` with a range, `string`; the prefix `cal.` is reserved).
- `schedules`: routines with a distinct priority and an optional `when` (weekdays, months, seasons, holidays, a date rule, a condition) and blocks (`[from, to)` in clock times, `24:00`-style end allowed). Exactly one routine has no `when`: the base, the lowest priority, covering every minute. Blocks are in time order and do not overlap.

Samples (all loaded and tested): `src/CalendarEngine/samples/gregorian.calendar.json` (the proleptic Gregorian calendar with US-style holidays, a moon, shopkeeper and guard schedules), `valley.calendar.json` (4 x 28, restart weeks), `realms.calendar.json` (ten-day weeks, five festival days, Shieldmeet).

## 7. Localisation and the dialogue dependency

A document holds keys only. `CalendarNames.RequiredKeys` lists every key; `CalendarNames.Coverage` reports missing keys, messages no document uses, and placeholders a key does not give (names give none; a format gives `day, day_of_year, month, month_number, year, year_of_era, era, weekday, hour, minute, hour_padded, minute_padded, daypart, season, holiday`). Rendering goes through `DialogueEngine`'s `LocaleTable`, `LocaleChain`, `LocaleRenderer` and CLDR plural rules, so an ordinal day is an ordinal-select message (`1st`, `2nd`, `3rd`, `11th`) and a missing key falls back down the chain and is remembered. Locale refusals keep the dialogue engine's `DLG_LOCALE_*` codes.

**Pass 35 update:** the locale tables, chains, CLDR plural rules, message rendering and coverage moved to `EngineCommon.Core/Locale` (the namespace is `EngineCommon`; the argument type `LineArg` is now `MessageArg`; the codes are `LocaleCodes`, with the same `DLG_LOCALE_*` texts), so `CalendarEngine.Core` references `EngineCommon` only, and coverage is the one rule `CoverageRules` that dialogue and calendar both call. No document digest or save changed. A format renders every name it can use, so formatting needs a locale chain that has every name key of the document (coverage is how that is checked); a partial table is refused with `DLG_LOCALE_MISSING_KEY` rather than printing a gap.

## 8. Acceptance criteria (fixed before coding)

| # | Criterion | Check |
|---|---|---|
| C1 | Dates: instant to date and back is exact for the proleptic Gregorian calendar: every day of years 1 to 9999 agrees with an independent implementation (weekday and day of year too); leap rules, negative years and the first and last supported instants are exact; a date that is not in the calendar is refused, never rolled over | Test over 3,652,059 days against `DateOnly`; boundary tests |
| C2 | Fictional calendars use the same code: 4 x 28 with restarting weeks; a ten-day week with festival days outside the weeks and a leap festival; a 30-hour day with 100-minute hours; leap rules of other shapes with several leap days | Tests, including a loop oracle over 300 years |
| C3 | Arithmetic: days, months (clamped like java.time, skipping months that do not exist that year), years, differences; all in the document's units | Tests |
| C4 | Recurrence: each RFC 5545 part in the subset matches an independent computation; COUNT and BYSETPOS are refused; rules that mean nothing in the calendar (weekly interval 2 where weeks restart) are refused | Tests against `DateOnly` |
| C5 | No clock, no float, no random: the core and the game layer contain none of `DateTime`, `Stopwatch`, `Random`, `float`, `double`, `decimal`, `Math.Floor/Round/...` | Source guard, itself tested to fail on each |
| C6 | Schedules are total and deterministic: every schedule answers every minute of every day; the answer is the same whatever was asked before; priorities decide; a gap in a routine falls through to a lower one; interrupts win at equal or lower priority; a fact a routine reads is needed only where the routine could apply | Property test over random instants, order test, golden `explain` text |
| C7 | Strict loader: every document refusal code is reached by one input; a code with no test fails the matrix test | Refusal matrix over `CalendarCodes` by reflection |
| C8 | Save/load: format, document, ranges (before the digest), digest, then what the state says about the document, each with its own code; every field is required; an old format is refused; a forged value with a recomputed digest is refused as state | Tamper tests per field |
| C9 | The clock: a rational scale with the remainder carried is independent of how ticks are split; pause earns nothing; a step is bounded and the rest is a backlog; time stops at the last instant and says so; a skip is capped and refuses the past | Tests |
| C10 | Component: requests only queue and are answered in Update in arrival order; a refusal is an event; a request for a channel nobody serves is refused in the same tick; a checkpoint is refused while a request or refusal waits; announcements are capped per Update and none is lost | Tests |
| C11 | Replay: a journal of requests and host facts replayed from a checkpoint, or from the start, ends in the same state digest and raises the same events | Tests; the headless demo does it on every run |
| C12 | Integrations, one proof each: dialogue conditions read season, hour and holiday from the calendar; an AI agent acts on the time only after it is told it, as a percept; SimEngine reads its date through the calendar; a real `SimGame` is projected | Tests |
| C13 | Localisation: names and formats through locale tables with CLDR ordinal categories and fallback; coverage lists missing, unknown and misused keys | Tests; Cli `coverage` |
| C14 | Cost: 10,000 actors answered per tick in a few milliseconds with a small allocation; long-range conversion is constant time; measured with repeats | `ScaleTests`, numbers in section 10 |
| C15 | Cli: check, explain, convert, month-view, coverage; every mistake has an exit code | Tests |
| C16 | Every new test is mutation-checked; survivors are reported | Section 10 |

## 9. Game-layer design notes

- **One clock.** The session's `RuntimeClock` is the only clock. `CalendarComponent.Update(tick)` asks the runtime to `Step(tick)`: `elapsed = tick - LastTick`; the calendar ticks earned are `(elapsed x numerator + remainder) / denominator`, the remainder carried exactly. `SetScale` drops the fraction earned at the old speed (as `RuntimeClock.SetSpeed` does). The scale may be at most 366 days of calendar per game tick and the denominator at most 10,000.
- **Catch-up.** A step applies at most 400 days of calendar. A game that resumes at a much later tick (the runtime last stepped at tick 0, the session starts at tick 1,000,000) gets the first 400 days in its first Update and the rest, as a backlog held in the saved state, in the following ones. A single jump of more than 50,000,000 ticks is refused each Update (`CAL_TICK_ORDER`, request id `clock`).
- **Channels.** Same design as `DialogueChannels` (pass 32 and 33): `CalendarChannels.Install(session)` declares the events and adds the registry as the last component; every component takes a registry; an unserved request is refused with `CAL_CHANNEL_UNKNOWN` in the same tick, a second component on a channel with `CAL_CHANNEL_DUPLICATE` at Init. **Pass 35 update:** the registry is no longer a copy. `GameRuntime.Channels/ChannelRegistry` is the one registry; `CalendarChannels` and `DialogueChannels` are sealed subclasses that name their events, their request port, their codes and a *rank* (dialogue 0, calendar 1, relationship 2: a registry updates at `int.MaxValue - rank`, so several share a session and each is after every ordinary component).
- **Announcements.** A schedule change is announced when the minute changes, the host facts change, a request changed the actors, or announcements are still owed. At most `maxAnnouncements` (default 1,000) are raised per Update, in actor id order; the answer last announced is part of the saved state, so a restore announces nobody twice. A fact a routine needs and the host did not give is refused once (`CAL_FACT_MISSING`, request id `announce`) until the facts or the minute change.
- **Journal.** The component writes each request, refused ones too, with the tick it was applied at, and the host facts when they change. Replay submits the requests before their tick and serves the facts from the journal. The replay session feeds whole microseconds per tick, so the tick rate must divide 1,000,000 (50 and 100 do; 60 does not).
- **What a checkpoint omits:** the registry (fixed by assembly) and the component's memory of the last minute and facts it announced for (the first Update after a restore looks at every actor again, which announces nothing that was announced).

## 10. Result

**Built (2026-10-28), nothing committed or pushed, no Actions run.** Projects, all in `GameFrameWork.slnx`:

| Project | What it is |
|---|---|
| `CalendarEngine.Core` (35 refusal codes, about 1,950 lines) | Format 1 and strict loader; `CalendarSystem`, `LeapRules`, `Recurrence`, `CalendarSet`, `CalendarContext`, `CalendarFacts`, `CalendarRuntime`, `CalendarSave`, `CalendarNames`, `CalendarExplain` |
| `CalendarEngine.Cli` | `check`, `explain`, `convert`, `month-view`, `coverage` |
| `CalendarEngine.Sim` | `CalendarSimCalendar : ICalendar` for SimEngine |
| `GameRuntime.Calendar` | `CalendarComponent`, `CalendarChannels`, events, `CalendarCheckpoint`, `CalendarJournal`, `CalendarReplay`, `CalendarTranscript` |
| `GameRuntime.Calendar.Ai` | `CalendarAiBridge`: the time as a percept |
| `GameRuntime.Calendar.Headless` | the demo |
| `CalendarEngine.Tests` (192), `GameRuntime.Calendar.Tests` (87) | the tests |
| `src/CalendarEngine/samples` | `gregorian`, `valley`, `realms` documents and English tables |

### 10.1 Criteria

| # | Result | Evidence |
|---|---|---|
| C1 | Met | `DateTests.EveryDayOfYears1To9999AgreesWithDateOnly` (3,652,059 days: year, month, day, weekday, day of year, and the way back); leap years, year 0 and negative years, first and last instants, refused dates |
| C2 | Met | `FictionalCalendarTests`, `SeveralLeapDaysTests` (a 5-day week, four months, three leap months, three nested leap rules, a 10-hour day of 10-minute hours: every day of 300 years against a loop) |
| C3 | Met | `DateTests.ArithmeticOn...`, `FictionalCalendarTests.MonthArithmeticSkips...` |
| C4 | Met for what was built | `RecurrenceTests`, `EdgeTests`: every part against `DateOnly`; COUNT and BYSETPOS refused as unknown fields; weekly with interval 2 and ordinal weekday in a year refused where weeks restart. The RFC text beyond the excerpt I read was not checked (section 2) |
| C5 | Met | `SourceGuardTests` (Core), `RuntimeSourceGuardTests` (component, bridge); each guard is tested against snippets that break it |
| C6 | Met | `ScheduleTests` (6,000 random instants x 2 weather x 2 schedules; reversed-order test; golden explain text), `EdgeTests` (ties), `SelectorAndFormatTests` (months, date rules, several fields) |
| C7 | Met | `RefusalMatrixTests`: one input per code, and the set of codes equals the constants of `CalendarCodes` by reflection |
| C8 | Met | `SaveTests`: 12 tampers refused as digest, 18 forged values refused as state with a recomputed digest, every required field, a range error reported before a digest error |
| C9 | Met | `RuntimeTests`: 40 random splits of 1,000 ticks end identical at scale 7/3; pause; backlog paid in 10 steps; end of time; skips |
| C10 | Met | `CalendarComponentTests`, `OwedAnnouncementsTests`, `SkipBoundaryTests` |
| C11 | Met | `CalendarReplayTests`; the demo replays 100,800 of its 172,800 ticks from a checkpoint on every run |
| C12 | Met, with one limit (pass 35 closed the other) | `IntegrationTests`: dialogue (a calendar component and the real `DialogueComponent` on one session, the dialogue component reading `calendar.Facts("ext.")` when it answers the start request and the line arriving as `DialogueLineShown`), AI (agent lights a lamp only after it is told the sun has set; unknown agent refused by the AI component), SimEngine (fake time with every alignment case, and a real `SimGame` of the quickstart content) |
| C13 | Met | `NamesTests`, `CliTests.CoverageSays...` |
| C14 | Met | 10.3 |
| C15 | Met | `CliTests` (11 tests, every exit code) |
| C16 | Met in part | 10.4 |

### 10.2 What was decided in the work, with the source followed

- A schedule is total: the base routine covers the whole day and every priority is distinct (Stardew's "no matching key means no schedule" is avoided; its "highest wins" is kept, without a tie-break nobody wrote down).
- An interrupt wins over a routine of **equal or lower** priority: a rule of this engine (no source for it; the Sims and Stardew pages I could reach do not say). The tie was a decision and has a test.
- The week can run on or restart each month; an intercalary month has no weekday in either mode.
- Moons use 128-bit integer arithmetic and eight equal phases centred on the new moon; the real phases (January 2000) are checked within one phase width.
- Time beyond the last instant (year 100,000) is dropped and the step that hits the end says so (`Clamped`).

### 10.3 Cost (measured on this machine, 2026-10-28; `perf.txt`, median, fastest and slowest of the repeats)

Two sets of runs. The first was taken with nothing else running; the second while other builds and the full suites ran, and is 1.5 to 2.5 times slower. Both are given.

| Question | Quiet (median, min, max) | Under load (median, min, max) |
|---|---|---|
| 10,000 actors `WhereIs` in one context | 6.6 ms (3.5 to 12.8) | 12.8 ms (6.0 to 29.2) |
| 10,000 actors `Refresh`, nothing changed, context included | 7.9 ms (4.5 to 18.3) | 15.7 ms (5.8 to 58.8) |
| 10,000 actors `Refresh` at a block change (7,500 announced) | 37 ms (19 to 55) | 71 ms (43 to 124) |
| Allocation per `WhereIs` in a context | 104 bytes | 104 bytes |
| 100,000 instant to moment over 200,000 years | 80 ms (74 to 85), 0.8 microseconds each | 133 ms (113 to 193) |
| the same near the epoch | 77 ms | 125 ms |
| 100,000 date to instant over 200,000 years | 52 ms | 78 ms |
| 10,000 `CalendarFacts.Read` | 54 ms (5 microseconds each) | 96 ms |
| 10,000 `HolidaysOn` (6 holidays) | 29 ms | 48 ms |
| 100,000 `Step` of one tick | 47 ms | 81 ms |
| 365 one-day skips with season and holiday scan | 1.4 ms | 1.4 ms |
| Save of 10,000 actors (1.31 MB): write, open | 68 ms, 89 ms | 88 ms, 160 ms |

Far and near conversions cost the same, so conversion is constant time. The component's own per-tick cost (it builds the host-fact text once per Update to notice a change) was not measured separately; the headless demo runs 172,800 ticks, 5 villagers and 176,825 events, and replays 100,800 of them, in about 9 seconds of wall time.

### 10.4 Mutation checking

Mutants are single-comparison or single-expression changes applied by a script to the source, built, and run against the suite (a script outside the tree; mutants were restored after each). Two rounds, 119 mutants across the core, the component, the bridge, the journal, the Cli and the SimEngine adapter.

- Round 1, 80 mutants: 66 killed at once, 14 survived. 13 of the 14 were killed after adding tests (monthly and weekly rules with an interval of 3, because parity hid the sign mutants; the monthly default day; a window end; interrupt ties and the end of an interrupt; clock, season-order and block bounds; owed announcements while the minute does not change; an entry at the checkpoint tick; a late perceiver). The 14th, `while (YearStart(year) >= dayNumber) year--;` for `>`, is **equivalent**: the loop below it corrects the year, so the result cannot differ.
- Round 2, 39 mutants (Cli, journal, names, reader bounds, selectors, component): 29 killed, 9 survived, 1 did not compile. After tests (a `--fact x=true` explain, extra parts in a journal line, entries of another channel, equal day-part starts, the twilight at both ends of the day, a season on the last day of its month, equal era starts) 8 of the 9 were killed. The 9th, a skip request with hour equal to the hours of a day, is **equivalent**: the instant conversion behind it refuses the same code. The mutant that did not compile was replaced by one that does and was killed.
- Two mutants did not compile at first (an unreachable-code error; an unused field) and were redone with a compilable form.
- Final: 117 killed, 2 equivalent survivors, none open.

Not covered by any mutant, so their tests are not shown to fail: the headless program's divergence exit (3), the 64 MB save size bound, the `NextHoliday` helper's argument bound beyond the one tested, `CalendarExplain`'s wording outside the two golden cases, and most of `CalendarAiBridge`'s construction checks beyond the five shapes tested. Every test was run and passes; this is not the same as a mutant for each.

### 10.5 Suites

`dotnet build GameFrameWork.slnx`: 0 warnings, 0 errors. CalendarEngine 192, GameRuntime.Calendar 87, AIEngine 209, CombatEngine 139, GameContent 356, GameRuntime 553: all passing. DialogueEngine (1,200, not part of this pass and being changed by another): 1,199 pass; `CostTests.AHundredThousandLineStepsThroughATwoHundredNodeDocumentCostUnderASecondAndTwoKilobytesPerStep` failed in the run that followed the other suites on a loaded machine; it is a time bound and I did not touch the dialogue projects. The headless demo, default 120 days: `output-sha256 22848928B5F01E01B08124B7EE522CF42424C2E5C32A06B5223AD3819808A57C`, twice the same, replay "state same, events same".

### 10.6 Not done, and why

- **ISO 8601 week dates and week-of-year numbering**, and **real non-Gregorian calendars** (lunar months, Hebrew, Islamic): not built; the format expresses fictional calendars, and real ones with computed months need a rule type the format does not have. **Time zones** and leap seconds: not applicable to a world calendar.
- **RFC 5545 COUNT and BYSETPOS**: refused; they need enumeration from the start.
- **Daylight between seasons**: it steps at the season start; there is no interpolation or latitude.
- **SimEngine content registration**: `CalendarSimCalendar` implements `ICalendar` and is used by the host directly. SimEngine reads a time definition's calendar by name through its content resolver, which needs a `CalendarDef` subclass registered in a content module; not done.
- **CombatEngine**: it has no time notion (terrain is an id in the order), so no bridge was built. A game maps `cal.season`, `cal.is_day` and `DaylightAt` to a terrain id itself.
- **Godot adapter**: none in this pass.
- ~~The locale code lives in DialogueEngine and the channel registry is a copy~~ **Resolved in pass 35** (sections 7 and 9): `EngineCommon.Core/Locale` and `GameRuntime.Channels`; `EngineCommon.Tests` fails if a second locale implementation or a second registry appears.
- **The journal grows without bound** while it is on; it is a test and QA tool, off in a shipped game.
- **Replay needs a tick rate that divides 1,000,000.**
- ~~The dialogue proof used `DialogueRuntime`, not the component~~ **Resolved in pass 35**: it uses the real `DialogueComponent`.
- **Pass 35 also changed the calendar's guards**: the source guards are the shared Roslyn ones of `EngineCommon.TestSupport` (stricter than the textual ones: they found two `var other => throw` arms in the readers, now closed-set lookups, and one conditional catch, now a `Report` method). The calendar tests are now 206 and `GameRuntime.Calendar.Tests` 86.
