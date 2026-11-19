# RelationshipEngine: research, boundary, document, criteria and result

Pass 35, 2026-10-28. The second of six planned engines (Calendar done in pass 34; Relationship, Economy, Tutorial/Onboarding, WorldEvents, Diplomacy follow). Written before the code; section 10 records what was built and measured.

**Pass 43 (2026-10-29): document and save format 2, derived social state (communities, standing, balance pressure) and opinions as AI percepts are in section 11. Where sections 3, 6 and 10.7 say format 1, "no reputation number" or "the AI bridge tells each agent only about itself", section 11 supersedes them; the village demo hashes of 10.5 are replaced by the ones in 11.5.**

**Pass 47 (2026-10-30) supersedes the format numbers of pass 43: the document is format 3 (`RelationshipLoader.Format`), the save stays format 2 (`RelationshipSave.Format`); a format 2 document is refused with `REL_FORMAT_2`, a format 1 document with `REL_FORMAT_OLD`, any other number with `REL_FORMAT`. Section 13 has the reason, the 41 declared codes and the current village demo hashes (60 days and `--days 20 --checkpoint-day 7`), which replace those of 11.5. Where sections 3, 6 and 11.2 say "format 1" or "format 2" for the document, read 3.**

## 1. What already owns this (evidence, and the decision)

Read in the source on 2026-10-28:

| Owner | What it holds | Decision |
|---|---|---|
| `EngineCommon` | strict JSON reading, the one condition grammar (ScenarioEngine's), digest, stateless seeded roll (`DeterministicRoll`), and since pass 35 the locale machinery (tables, chains, CLDR plurals, coverage) | **Reused.** Documents are strict JSON; conditions on facts use the shared grammar; randomness is `DeterministicRoll`; words are locale keys |
| `CalendarEngine` | The one coordinate of time (an instant), days, seasons, schedules with activities and locations, facts `cal.*` | **Reused through its contract** (`docs/CALENDAR_ENGINE.md` section 5): the relationship core keeps no clock; the game layer converts the calendar instant to a step number; activities of schedule blocks satisfy needs |
| `DialogueEngine` | Conversations; external facts (`ext.*`); signals and quest outcomes | Reads relationship facts as external facts; writes back by raising a relationship request (deferred) |
| `AIEngine` | Percepts, utility, behaviour trees; knowledge is not truth | Needs and opinions arrive as percepts through a bridge; an agent chooses by need urgency |
| SimEngine characters (`CharacterComponent`, `ICharacter`, `CharacterTraitDef`) and `GameContent.Catalog.Characters` (`TraitWeight`) | A character's name, stats, skills and **trait ids**, drawn by the factory; the trait vocabulary has an id and a label key | **Reused, not duplicated.** The relationship document says what a trait id *does* to moods, needs and opinions; it does not define traits. A bridge reads `ICharacter.Traits` and refuses a document that names a trait the game does not have |
| nobody | Needs with decay and satisfaction, mood as a sum of timed modifiers, per-pair opinions with memories, named relationship states, social interactions with preconditions, gossip | **The relationship engine** |

The engine is a pure library over a step counter. It has no clock, no files, no world and no float. The game layer's clock stays the only clock.

## 2. Research (searched 2026-10-28)

P means the page itself was read; S means a search summary or snippet only; N means not reached. Where a page failed, the status is said.

- **RimWorld wiki, "Mood".** P. Mood target is a base (42 Peaceful down to 22 Losing is Fun by difficulty; the same page also says 32, unresolved) plus the sum of thoughts (example: +18 and -6 give a target of 54 percent). The bar **moves toward the target** at most +12 per hour up and -8 per hour down. Mental break lines at 5, 20 and 35 percent. The page does not give stacking, durations or expiry numbers and gives no numbers for trait effects. Not read: the "Thoughts" page.
- **Stardew Valley.** The wiki page "Friendship" returned HTTP 403. S (search summary of the wiki): 250 points per heart, cap 3,749 (just under 15 hearts); gift taste values Love +80, Like +45, Neutral +20, Dislike -20, Hate -40, with Birthday x8 and Winter Star x5; one gift a day and two a week, plus 10 points on Sunday for two gifts; talking decay is daily (-2 a day for non-romanceable villagers until 10 hearts, -20 a day for a spouse, never stopping); a spouse's gains and losses are reduced (the sources disagree: half, or 34 percent). Wikipedia: only that gifts and interaction build relationships over time and that one may marry one of 12 characters.
- **Crusader Kings 3 wiki, "Opinion".** P, first 100,000 of 129,529 characters. Opinion is a list of modifiers; some grow, some stay, "most decay to 0"; fixed durations such as -25 for 3 years (Attacked An Ally) and -50 for 3 years (Broke Truce); trait-based modifiers (from -126 to +92); modifiers depend on the *holder's* traits and faith, and the reverse opinion is a separate number; attraction and child opinion apply only to some ages. **Not on the page:** stacking rules, a numeric opinion range, decay rates.
- **Dwarf Fortress wiki, "Thoughts and preferences" / "Stress".** The thoughts page returned HTTP 429. S: positive thoughts lower stress and negative raise it; memories are thoughts about the past; short-term stress follows mood and long-term stress is a slow accumulation of it (at most +20,160 a year and -43,564 a year under ideal conditions); unmet needs mostly lower productivity (a forum opinion). Not found: how long a thought lasts.
- **The Sims wiki and fan guides.** S. Default decay times in hours (fan guide, approximate): hunger 4-6, bladder 8-12, energy 14-20, fun 7-10, social 8-15, hygiene 24-36; a motive decay multiplier (100 normal, up to 2x) that traits and buffs also scale; moodlets carry durations (a full bladder's lasts 3 hours, hunger's 24); autonomy picks actions to fill needs. No primary EA document reached.
- **Prom Week / Comme il Faut.** S from the project blog: a *social exchange* pairs an action with the other character's response; each character holds a ranked list of exchanges it wants to perform (over 40) and decides how to respond, from over 5,000 social considerations (rules over traits and history). The Wikipedia page returned 404; the McCoy et al. papers were not read.
- **Versu (Evans and Short, 2014).** S: *social practices* are reactive joint plans that give the participants affordances and only suggest; separate, composable practices; "exclusion logic" in the index terms. The paper itself (PDF) was not read, so I use nothing about its logic.
- **Maslow, needs models in games.** S: Age of Conan's NPC AI sits on a "Maslovian pyramid" where basic needs gate higher behaviour; a student post found a rigid pyramid hard to represent as a game system and suggested using it as a model. I use needs as independent levels with urgency curves, not a strict hierarchy.
- **Ultima (NPC schedules and memory), decay/half-life as a game model.** N. A search for Ultima's NPC systems found nothing usable. Half-life decay is taken from mathematics, not from a game source: the method in section 5.3 is mine.
- **Not reached at all:** the RimWorld "Thoughts" and "Traits" pages, the Stardew pages, the Dwarf Fortress thoughts page, Sims primary documents, CiF and Versu papers, any psychology source for needs.

## 3. Adopt, adapt, avoid

| Source idea | Decision |
|---|---|
| RimWorld: mood = base + sum of thoughts; a bar that moves toward a target at different up and down speeds | **Adopt** the sum. **Adapt** the bar: the mood *value* is the exact sum (so it explains), the named mood *state* has hysteresis (a margin that must be crossed to leave a state), which gives the same "does not flicker" result without a second moving number. **Avoid** unexplained constants: every constant is in the document |
| Sims: needs decay, activities fill them, decay multiplied by traits; autonomy follows urgency | **Adopt.** Needs lose per-day amounts, scaled by traits and by host facts (winter); an activity (a schedule block's activity) fills them; **urgency** is a document curve the AI reads. **Avoid** the Sims' implicit "everything is a need": needs are a short list the document declares |
| Sims moodlets / RimWorld thoughts with durations and stack limits | **Adopt** timed mood modifiers with `refresh`, `stack` (limit), `replace`, `unique` stacking and three decay shapes |
| CK3: opinion is a list of dated modifiers that mostly decay to 0, with fixed durations; trait-based modifiers on the holder | **Adopt** memories (opinion modifiers) on an edge with a duration and a decay; **adopt** that traits belong to the holder; opinion = base + sum of memories, and `explain` lists the modifiers. **Avoid** CK3's missing stack/range rules: every bound is stated and tested |
| Stardew: friendship points in hearts, daily decay, gift taste multipliers, per-week gift limits, spouse changes halved | **Adapt.** An axis has a baseline and a per-day drift toward it; gifts have tags and the receiver has tastes (hate to love) that scale effects; a per-pair **cooldown** replaces "one gift a day"; a bond can scale effects. **Avoid** hearts as a unit: axes are plain integers |
| Prom Week / CiF: exchanges with initiator desire and responder acceptance from weighted rules | **Adapt.** An interaction has `desire` rules (weights over facts, so the AI can rank them), `accept` rules with a threshold and a seeded noise, and separate effects for acceptance and refusal. **Avoid** thousands of rules: rules are the shared condition grammar over a small fact set |
| Versu: social practices as composable affordances | **Not adopted.** A practice is a joint plan with state; here an interaction is one exchange. Preconditions (facts) give the affordance idea in the small. Said plainly: this is not a practice system |
| DF: thoughts and memories feed a slowly accumulating stress | **Not adopted.** One mood value and its state; long-term stress is a possible later module |
| Maslow | **Not adopted** as a hierarchy; needs are independent levels |
| Gossip and reputation (social games generally) | **Adapt.** A memory can *spread*: a bounded daily telling to the teller's most trusted neighbours, seeded chance, scaled and with a hop limit, each instance told once. Reputation is what others hold (their edges toward the subject), not a separate number |
| Hard-coded needs, thresholds and axes | **Avoid.** All are document data; the engine has no names of its own |
| Floats for decay | **Avoid.** Integer arithmetic only; section 5.3 |

## 4. Layer boundary

- `RelationshipEngine.Core` (pure library): document format 1, strict loader, `RelationshipSet`, `RelationshipRuntime` (actors, needs, moodlets, edges, memories, bonds, ladders, interactions, gossip, bounds), facts, explanations, save/open, locale keys and coverage. No clock, no files, no Godot, no world.
- `GameRuntime.Relationship` (game layer): `RelationshipComponent` with Init and Update only, requests as events, answers in `Update`, its channel registry (`ChannelRegistry`, rank 2), deferred events, checkpoint, journal and replay.
- `GameRuntime.Relationship.Bridges`: dialogue facts and outcomes, AI percepts and utility inputs, calendar (time and schedule activities), SimEngine characters.
- `GameRuntime.Relationship.Headless`: the village demo. `RelationshipEngine.Cli`: check, explain, simulate, coverage.

## 5. The model

### 5.1 Time

The document says `stepsPerDay` (1 to 288). One **step** is the smallest time the engine moves in; every duration is a number of steps; a day is `stepsPerDay` steps; the **day boundary** is the step where `step / stepsPerDay` changes. The game layer converts a calendar instant to a step with `step = instant x stepsPerDay / ticksPerDay` (exact, floor), and the component refuses a document whose `stepsPerDay` does not divide the calendar's ticks per day. The engine moves at most 480 steps per `Advance`; what is owed is the caller's to ask for again (the component does it in its next Update and says how many steps are owed). Nothing reads a clock.

### 5.2 State

- **Actor:** id, trait ids (sorted), tastes (gift tag to -2..2), activity (an id), need levels, mood modifiers (moodlets), mood state, outgoing edges.
- **Need:** a level 0..max stored times `stepsPerDay` so a per-day rate applies every step with no remainder. Each step: level loses `decayPerDay` scaled by the actor's traits and by the **decay modifiers** of the document (a condition over host facts such as `cal.season`, evaluated once per step for everyone), and gains the `perDay` of every source whose activity the actor is doing. A need is *critical* at or below `critical` and re-armed at `rearm` (hysteresis). **Urgency** is a piecewise-linear curve from level to 0..1000.
- **Mood:** `base` + the actor's trait bias + for each declared need `(level - neutral) x weight / max` + the current value of every moodlet; clamped; mapped to a named **mood state** by an ascending ladder with a hysteresis margin.
- **Moodlet:** a document definition (magnitude, duration, decay, stacking, tags) with instances (start step, scale in permille, source actor). Traits scale a moodlet by its tags.
- **Edge:** the opinion of actor A about actor B (directed). A *base* value per axis (changed by interaction effects, drifting toward the axis baseline each day), a list of **memories**, a state per **ladder**, **bonds**, **cooldowns**, and when it was last touched. The *opinion* on an axis is the clamped base plus the current value of the memories that name the axis.
- **Ladders:** per axis, ascending steps with `from`; moving up needs `opinion >= from(next)`, moving down needs `opinion < from(current) - hysteresis`. A **bond** is a binary relation (spouse, rival-sworn) set and cleared by interaction effects and optionally broken when an axis falls below a value.
- **Interaction:** id, tags, `requires` (a condition), cooldown per pair, `gift` (needs a gift item), `desire` rules, `accept` rules with `acceptAt` and `noise`, `effects` on acceptance, `refusedEffects` on refusal, and an optional **witness** rule. Effects: axis delta (either direction), memory (either edge), moodlet, need delta, bond set or clear; an effect may be `byTaste`.

### 5.3 Decay without floats

A modifier's value at age `a` steps is a pure function of its magnitude `m` and its definition, so there is no accumulated rounding and no dependence on how often it is read. `fixed`: `m` while `a < duration`. `linear`: `m x (duration - a) / duration`. `halfLife` of `H` steps: with `k = a / H` and `r = a mod H`, the value is `lo - (lo - hi) x r / H` where `lo = m / 2^k` and `hi = m / 2^(k+1)` (truncating toward zero) - a piecewise-linear approximation of `m x 2^(-a/H)`, exact at every whole half-life and at most 6.15 percent high between them (largest at 56 percent of a half-life) (the true curve is convex; the chord lies above it). All modifiers end at `duration`. Division truncates toward zero; the same rule everywhere.

### 5.4 Bounds and eviction

The document states `maxActors`, `maxEdgesPerActor`, `maxMemoriesPerEdge`, `maxMoodlets` (each with a hard ceiling in code). When a limit is reached the engine evicts deterministically and **says so**: a moodlet or memory with the smallest absolute current value (then the oldest start, then the id); an edge with the least significance (no bond first, then the smallest sum of absolute base values plus memories, then the least recently touched, then the id). Eviction is reported in the result and by an event; a request is never refused merely because something old had to go. An edge that holds nothing (all axes at baseline, no memory, no bond) is forgotten at the day boundary.

### 5.5 Randomness

Only `DeterministicRoll` with the document's seed, a named stream and a counter that is part of the saved state: interaction noise (`"noise"`), gossip chance (`"gossip"`). The same requests in the same order give the same results, and the counter makes a saved game continue the same sequence.

### 5.6 Facts

Preconditions, rules and decay modifiers read facts of the shared model (boolean, integer, string) with declared types and ranges: `actor.mood`, `actor.mood_state`, `actor.activity`, `actor.need.<id>`, `actor.trait.<id>`, the same under `target.`, `edge.<axis>` and `reverse.<axis>` (opinions, A about B and B about A), `edge.state.<ladder>`, `edge.bond.<id>`, `edge.memory.<id>` (how many), `edge.known`, and the document's own host facts (any other id, for example `cal.season`). A condition is checked at load against these declarations; a fact it needs is built for it and nothing else.

## 6. Document format 1 (summary)

One strict JSON object, every field present (arrays may be empty where the bounds say so): `format, id, seed, time, limits, axes, needs, sources, mood, moodlets, memories, traits, ladders, bonds, gifts, tastes, gossip, interactions, facts`. An unknown, missing or repeated field, a number out of range, a bad id, a reference that does not resolve, a bad condition, an unordered ladder, a modifier with no end are each refused with a distinct code and a message naming the place. The long form is in `src/RelationshipEngine/samples/village.relationship.json`, which the tests load.

## 7. Localisation

A document holds keys only. `RelationshipNames.RequiredKeys` lists every key; `Coverage` reports missing keys, messages no document uses, and placeholders a key does not give, through the shared `CoverageRules`; rendering uses the shared `LocaleChain` (CLDR plural categories, fallback chain). Locale refusals keep the `DLG_LOCALE_*` codes of `LocaleCodes`.

## 8. Acceptance criteria (fixed before coding)

| # | Criterion | Check |
|---|---|---|
| R1 | Strict loader: every document refusal code is reached by one input; a code with no test fails the matrix test | Refusal matrix over `RelationshipCodes` by reflection |
| R2 | Decay is exact and order independent: the three shapes equal an independent computation at every age of every parameter set in a sweep; the value at a whole half-life is exactly `m / 2^k`; it is zero from the duration on; reading in any order or any number of times gives the same value | Sweep tests against an independent rational implementation |
| R3 | Needs: per-day decay and sources are exact over any split of steps; trait and host-fact multipliers apply; clamps at 0 and max; critical and re-arm with hysteresis fire once per crossing; urgency interpolates exactly | Tests incl. a 365-day loop oracle |
| R4 | Mood: the value is the exact sum (needs, moodlets, trait bias, base) and `explain` lists every term and adds to the value; the state changes only across the hysteresis margin; stacking modes behave as stated | Tests incl. explanation-sums-to-value property over random states |
| R5 | Memories and opinion: opinion = clamped base + memories; expiry exactly at the duration; memory caps evict the stated one and report it; a trait scales the holder's gains | Tests |
| R6 | Ladders and bonds: hysteresis both ways, no flicker around a threshold over 1,000 random walks; a bond breaks at its line; exclusive and symmetric bonds | Tests incl. property test |
| R7 | Interactions: preconditions, cooldown per pair, gifts and tastes, accept rules with noise from the seeded roll, refused effects, witnesses, every effect kind, every refusal code | Tests |
| R8 | Gossip: bounded per day, once per instance, hop-limited, seeded, only to trusted neighbours; spreading a memory to 100 listeners cannot exceed the stated bounds | Tests incl. bound property |
| R9 | Bounds: actors, edges, memories and moodlets never exceed their limits after any sequence of calls; the evicted item is the stated one | Random-sequence invariant test |
| R10 | Determinism: same document, seed and calls give the same events and the same state digest; the counter for the roll is saved | Replay tests |
| R11 | Save/load: format, document, ranges (before the digest), digest, then what the state says about the document, each with its own code; every field required; old format refused; a forged value with a recomputed digest is refused as state | Tamper tests per field |
| R12 | No clock, no float, no random source, no file access in the core and the game layer | Source guard on Roslyn syntax trees, itself shown to fail |
| R13 | Component: requests only queue and are answered in Update in arrival order; a request for a channel nobody serves is refused in the same tick; a checkpoint is refused while a request waits; events are deferred | Tests |
| R14 | Replay: a journal of requests and host facts replayed from a checkpoint or from the start ends in the same digest and the same events | Tests; the demo does it on every run |
| R15 | Integrations, one proof each: dialogue conditions read relationship facts and a dialogue outcome writes an interaction back as a deferred request; an AI agent chooses by need urgency only from what it perceives; the calendar's schedule block satisfies a need; characters from the SimEngine factory are the actor source and their trait ids are checked | Tests |
| R16 | Localisation: keys, coverage, ordinal and plural messages through the shared machinery | Tests; Cli `coverage` |
| R17 | Cost: 1,000 actors with 20 edges each, a budget set from measurements, with repeats | `ScaleTests`, numbers in section 10 |
| R18 | Cli: check, explain, simulate, coverage; every mistake has an exit code | Tests |
| R19 | Every new test is mutation-checked; survivors are reported | Section 10 |

## 9. Game-layer design notes

- **One clock.** The session `RuntimeClock` is the only clock. `RelationshipComponent` asks an `IRelationshipClock` for the step of this tick, and `RelationshipRuntime.Advance` moves there, at most 480 steps per Update (the rest is owed, `RelationshipStepped.Owed` says how many). Three clocks: `TickRelationshipClock(ticksPerStep)` (no calendar; a pure function of the tick), the calendar's (`RelationshipCalendar.Clock`: the step is `(calendar instant - origin) / (ticks per day / steps per day)`, read after the calendar component has moved this tick, so the relationship component is added after the calendar component), and `JournalRelationshipClock` (the steps a recorded run used). The journal records the step whenever it changes, so a replay needs no calendar.
- **Channels.** `RelationshipChannels` is a sealed subclass of the one `ChannelRegistry`, rank 2. Unserved channel: `REL_CHANNEL_UNKNOWN` in the same tick; second component on a channel: `REL_CHANNEL_DUPLICATE` at Init.
- **Requests and answers.** Eleven requests (add or remove an actor, activity, interact, grant, remember, need, axis, bond, mood query, opinion query). All only queue; each is answered in the next Update in arrival order with `RelationshipApplied` (and the changes it made), `RelationshipInteractionPerformed` (an interaction, accepted or refused by the target), an answer event (queries) or `RelationshipRefused` with the code. A refusal leaves the relationships as they were (tested by digest).
- **Deferred, bounded events.** `Emit` queues, so every event is delivered after the Update returns. The changes of the *steps* (moods, needs, ladder states, bonds, memories, moodlets, evictions) are queued as owed closures in a fixed order and at most `maxEvents` (default 2,000) are raised per Update, oldest first, none lost; `IsQuiet` is false while any are owed, and a checkpoint is refused (`REL_NOT_QUIET`) until they are raised. The changes of a request are raised with its answer.
- **Journal.** Text, one line per entry: `F|tick|facts`, `S|tick|step`, `R|tick|channel|kind|requestId|a|b|c|d|e|n|m`; escapes shared with the calendar (`JournalText`). Replay needs a tick rate that divides 1,000,000 (50 and 100 do; 60 does not). The transcript is `EventTranscript` (shared with the calendar), a hash of every event as text.
- **Calendar bridge.** `RelationshipCalendar.Check` refuses a pair of documents at assembly: steps per day must divide the calendar's ticks per day; every host fact of the relationship document must be a calendar fact (or a calendar host fact) of the same type whose range the calendar's range fits inside; every activity that satisfies a need must be an activity of the calendar. `RelationshipCalendarActivities` forwards each `CalendarScheduleBlockChanged` as an activity request (deferred); a block of an actor the relationship component does not know yet waits (`Waiting`) and is forwarded once the actor exists (found by a test: the first block was lost when both were added in the same tick).
- **Dialogue bridge.** `RelationshipDialogueFacts` gives the pair's facts as `ext.*` external facts; `Check` refuses a dialogue document whose declarations of those facts disagree in type or range. `RelationshipDialogueOutcomes` turns a signal (`signal:<name>`, text arguments fill `{0}`, `{1}`) or a quest status (`quest:<id>:<status>`) into an interact, grant or remember request on the relationship channel; the answer is one tick later (shown by test), never a call.
- **AI bridge.** `RelationshipAiBridge` posts the percept `self` (mood, mood state, `urgency_<need>`) to the agents the host names, when it changed, so an agent chooses by urgency and knows only what it was told (shown by test, including acting on stale knowledge). The AI document's percept kind is checked against the relationship document.
- **Characters bridge.** `RelationshipCharacters.ActorId` is `archetype.seed.index`; `AddActors` takes `ICharacter` (the factory's) and keeps its trait ids; `CheckTraits` refuses a relationship document with effects for a trait the game does not declare. The relationship document never defines a trait.
- **What a checkpoint omits:** the registry (fixed by assembly) and the tick (kept by the caller: `RelationshipComponent.Tick`).

## 10. Result

**Built (2026-10-29), nothing committed or pushed, no Actions run.** All in `GameFrameWork.slnx`:

| Project | What it is |
|---|---|
| `RelationshipEngine.Core` (41 refusal codes, 22 files, about 2,450 lines) | Format 3 and strict loader; `DecayMath`; `RelationshipRuntime` (actors, needs, mood, edges, memories, ladders, bonds, interactions, gossip, bounds, host effects); facts; explanations; save; locale keys |
| `RelationshipEngine.Cli` | `check`, `simulate`, `explain`, `coverage` |
| `GameRuntime.Relationship` | `RelationshipComponent`, `RelationshipChannels`, 24 events, clocks, checkpoint, journal, replay, transcript |
| `GameRuntime.Relationship.Bridges` | calendar, dialogue (facts and outcomes), AI, characters |
| `GameRuntime.Relationship.Headless` | the village demo |
| `GameRuntime.Channels` (pass 35, part A) | the one `ChannelRegistry`, `ChannelProtocol`, and the journal text and event transcript the calendar and relationship components share |
| `EngineCommon.TestSupport`, `EngineCommon.Tests` (50) | the Roslyn source rules (shared), and the tests of the shared locale and registry code and of the one-owner guards |
| `RelationshipEngine.Tests` (201), `GameRuntime.Relationship.Tests` (92), `RelationshipCharacterTests` in `GameRuntime.Tests` (6) | the tests |
| `src/RelationshipEngine/samples` | `village.relationship.json`, `village.en.json`, `village.scenario.json` |

### 10.1 Criteria

| # | Result | Evidence |
|---|---|---|
| R1 | Met | `RefusalMatrixTests`: 49 bad documents, 26 bad calls and 7 bad saves, and the set of codes they reach equals the constants of `RelationshipCodes` by reflection |
| R2 | Met | `DecayTests`: the three shapes against a big-integer oracle at every age of 9 magnitudes x 6 durations x 5 half-lives (about 9,000 values), exact at whole half-lives, within 6.15 percent above the true curve between them, never growing, never changing sign, zero from the duration |
| R3 | Met | `NeedTests`: per-step exactness, 365 days of two meals and sleep against an independent hour-by-hour account, any split of calls gives one digest, traits and host rules multiply, clamps, critical and re-arm once each, 480 steps per call with the owed count |
| R4 | Met | `MoodTests`: the value is the exact sum; 40 random lives of 60 calls (7,200 moods) check that the explanation terms add to the sum and the clamped sum is the mood; hysteresis; four stacking modes with the limit and its eviction (ties go to the older) |
| R5 | Met | `EdgeTests`, `BoundaryTests`: opinion = clamped base + memories; exact ends; drift; forgetting; traits on the holder; stacking; caps with the stated eviction |
| R6 | Met | `LadderBondTests`: hysteresis both ways, 9,000 random-walk moves in which a state is never below the plain state and never more than the margin above it; symmetric, exclusive, non-exclusive, one-way, breaking bonds |
| R7 | Met | `InteractionTests`: every effect kind, cooldown per pair and direction, gifts (love, hate, neutral, clamped sum), seeded noise equal to `DeterministicRoll` call by call, refused effects, witnesses, offers, desire with negative weights, explanation text |
| R8 | Met | `GossipTests`, `BoundaryTests`: the listener limit, the telling limit, once per instance, hop limit, plan-then-apply (one hop a night), trust line, roll equal to the chance, seed, and the bound over 12 random villages x 6 nights |
| R9 | Met | `BoundsTests`: 300 random sequences of 80 calls each, invariants after every call and a save-and-open every 20 calls; found the orphaned bond (10.2) |
| R10 | Met | same inputs, same events and digest; another seed another run; the roll counter is saved and continues |
| R11 | Met | `SaveTests`: round trip bytes, 12 kinds of change change the digest, 38 forged states with recomputed digests refused as state, format/document/range-before-digest order |
| R12 | Met | `SourceGuardTests`, `RuntimeSourceGuardTests`: no clock, random, float or file access in the core and the game layer; no silent catch; no throwing catch-all |
| R13 | Met | `ComponentTests` (21): queue until Update, order, refusals leave the digest unchanged, channels, bounded events with none lost or reordered, host facts and clock faults reported once |
| R14 | Met | `ReplayTests` (27): from a checkpoint, from the start, from at least 8 other quiet ticks; a missing request or a changed fact is detected; the demo replays on every run |
| R15 | Met, with limits (10.7) | calendar (13 tests), dialogue (11), AI (8), characters (6) |
| R16 | Met | `FactsNamesTests`, `CliTests`: 47 keys, complete English table, fallback chain, misuse reported |
| R17 | Met, measured | 10.4 |
| R18 | Met | `CliTests` (15), every exit code |
| R19 | Met in part | 10.5 |

### 10.2 What the work found, and decided (source followed)

- **A symmetric bond could outlive its edge.** The random-sequence bounds test found an actor with a bond to a partner whose own edge had been evicted for size. Fix: evicting an edge clears its bonds on both sides first. Regression test in `EdgeTests`.
- **A calendar block could be lost.** The first block of a schedule was announced in the tick the calendar added the actor, before the relationship component had added it, and the bridge skipped it. The bridge now keeps the latest waiting block and forwards it once the actor exists.
- **An edge that holds nothing is forgotten at the day boundary, before gossip runs.** So an actor whose only link is a trust of exactly the baseline is not a listener. Said in section 5.4 and tested.
- **Mood is the exact sum, with a hysteresis margin on the state, not RimWorld's moving bar** (RimWorld wiki "Mood": the bar moves toward the target at +12 or -8 an hour). The bar is a second number that would need its own explanation; the margin gives the same stability and `explain` adds up.
- **Opinions are base plus modifiers with durations and decays** (CK3 wiki "Opinion": fixed durations such as -25 for 3 years, "most decay to 0"); the stacking rules, the range and the decay rates are this engine's, because that page gives none.
- **Gifts: taste scales the effects, one per cooldown** (Stardew, from search summaries of the wiki: Love +80 .. Hate -40, one gift a day); the numbers are the document's.
- **Acceptance: weighted rules plus a seeded noise against a threshold** (Prom Week blog: a social exchange pairs an action with a response decided by weighted social considerations). Not Versu's practices (10.7).
- **Gossip: planned from the state at the start of the night, applied afterwards.** Without that, how far news goes in one night would depend on who is asked first.
- **The step is the unit and durations are in steps; the day boundary does the slow work.** 1,000 actors x 4 needs is cheap every step; 20,000 edges are touched once a day.
- **Enum switches** are statements listing every member (`switch` expressions over an enum give CS8524 under warnings-as-errors); string-keyed dispatch is a closed dictionary with one refusal.

### 10.3 Pass 35, part A (the one-owner violations of pass 34), in one place

Locale, plural, message-format, coverage and `MessageArg` moved to `EngineCommon.Core/Locale`; `CalendarEngine.Core` no longer references `DialogueEngine.Core`. One channel registry in `GameRuntime.Channels` (rank per engine so several share a session; found necessary by the calendar proof that needed both). The calendar dialogue proof uses the real `DialogueComponent`. `EngineCommon.Tests` fails if a second locale or plural implementation or a second registry appears (shown to fail with two mutant files). Combat's three SHA-256 hex digests are `EngineCommon.Digest.Of`; its roll keeps its own hash on purpose. The calendar guards are now the shared Roslyn rules, which found two throwing `var other =>` arms and a conditional catch in the calendar (fixed). Suites after part A: unchanged hashes of the three demos.

### 10.4 Cost (measured on this machine, 2026-10-29, Release; `ScaleTests`, three runs, median, fastest and slowest of the repeats)

1,000 actors with 20 edges each (20,000 edges), 4 needs, 6 activities, traits on two thirds of them, winter and rain.

| Question | Run 1 | Run 2 | Run 3 |
|---|---|---|---|
| One ordinary step (15 repeats), median (min, max) | 1.31 ms (1.30 to 1.47) | 1.36 ms (1.32 to 1.70) | 1.61 ms (1.27 to 1.87) |
| The first step of a day: drift, prune, bonds, ladders, forgetting, gossip (15 repeats) | 6.1 ms (3.2 to 50.8) | 5.7 ms (3.3 to 60.0) | 6.2 ms (3.4 to 54.4) |
| Allocation of an ordinary step / a day's first step | 205 KB / 6.2 MB | the same | the same |
| 100 share-meals and 12 steps (15 repeats) | 5.8 ms | 4.9 ms | 4.7 ms |
| Save of 20,000 edges (2.9 MB): write, open | 78 ms, 216 ms | 89 ms, 226 ms | 78 ms, 242 ms |

The slowest day-boundary sample (50 to 60 ms) is the first one of each run (JIT and the first gossip night). Budgets set in `ScaleTests` from these: an ordinary step under 15 ms (10x the median), a day's first step under 150 ms (25x), under 400 KB and 12 MB allocated. The allocation of an ordinary step (about 200 bytes per actor) comes from enumerating interface lists; it was not tuned. Debug builds pass the same budgets. The component's per-Update cost (events for the changes) was not measured separately; the village demo runs 86,400 ticks, 6 villagers and 6,775 events in about 3 seconds.

### 10.5 Mutation checking and the demos

Mutants are single-expression changes applied by a script outside the tree to the source, built and run against the test project (the script restored each file; the checksums of all sources were compared before and after each round and are equal). **137 mutants** of the core, the Cli, the component, the bridges, the demo and the shared registry and coverage code: round 1 killed 73 of 88 core mutants; 15 survivors showed tests missing for boundaries (a need starting exactly at its critical line, a non-zero baseline with no edge, a memory at the exact end of its duration in the count and in the fact, the integer range of a host fact at both ends, a cooldown that ends exactly on the day boundary and one that does not, negative opinions in eviction by size, a witness when the target refuses, a gossip roll equal to the chance, an axis whose minimum equals its maximum, equal tastes, a half-life longer than the duration); `BoundaryTests` (16) was written for them and all but two now die. The game layer and bridges: 38 of 49 at once; 5 did not compile and were redone in a compilable form (all killed); the survivors showed a missing test of the calendar step at the origin, of equal ranges in the dialogue check and of the demo's exact output (a golden hash test was added). **Final: 133 killed, 4 survivors:** (a) `Replace` stacking with `>` instead of `>=` for memories of equal strength: the two states differ only in the origin, hops and told flag of an equally strong instance, which no scenario reaches; (b) the reader check that a ladder first step starts at the bottom of the range (`from != min` as `from > min`), equivalent because a value below the minimum is already refused by its range; (c) the scale check at exactly `int.MaxValue`, equivalent because the next check refuses the same value with the same code; (d) the demo director's `context.Tick < 5` guard, which nothing observes because the villagers are asleep for the first 500 ticks. Not covered by any mutant, so their tests are not shown to fail: the wording of `explain` beyond the tested lines, the order of the change events inside `EventsOf` (only checked as sets and a few sequences), the Cli's scenario reader beyond the tested shapes, the 128 MB save bound, the `MaxWaiting` limit, and the demo's divergence exit (3).

The village demo (`GameRuntime.Relationship.Headless`, default 60 days): `output-sha256 C0758AD761319EEF9D127E73A98B29FF995D71F58171DEDECE11E839CDE1DE2F` (twice the same; replay of ticks 28,831 to 86,400 from the checkpoint at 28,830: state same, events same, 4,575 events); with `--days 20 --checkpoint-day 7`: `8A5C1BE99F204EFEB30F3585708EC130EDCBCE27076CA1D4943F5EBA00FFE8B8`. The earlier demos are unchanged: Briefing go `201EA953...F219261`, wait `7FEE8EF5...F71FEE`, calendar `22848928B5F0...A57C`.

### 10.6 Suites

`dotnet build GameFrameWork.slnx`: 0 warnings, 0 errors. RelationshipEngine 201, GameRuntime.Relationship 92, EngineCommon 50, CalendarEngine 206, GameRuntime.Calendar 86, DialogueEngine 1,200, AIEngine 209, CombatEngine 139, GameContent 356, GameRuntime 559 (553 + the 6 character tests): all passing. (`DialogueEngine.Tests` has one time bound, the cost of 100,000 line steps, that failed once while the machine was loaded and passed alone and in the final run.)

### 10.7 Not done, and why

- **Versu's social practices and Prom Week's rule volume.** An interaction is one exchange with weighted rules over a small fact set; there are no joint plans with their own state, no obligations and no practice-level reasoning. Said in section 3; the primary papers were not read.
- **Reputation as a number, long-term stress, a moving mood bar, needs as a hierarchy.** Not built (section 3).
- **A change of opinion inside a day from a fading memory.** States are re-read at the day boundary and when the edge is touched; a memory that decays across a ladder line mid-day is noticed at the next boundary (tested).
- **Per-step allocation** (about 200 bytes an actor) was not reduced.
- **Locale tables in other languages**: only English is shipped; the machinery (plural categories, fallback, coverage) is tested with a partial French table.
- **Godot adapter**: none in this pass.
- **The AI bridge tells each agent only about itself.** Opinions of others are not percepts; an agent chooses by its own needs. A percept kind for known opinions would be a further bridge.
- **The dialogue outcomes are rules in code, not in the dialogue document.** A document cannot yet say "this signal does that to the relationships"; the game declares the rules when it assembles the bridge.
- **Real SimEngine time**: the bridges use the calendar and the session tick; SimEngine's own tick is not connected to the relationship clock (the calendar already projects onto it through `CalendarEngine.Sim`).
- **Economy, Tutorial/Onboarding, WorldEvents and Diplomacy**: the four remaining engines of the six were not started.

## 11. Pass 43 (2026-10-29): derived social state, document and save format 2, opinions as AI percepts

Work items 4 and 5 of `docs/ENGINE_ALGORITHM_REVIEW.md` (section R1). The engine now looks at the **graph** of opinions, not only at edges taken pairwise. It uses `EngineCommon.Graph` (`docs/ENGINE_GRAPH.md`: `LabelPropagation`, `TrustIteration`, `GraphBuilder`); no graph code is copied.

### 11.1 What is derived, and when

All three run once, in `LiveDay` (the first step of a day), after drift, expiry, bonds, ladders and forgetting and before gossip, in this order. Nothing runs per step (shown by `SocialScaleTests`: an ordinary step costs the same with the passes on and off).

| Pass | Algorithm | Result (saved) | Explain |
|---|---|---|---|
| Communities | Deterministic asynchronous label propagation over the edges whose opinion on `communities.axis` is above `above` (weight: the excess). Each actor starts from the community it was in (an actor with no liking edge starts alone), so a community keeps its identity. | `actor.Community`: the id of the lowest member (always an actor that exists and belongs to it) | `RelationshipExplain.Community`: members, who the actor likes and which of them share the community |
| Standing | EigenTrust-style fixed-point power iteration over `standing.axis` (weight: excess over `above`), pre-trust shared equally by the actors with trait `preTrustTrait` (nobody, or no trait: everyone), `alphaPermille` of pre-trust, `iterations` at most | `actor.Standing`: parts per thousand of the village average (1000 is average). Replaces the 10.7 decision "no reputation number": still nothing is stored as a *fact the player sets*; the one number is derived and saved with the state | `RelationshipExplain.Standing`: stored figure, the figure worked out from today's opinions, top three trust contributors, pre-trust |
| Balance pressure | Heider: for holder A, friend/foe B (A's strongest `topK` opinions at or above `like` or at or below `dislike`) and B's strongest `topK` opinions of C, with all three edges present: wanted sign of A about C is `sign(A,B) x sign(B,C)`. When A's own opinion of C (without the pressure memory) lacks that sign, A is given the document's memory about C at `strengthPermille`, origin B. At most `maxPerActor` pushes per actor per day (strongest product first, one per subject), each with `chancePermille` through the seeded roll stream `"balance"`. Planned from the state at the start of the pass, then applied. | an ordinary memory on the edge (so it decays, stacks, is evicted and explained like any other); bound: its magnitude x strength x the stack limit | `RelationshipExplain.Balance` (every triad through a common third actor, signs, wanted/has, pressed or not, the memory now and who it came through) and the opinion explanation ("pressure of the triad through B") |

The balance rule is **our design** (Heider's theory defines balance, not a local update rule; review section 3 R1). Label propagation is chosen over Louvain for order independence and bounds; Leiden stays deferred. Sources are those of the review; nothing new was fetched in this pass.

Facts (the dialogue and AI conventions of 5.6 and 9, `ext.` prefix in a dialogue document): `actor.community`, `actor.community_size`, `actor.standing` (and `target.`), `edge.same_community`, `reverse.same_community`. Their types and ranges are declared by the set, and `RelationshipDialogueFacts.Check` verifies a dialogue document against them (`SocialDialogueTests`). An axis may not be called `same_community`.

Events (game layer, declared v1): `RelationshipCommunityChanged`, `RelationshipStandingChanged` (only when the move is at least `eventDelta`), `RelationshipBalanceShifted` (holder, subject, via, the two opinions of the triad, scale; only for a new or changed push, not the daily renewal). They follow the existing order after evictions, are deferred and bounded like the others, and are in the transcript.

### 11.2 Document and save format 2

`RelationshipLoader.Format` and `RelationshipSave.Format` are 2. A format 1 document is refused with `REL_FORMAT_OLD` before any field is looked at, a format 1 save with `REL_SAVE_FORMAT_OLD`; any other number keeps `REL_FORMAT` / `REL_SAVE_FORMAT`. New required root field `social` with three blocks, each always present and each with `enabled` (a disabled block is still checked for shape; `balance.memory` may then be `""`): `communities {enabled, axis, above, maxRounds 1-64}`, `standing {enabled, axis, above, alphaPermille 1-1000, iterations 1-64, preTrustTrait, eventDelta}`, `balance {enabled, axis, like, dislike, memory, strengthPermille 1-10000, topK 1-8, maxPerActor 1-16, chancePermille 1-1000}`. New code `REL_SOCIAL`: dislike not below like; balance on with no memory; the pressure memory has no effect on the axis or spreads by gossip; communities/standing on with `maxActors x maxEdgesPerActor` over the graph library's 2,000,000 edges. Save: each actor carries `Community` and `Standing`; the digest covers them; ranges are checked before the digest (community is an id not after the actor's own id, standing 0 to 100,000,000), and after the digest the state must agree with the document (the community's namer exists and is in it; a disabled feature leaves the actor alone / at 1000). The checkpoint is this save. `samples/village.relationship.json` is format 2 with a `social-pressure` memory.

### 11.3 Opinions as AI percepts (item 5)

`RelationshipAiOpinions` (`GameRuntime.Relationship.Bridges`) tells an agent, for each (agent, other) pair the host says it can perceive, under the other's actor id: its **own** opinion of the other on every axis, the state of every ladder, `known`, and what a village knows of anyone: `standing`, `same_community`, `community_size`. It never tells what others think of the agent, nor another's mood. The AI document declares the percept kind exactly (checked at assembly, `RELAI_PERCEPT_KIND`); it is posted only when it changed; an agent that is no longer told keeps acting on what it last knew. The bridge decides nothing. `AiOpinionTests` proves with a real AI document and the AI engine's own `select` and `utility` nodes, on a village lived past a day boundary, that the agent befriends the high-standing neighbour rather than the one it likes more, avoids a hostile circle when nobody is worth befriending, prefers befriending when a worthy neighbour is in sight, changes its choice when its opinion changes and is told again, does not avoid someone of its own community, and acts on stale knowledge when not told.

### 11.4 Cost (Release, this machine, loaded by other work; `SocialScaleTests`)

1,000 actors x 20 edges (the sample document with its three passes on). The five variants (all off, each alone, all on) are lived to a day boundary round after round (25 rounds, interleaved, a collection before each timed step), three runs; rows in `docs/measurements/relationship-social-scale-2026-10-29.csv`. The machine was loaded by other work, so absolute figures are higher than those of 10.4 (the same test showed 12 to 14 ms for the unchanged day boundary where 10.4 measured 6 ms); read the differences.

| First step of a day, median over 25 rounds (min to max) | Run 1 | Run 2 | Run 3 |
|---|---|---|---|
| all passes off (the old behaviour) | 14.1 ms (6.3 to 69.7) | 12.2 (5.8 to 30.9) | 12.6 (6.2 to 30.8) |
| communities only | 20.9 | 20.3 | 21.9 |
| standing only | 30.1 | 27.8 | 27.2 |
| balance only | 26.8 | 26.9 | 27.3 |
| all three on | 56.2 (24.0 to 106.2) | 45.6 (24.0 to 111.8) | 45.8 (18.0 to 99.9) |
| ordinary step, all on / all off | 0.56 / 0.52 | 0.57 / 0.53 | 0.56 / 0.53 |

So the three passes together make the day's first step about **33 to 42 ms slower, 3.5 to 4 times the old boundary** on this loaded machine (about 40 ms against about 12 ms); the ordinary step is unchanged (0.5 ms). Most of it is building two 20,000-edge graphs (the library's sort) and the 12-iteration trust iteration (which sorts remainders each iteration, `docs/ENGINE_GRAPH.md` section 5); allocation per boundary rises from 4.3 MB to 7.4 MB. It is a one-off every day, but a game that renders at 60 frames a second will see a hitch of about two frames unless it spreads the day over several ticks (the component already lives at most 480 steps per Update); I did not split the pass across steps. `ScaleTests` (the older budget test) gives 44 ms for the day's first step in Release and its budget is now 150 ms in Release; in Debug the budget is 1,500 ms because Debug under load measured 100 to 240 ms. Save of 20,000 edges: 3.0 MB, write about 130 ms, open about 290 ms (Release, loaded).


### 11.5 Tests and checks

tests/WorldCreator.Tests/WorldCreator.Tests.csproj
src/ScenarioEngine/tests/ScenarioEngine.Tests/ScenarioEngine.Tests.csproj
src/EngineCommon/EngineCommon.Graph.Tests/EngineCommon.Graph.Tests.csproj
src/EngineCommon/EngineCommon.Tests/EngineCommon.Tests.csproj
src/DiplomacyEngine/DiplomacyEngine.Tests/DiplomacyEngine.Tests.csproj
src/RelationshipEngine/RelationshipEngine.Tests/RelationshipEngine.Tests.csproj
src/TutorialEngine/TutorialEngine.Tests/TutorialEngine.Tests.csproj
src/EconomyEngine/EconomyEngine.Tests/EconomyEngine.Tests.csproj
src/WorldEventsEngine/WorldEventsEngine.Tests/WorldEventsEngine.Tests.csproj
src/AIEngine/AIEngine.Tests/AIEngine.Tests.csproj
src/DialogueEngine/DialogueEngine.Tests/DialogueEngine.Tests.csproj
src/CalendarEngine/CalendarEngine.Tests/CalendarEngine.Tests.csproj
src/CombatEngine/CombatEngine.Tests/CombatEngine.Tests.csproj
src/GameContent/GameContent.Tests/GameContent.Tests.csproj
src/GameRuntime/GameRuntime.Calendar.Tests/GameRuntime.Calendar.Tests.csproj
src/GameRuntime/GameRuntime.Diplomacy.Tests/GameRuntime.Diplomacy.Tests.csproj
src/GameRuntime/GameRuntime.Relationship.Tests/GameRuntime.Relationship.Tests.csproj
src/GameRuntime/GameRuntime.Economy.Tests/GameRuntime.Economy.Tests.csproj
src/GameRuntime/GameRuntime.Tutorial.Tests/GameRuntime.Tutorial.Tests.csproj
src/GameRuntime/GameRuntime.WorldEvents.Tests/GameRuntime.WorldEvents.Tests.csproj
src/GameRuntime/GameRuntime.Tests/GameRuntime.Tests.csproj


### 11.6 Not done

- Gossip does not yet prefer in-group listeners (the review listed it as a possible use; nothing asks for it).
- The pressure memory is announced as a triad event, not as a memory event; a game that wants the memory itself can read the edge.
- Balance considers triads whose three edges all exist; it never creates an edge. Opinions of unknown people reach an actor only by gossip.
- Betweenness, Leiden, signed-graph clustering (Davis clusterability) are not built. No Godot adapter. No other-language locale tables (the new key `rel.memory.social-pressure` exists in English only).
- `OpinionGraph` raises `REL_CAPACITY` if the graph library ever refuses an edge; the loader keeps it inside the library's limits, so no test can reach that line.

Village demo after this pass (hash changes legitimately: the demo now prints the new events and every villager's community and standing, and the day-boundary passes move opinions): default 60 days `output-sha256 273A8AA080EA02123A0BB29DE068EF22B2D77CF03381545BE39F7C33F8F3718E` (twice the same; replay of ticks 28,831 to 86,400 from the checkpoint: state same, events same, 4,564 events); `--days 20 --checkpoint-day 7`: `E32F03FBAEA3078BCAC7F3BC44BF197CDC32A1DC02CB1622EC4AA6B2F09E1AB6`. Unchanged: Diplomacy `19D3184D...B025D`, Tutorial `21C23990...36D6` (their demos do not run a relationship component).


## 12. Pass 45 (2026-10-29): the day's work spread over its first five steps (`social.schedule`)

### 12.1 Finding (profiled, Release, 20 days of 1,000 actors x 20 edges, collection before each step)

The first step of a day cost about 33 ms; the split of one boundary was: drift/expiry/bonds/ladders/forgetting 7.1 ms, communities 4.9 (graph build 4.2, label propagation 0.7), standing 10 (second graph 3.7, 12 trust iterations 5.3, recording 1), balance 8.7, gossip 2.8. Graph building is only about a quarter of it, and **dirty tracking cannot help**: opinions are base values plus memories evaluated at the current step, and drift and memory decay move them every day, so the graph differs every day. Allocation is 7.6 MB a boundary against 4.4 MB with the passes off. So the fix is the schedule, not caching.

### 12.2 Change

New required document field `social.schedule`: `"atomic"` (the old behaviour: everything in the first step of the day, one graph shared by communities and standing when they read the same opinions) or `"spread"`: five stages, stage `k` lived in the step whose number is `k` past the start of a day (`Step % stepsPerDay == k`): 0 upkeep, 1 communities, 2 standing, 3 balance pressure, 4 gossip. Each stage is whole and planned from the state at its own step. `spread` needs `stepsPerDay` of at least 5 (`REL_SOCIAL`); a missing field is `REL_MISSING_FIELD`, another word `REL_FORMAT`. Owner: `RelationshipRuntime.Day.cs` (the stages are an array in order, so no switch and no default arm).

Where the pass has got to is a function of `Step` and the document, so **the save did not change** (format 2, no new field, digest unchanged in layout) and a save taken between two stages is exactly a save; a restored runtime lives the next stage. The document layout gained a field but the format number stays 2 (this pass's brief: bump only for a save layout change); a format-2 document written before this pass is refused naming the missing field. The sample is `"spread"`.

What changes when facts update (spread only): communities, standing and pressure are worked out from the opinions as they stand at steps 1, 2 and 3 of the day, not at step 0, and a pressure memory begins at step 3 of the day. A run that begins at step 0 (or restores mid-day) meets the later stages of its first day (steps 1 to 4) without that day's upkeep. The facts a dialogue reads (`actor.community`, `actor.standing`) are therefore the previous day's for the first 1 to 3 steps of a day.

### 12.3 Measured (Release, same process, four variants interleaved round after round, 25 rounds, every step of a day timed; machine loaded by other work; `docs/measurements/relationship-schedule-scale-2026-10-29-pass45.csv`)

| Worst step of a day, median (min to max) | Run A | Run B | Run C |
|---|---|---|---|
| passes off (control) | 8.5 | 9.6 | 10.7 |
| all on, atomic (before) | 29.7 (14.2 to 57.3) | 31.6 (15.2 to 74.4) | 32.8 (21.8 to 184.5) |
| all on, spread (after) | 8.4 (4.7 to 14.8) | 9.2 (5.5 to 14.3) | 10.6 (7.1 to 56.1) |

Spread steps 0 to 5 (run C): 9.4, 4.7, 9.5, 9.1, 3.1, 0.55 ms; ordinary step 0.53 ms (unchanged). The worst step of a spread day is about the cost of the old day without the passes (the upkeep step); the target of under 25 ms is met in the median of every run (maxima of single days under load reached 56 ms in the third run, the same spikes the passes-off control shows). The whole day costs the same (about 45 ms against 45 ms): the work is moved, not removed. The standing stage no longer shares the community graph (one extra 3.7 ms graph in that step, in spread mode only).

### 12.4 Checks

`DayScheduleTests` (19): stage timetable by step (events, rolls and memory start per step), atomic does it all in step 24, a frozen village reaches in 28 steps the same communities, standing, tellings and pressure under either schedule (only the pressure's start differs, 24 against 27), standing identical with a shared or separate graph (same axis same line, same axis other line, other axis same line), a save at each of steps 24 to 29 resumes to the same digest, rolls and events, one big Advance equals single steps, a five-step day, a mid-day start, the sample is spread, document refusals. Game layer: `SocialReplayTests` replays a spread day checkpointed between each pair of stages (ticks 241, 251, 261, 271, 281, 291, 1451) to the same state and transcript. Most older tests build on the sample with `"atomic"` (the text is swapped in `Fixture`/`Samples`); they pin what the passes decide, unchanged. Mutation check of the new code: 26 mutants, 23 killed at first, then 2 gaps closed with tests (standing graph sharing on the line alone; community-size cache after the communities stage) and re-killed, 1 equivalent survivor (the atomic day passing the communities graph on to standing instead of rebuilding it: results are identical, it saves 3.7 ms only).

Village demo (Headless): 60 days `84794AF42C5E24D63BDEE78802AD14AC06BB4C35B2FA759C6322C1C4BD28DC60` (was `273A8AA0...F3718E`), `--days 20 --checkpoint-day 7` `C1B6808F2FC5267B4AEA591F5710176C1F6EB0CBFBECB05682990E3F109BF00F` (was `E32F03FB...E1AB6`). They change legitimately: the sample document gained a field (its digest is part of every state digest) and the sample is now spread. Control: with the sample temporarily set to `"atomic"` the 60-day run replayed 4,564 events, the figure recorded before this pass (the hash itself still differs by the document digest); with spread it is 4,561. Two runs of each match and the replay from the checkpoint is state-same, events-same.

### 12.5 Not done

Balance and standing are not split further inside a stage; the standing stage (about 10 ms) is now the longest after the upkeep. No float or parallel version. Gossip stays in the last stage. Other documents (Diplomacy, Tutorial and so on) that embed a relationship document are not changed here.

## 13. Pass 47 (2026-10-30): document format 3, `REL_FORMAT_2`

`RelationshipLoader.Format` is 3. The document layout is exactly what format 2 required after pass 45: format 2 documents were allowed to omit nothing, but pass 45 added the required `social.schedule` (`"atomic"` or `"spread"`) without a number change, so a document marked format 2 could be either with or without it. The number now says which. A format 2 document is refused by its own code, `REL_FORMAT_2`, before any field is looked at, **whether or not it carries a schedule**; the message says that format 2 documents lack the required `social.schedule`, and how to migrate: set `"format"` to 3 and add `social.schedule`. There is no migration shim and no silent acceptance. A format 1 document keeps `REL_FORMAT_OLD`, any other number (including 0 and 4) and a non-number keep `REL_FORMAT`; this is the model of Economy's `ECON_FORMAT_2` (`docs/ECONOMY_ENGINE.md` section 13). The save format stays 2 (`RelationshipSave.Format`; the save layout did not change); a save records the digest of its document (`REL_SAVE_DOCUMENT` when it differs), so a save taken from a format 2 document text cannot be opened against the edited format 3 text; no document text is carried in a save.

The declared code list (`RelationshipCodes`) has 41 codes; the refusal matrix requires every one to be reached (new rows: "format 2" reaches `REL_FORMAT_2`; the former "format 3" row is now "format 4"). Samples and fixtures changed: `samples/village.relationship.json` and the embedded document in `GameRuntime.Tests/RelationshipCharacterTests.cs` (format 3); the test fixtures derive from the sample, so they follow. No other engine embeds a relationship document (Diplomacy and Tutorial integration tests load the sample).

Village demo: 60 days `D3987D2841CCF236859AB6AF08C6A3C3C724BEF19C341E8DF40DE462019656D3` (was `84794AF4...DC60`), `--days 20 --checkpoint-day 7` `84900AF4609AA265E4A1B917475219628FE7B76687025C61A7002B3A258FB205` (was `C1B6808F...F00F`). They change only because the document text changed (its digest is part of every state digest); the event counts are unchanged (4,561 and 1,443). Two runs of each match and the replay from the checkpoint is state-same, events-same. Tests: `FormatThreeTests` (format 2 without and with `social.schedule` refused with `REL_FORMAT_2`, message content, other formats keep their codes, format 3 accepted).

## 14. Pass 48 (2026-10-30): the multi-seed sweep in the Relationship tool

`RelationshipEngine.Cli simulate <relationship.json> <scenario.json> [<days>] --seeds <n> [--from-seed <s>] [--days <d>] [--metrics a,b,...] [--rows]`. The shared reducer, the report format, the refusal codes, the research and the offline tail tool are in `docs/WORLD_EVENTS_ENGINE.md` section 12; this section holds what is Relationship's. `RelationshipEngine.Core`, the format and the sample are unchanged; the single-run `simulate` output is byte-identical (`Simulate` and the sweep share one private `Live` loop) and the village headless demo hash `D3987D2841CCF236859AB6AF08C6A3C3C724BEF19C341E8DF40DE462019656D3` is equal after the change. The horizon is `<days>` or `--days D`, 1 to 3,650.

**What a seed can change here.** The seed feeds exactly three streams in this engine: the accept noise of an interaction (`accept`), the gossip chance of a telling (`gossip`) and the chance of a balance push (`balance`). Everything else (needs, moods, moodlets, ladders, bonds, decay, communities, standing) is deterministic from the document and the scenario.

**Metrics:** `mood.end.<actor>`, `mood.min.<actor>` (lowest mood at the end of any step), `standing.end.<actor>`, `edges.end.<actor>`, `critical.steps.<actor>` (steps that ended with any need critical), `standing.spread` (highest minus lowest standing among the scenario's actors at the end), `communities.end`, `edges.total`, `rolls` (seeded rolls drawn), `interactions.accepted`, `interactions.declined`, `changes.<balance|bonds|communities|evictions|memories|moodlets|moods|needs|standing|states>`, `scenario.refused`. Opinions per pair are not metrics: edges come and go, so their names would differ between seeds (`BAL_METRICS_DIFFER`).

### 14.1 Findings (200 seeds from 1, 90 days of the village sample; `docs/measurements/balance-sweep-relationship-village-2026-10-30.csv`)

- **Of 37 metrics, one varies: `changes.memories`** (0 to 1, positive in 119 of 200 seeds, mean 0.595). That is the gossip: in 59.5% of seeds the one telling the scenario makes is believed. **The other 36 are identical in all 200 seeds, and this is a property of the sample, not noise:** the village scenario draws only 2 seeded rolls in 90 days (`rolls` is 2 in every seed). Its five scripted interactions are accepted or declined the same way in every seed (4 accepted, 1 declined, in all 200): in the run at the document's own seed the one with noise that matters, `share-meal`, scored 0 plus noise -8 against 40 needed, and the `chat` scored 100 against 50, so the noise range (which I did not read from the document) is small against their distance to the threshold. I did not read the gossip rule's chance either; the one telling lands in 119 of 200 seeds. Moods, needs, standing (alice 4,000, the others 0, spread 4,000) and community count (4) never change with the seed.
- **Honest reading:** the village is a good determinism and rule demo and a poor balance probe. A balance question about acceptance noise or gossip needs a scenario with many interactions near their thresholds (a crowd, not four people), and a sweep over it; this sample cannot show whether the noise range is well chosen.
- **A scenario that ends with no edges.** `edges.total` is 0 in every seed at day 90 although interactions happened: the opinions the scenario creates decay away within the 90 days. Not a defect found in the engine; a note that a ninety-day village leaves no trace of its few interactions.
- **Cost** (Release, `village.relationship.json`, 90 days): 1,000 seeds in 21.5 s (about 21 ms a seed).

### 14.2 Tests, mutation, not done

`RelationshipEngine.Tests.BalanceCliTests` 11: one seed at the document's seed equals the single run's printed mood, standing and edges per actor, the edge total and the roll count; same arguments, same bytes (`--days` and the positional horizon equal; rows included); the seeds change the gossip; 8 refusal cases with exit 2. The shared mutation round is in `WORLD_EVENTS_ENGINE.md` section 12.6; Relationship's `LiveOneSeed` and the seed replacement were mutated there (M32, M33).

Not done: a crowd scenario that exercises the accept noise; per-pair opinion metrics (would need a fixed pair list from the scenario); the social metrics of the pass-44 `social` rules beyond `standing.spread` and `communities.end`.


## 15. Pass 50 (2026-10-30): a crowd scenario that exercises the random paths

The village (section 14.1) draws two seeded rolls in ninety days. `samples/crowd.relationship.json` and `samples/crowd.scenario.json` are the probe that was missing; the village sample, its scenario and its demo are untouched (the headless demo prints `D3987D28...9656D3` for 60 days and `84900AF4...FB205` for `--days 20 --checkpoint-day 7`, re-run after this pass).

**The sample.** The document is the village with four edits: `id` `crowd`, another seed, the chat interaction now scores 60 against an `acceptAt` of 50 with `noise` 80 (so a chat is accepted when the noise is above -10: about 55% of the time, right at the threshold), and the balance push `chancePermille` 500 (it was 1,000, which draws no roll). The scenario has 120 people (five trait sets, one in seven with a second trait, six tastes, three shifts of the daily routine) and 2,070 scripted interactions in 45 days: 28 a day among neighbours (half within five places of the actor, half anywhere; chat 5/12, share-meal 2/12, give-gift 2/12, insult, flirt and steal 1/12 each, witnesses on gifts and thefts) and 18 a day inside a clique of twelve that talk, give gifts and share meals, which is what pushes opinions over the balance thresholds. The generator was a throwaway script (a 31-bit linear congruential sequence from seed 20261030); the files are the data. 13 of the 2,070 calls are refused by the engine (cooldown).

**Run:** `RelationshipEngine.Cli simulate samples/crowd.relationship.json samples/crowd.scenario.json 45` ends at step 1,080 with 120 actors, 1,585 edges and 3,391 rolls; digest `283E5F0D1AE93B4244EF446E45AD3A39B2D58FA8DEB7C1041F2B41F8C31F3503`; the output's SHA-256 (line feeds only) is `62506D10C5D01482B616ED7B9C4128ED40904AC3C330F1BBBC0ABF53E905D4BD`, the same on a second run. **Checkpoint replay:** `simulate ... 20 --save s.bin` then `simulate ... 45 --resume s.bin` is new (the tool had no way to continue a save; `Live` takes a start step and the resumed run carries on from the save's step with the scripted calls not yet made) and ends in the same digest. Both are pinned in `CrowdScenarioTests`.

### 15.1 Measured spread (200 seeds from 1, 45 days; `docs/measurements/balance-sweep-relationship-crowd-2026-10-30.csv`, aggregate metrics; 80 s, 0.4 s a seed)

Of 617 metrics **325 vary** (the village: 1 of 37). `rolls` 3,362 to 3,445 (median 3,400); accepted interactions 1,223 to 1,316 (median 1,272), declined 741 to 834 (median 785): the chat noise decides about 785 interactions a run. Gossip: `changes.memories` 1,425 to 1,538 (median 1,475, median interval 1,471 to 1,478). Balance pushes: `changes.balance` 14 to 30 (median 21; zero in the village). Evictions 165 to 225 (median 196: with 120 people, 24 opinions each and the clique, the edge limit is reached every run). Communities formed 109 to 188 changes, ending at 99 to 101 groups. `standing.spread` 6,784 to 17,552 (median 8,833, p90 12,691): the one outcome where a seed matters most, a factor 2.6 between the quietest and the most uneven world. `edges.total` 1,550 to 1,617. Not exercised: `share-meal` is refused by the target 334 times and accepted 7 (its weights, 60 and 40, sit far under `acceptAt` 40 plus noise 30 for most pairs), so its noise range is not probed; `changes.bonds` is 0 (no scripted `propose`); `changes.moodlets` is 0 in the crowd **and in the village** because the sweep counts the changes of `Advance` only and the effects of an interaction (moodlets, memories, bonds it sets) come back with the interaction, not with the step; the `changes.*` metrics are therefore step changes, not all changes. Not fixed here (it would move the recorded village measurements); the metric names say `changes`, read them as that.

### 15.2 Tests, mutation, not done

`CrowdScenarioTests` 5: the document and scenario are accepted (120 actors, 1,585 edges, 3,391 rolls); the output twice and its two pinned digests; resume from day 20 ends in the pinned state with its first day line `day 21`; a save at the horizon is refused; eight seeds change accepted, declined, gossip, balance, evictions, standing spread and edges, and every seed draws over 3,000 rolls. The mutation round and the other tests are in `WORLD_EVENTS_ENGINE.md` section 13.6 (the resume option was mutated there, M26).

Not done: interaction-effect changes in the sweep metrics; a scripted `propose` and a `share-meal` mix that sits at its threshold; per-pair opinion metrics.
