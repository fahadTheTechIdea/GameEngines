# DiplomacyEngine

Pass 39, 2026-10-29. Engine 6 of 6 (Calendar, Relationship, Economy, Tutorial/Onboarding, WorldEvents, **Diplomacy**). Sections 1 to 8 were fixed before the code; section 10 records what was built and measured.

The DiplomacyEngine is a passive child of the thin game layer, like the five before it: it has `Create`, `Advance` and requests, no clock of its own, no wall time, no floating point in state, no hidden randomness. It owns the relations between **realms** (factions, states, houses: whatever the game calls a party that can make a treaty): what each realm thinks of each other, the treaties and obligations between them, wars and coalitions, spies and their intelligence, and the internal politics that decide whether a realm can keep its word. It does not own markets (the Economy does), leaders' feelings (the Relationship engine does), conversations (Dialogue), or battles (Combat).

## 1. Research

### 1.1 What could be reached and what could not (read 2026-10-28)

Reached and used:

| Source | What was taken from it |
|---|---|
| EU4 wiki, Diplomacy (eu4.paradoxwikis.com/Diplomacy; the page was truncated and parts are marked verified for version 1.24) | Diplomatic reputation, trust, AI acceptance of proposals as a sum of weighted reasons, coalitions against an aggressor, casus belli, breaking treaties costing reputation. Only the parts visible in the fetched text were used. |
| Wikipedia, Bargaining model of war (Fearon) | War is costly, so it happens only through missing information or an indivisible stake or a commitment problem. Used to shape the war score, the exhaustion cost and the peace terms (a peace is accepted when it beats continuing), and to keep wars rare. |
| Wikipedia, Tit for tat (Axelrod) | Nice, provocable, forgiving, clear. Used for the standing rule: a first offence is answered once and then forgiven as it fades. |
| Wikipedia, Balance of power (Waltz, Walt, Mearsheimer) | Balance of threat and bandwagoning. Used for the coalition rule: the share of power, a bandwagon rule and a hysteresis band. |
| Wikipedia, Correlates of War | The alliance types (defense, neutrality, non-aggression, entente) used as the treaty clause vocabulary. |

Not reached, so not relied on:

- **Crusader Kings 3 wiki** (ck3.paradoxwikis.com/Diplomacy): the page is a disambiguation page; the opinion-modifier mechanics were not read.
- **Victoria 3 and Stellaris**: only search-result snippets (forum posts) were reached, not the wiki pages. Their diplomatic plays, infamy and attitude mechanics were **not** read; infamy in this engine is a design inference from the EU4 text and common usage, not from Victoria 3.

Everything below that says "adopt" or "adapt" is therefore tied to the sources in the table. Where a design choice rests only on our reasoning it is marked **design**.

### 1.2 Adopt, adapt, avoid

| Pattern | Decision | Why |
|---|---|---|
| AI acceptance as a visible sum of weighted reasons (EU4) | **Adopt** | `Acceptance.Evaluate` returns the score with a trace of fifteen named factor sources; `explain proposal` prints it. A refusal can always be explained. |
| Hidden random acceptance | **Avoid** | A proposal with the same inputs always has the same answer. Randomness appears only between the accept and reject thresholds, as a counter-based roll named by the proposal. |
| Reputation that decays (EU4 trust/reputation) | **Adapt** | Four axes (trust, fear, honour, infamy) in parts per million that approach zero at a rate each axis declares, plus dated deeds that fade linearly. |
| Tit for tat | **Adapt** | Provocable (a breach costs the breaker), forgiving (everything decays), clear (the consequences are listed in the document). |
| Coalitions against a threat (Walt) | **Adapt** | Threat share over the line forms a coalition; members join by bandwagon or balance; a hysteresis band stops flicker. |
| Bargaining model of war | **Adapt** | War score from declared sources, exhaustion that costs every season, peace accepted by comparing terms to continuing. |
| A diplomacy screen that lists every realm | **Avoid** | Not a UI; the engine answers questions (`Facts`, `Estimate`, `Evaluate`). |
| Perfect information about other realms | **Avoid** | A realm sees another's might only as a band that tightens with intelligence (`Estimate`). |
| Treaty rules in code per type | **Avoid** | Each treaty type is a data record of clauses (Defend, Truce, Payment, NonAggression and so on); no `if (type == "alliance")` in the engine. |
| Real-time AI chatter | **Avoid** | No timers: the host advances time. |

## 2. Principles

1. **Passive.** `Create`, `Advance(step, ...)`, and requests only. The caller owns the clock and tells the engine the step; a step that goes backwards is refused with `DIP_STEP_ORDER`.
2. **Deterministic.** Integer state. Counter-based randomness `DeterministicRoll.Below(seed, stream, index, bound)` with streams named by ids; no generator state, so a replay from any checkpoint rolls the same numbers.
3. **Data, not code.** Everything a game designer would tune is in the strict-JSON document: seasons, ideologies, traits, deeds, treaty types and their clauses, casus belli, missions, realms, factions, agents, limits.
4. **Refusal with codes.** Every wrong document, request or save is refused with a stable `DIP_*` code. A test names each code.
5. **One owner per fact.** Money is the economy's (the engine asks for funds, it does not hold a treasury); leaders' opinions are the relationship engine's (carried in, never stored twice); events are the game layer's.
6. **Bounded.** Realms 64, the document's limits cap treaties, proposals, wars, agents, missions, calls, incidents and grants; a save beyond them is refused before it is applied.

## 3. Document (format 1)

Strict JSON: unknown, missing and duplicate fields are refused. Top level: `format`, `id`, `seed`, `time` (steps per day), `limits`, `seasons`, `rules`, `ideologies`, `traits`, `deeds`, `casusBelli`, `treatyTypes`, `missions`, `realms`, `agents`, `signals`/`shocks` (outgoing effects), `events` (politics), `hostFacts`.

- **Seasons** (`spring`, `summer`, `autumn`, `winter` in the sample) carry a length in per mille of the year; they are what clauses with a `dueSeason` (tribute at harvest) refer to.
- **Rules**: acceptance (`acceptAt`, `rejectAt`, the weights of the fifteen factor sources), standing (decay per axis), war (score sources, exhaustion), balance (coalition line, bandwagon, minimum members, hysteresis), politics, espionage.
- **Treaty types**: consent (`mutual` or `imposed`), clauses, term range in days, permanence, what a cancel or breach does, `requires` (a condition over facts, evaluated by the shared condition reader), regard bonus, and signals/shocks on signing, breaking and expiry.
- **Clauses**: the eight kinds are `NonAggression`, `Trade`, `Tribute` (an amount every interval, with a missed-payment tolerance and an optional due season), `Defend` (call to arms with a grace period), `Vassal`, `Truce` (a ceasefire), `Bond` (marriage and similar ties) and `Embargo`. A treaty type that ends a war (peace) says so with `endsWar`.
- **Missions** (kinds Intel, Sabotage, Incite, Counter; the sample names them spy, sabotage, unrest and guard), each with base odds, the intelligence it needs, what a success changes, and what exposure costs.

## 4. Behaviour

### 4.1 Regard and acceptance

`RegardOf(a, b)` is one number from -1000 to 1000, the sum of the declared parts (standing: trust, fear and honour; power ratio; shared enemies; ideology; ties such as treaties and leader regard carried in; weariness; threat; and so on). The acceptance score of a proposal weighs fifteen factor sources: regard, trust, fear, honour, power ratio, shared enemy, ideology, ties, weariness, threat, stability, coalition, terms, war score and exhaustion. The acceptance score of a proposal is `bias + trait bias + sum(raw x weight / 1000) + modifiers`. At or above `acceptAt` the chance is 1000 per mille; below `rejectAt` it is 0; between, linear. A realm the host controls is never decided by the engine: its proposals wait for `Respond`.

### 4.2 Treaties and obligations

A signed treaty lists obligations from its clauses. An obligation falls due on its interval; `Fulfil` marks it done (the game layer pays through the economy), and a missed one beyond `maxMissed` is a **breach**. A breach cancels the treaty, costs the breaker honour and trust with the witnesses, grants the wronged party a casus belli, and leaves a deed that fades.

### 4.3 Wars

Declaring needs a casus belli (or accepts infamy for none). Sides are bitmasks; allies bound by a `Defend` clause are **called to arms** and may answer within a grace period. War score rises from declared sources (battles, occupation, blockade) and falls with exhaustion each season. A `Truce` clause is a ceasefire; a treaty that ends the war is a peace and may carry tribute.

### 4.4 Coalitions

When one realm's share of the total might passes the line, the realms that fear it may join a coalition against it (a bandwagon rule lets the weak join the strong target instead). A coalition forms at `minMembers` and dissolves only below the line minus the hysteresis band.

### 4.5 Espionage

An agent on a mission has odds from skill, the target's counter-intelligence and the intelligence level the owner has; success changes the target (intelligence, stability, treasury shock), failure may expose and **capture** the agent, and a captured agent is released after a term or by treaty. Intelligence fades.

### 4.6 Internal politics

Each realm has factions with weights, stability and legitimacy. Stability moves toward a target, has three bands (Stable, Unrest, Revolt) with hysteresis, and a realm in revolt cannot propose treaties (`DIP_REVOLT`). A ruler can die or be deposed: `Succession` draws an heir (a roll named `succ:<realm>` indexed by generation) and may be contested.

## 5. Game layer

`GameRuntime.Diplomacy` wraps the runtime as an `IGameComponent`:

- **Channels**: a `ChannelRegistry` subclass (rank 6; the duplicate-owner guard now expects at least seven such registries) so several diplomacies can coexist.
- **Events** are deferred and capped with back-pressure (`maxEvents`, `maxOwed`); a step that would exceed them is carried over, not dropped.
- **Clock**: a `TickDiplomacyClock` for tests or a calendar-driven clock; catch-up is capped at 366 steps per Update.
- **Journal and replay**: every request and each step actually lived is journalled as text; a checkpoint is taken only when the component is quiet (`DIP_NOT_QUIET` otherwise) and `DiplomacyReplay.Run` resumes it on a fresh session; the replay must end in the same state digest and the same event transcript hash.
- **Request/answer**: facts about a pair, the acceptance explanation and the mission odds are asked with events and answered with events.

### 5.1 Bridges (`GameRuntime.Diplomacy.Bridges`)

| Bridge | Meets | How |
|---|---|---|
| `DiplomacyCalendar` | Calendar | The calendar's day count drives the steps, its seasons map onto the diplomacy's seasons; the pairing is checked at assembly (`DIP_CALENDAR_MISMATCH`). |
| `DiplomacyEconomy` | Economy | Payment obligations become `fund` requests (debit one market, credit the other, refund if the second fails); shocks and signals become economy shock requests. |
| `DiplomacyWorldEvents` / `DiplomacyWorldFacts` | WorldEvents | Facts about the world (storms, plague) flow in as host facts; the diplomacy's signals flow out as story signals; checked at assembly (`DIP_WORLD_*`). |
| `DiplomacyDialogueFacts` / `DiplomacyDialogueOutcomes` | Dialogue | External facts such as `diplomacy.aurel.brass.at_war` are offered to conversations (declared type and range are checked when the game is assembled); chosen outcomes become requests. |
| `DiplomacyTutorialObserver` | Tutorial | Treaty signed or war declared complete tutorial steps. |
| `DiplomacyAiPercepts` | AI | An envoy agent is told, as a percept, its realm's regard of another, the other's trust, war, treaty count, intelligence level and a **band** for its might; it knows nothing else. |
| `DiplomacyLeaderRegard` / `DiplomacyLeaderOutcomes` | Relationship | In: a leader's opinion of the other leader (one axis, stretched to -1000..1000) becomes the realm's leader regard. Out: a marriage creates a bond between rulers, an alliance moves an axis, a betrayal leaves a memory. Rules are data (`LeaderRule`) and are checked against both documents. |

### 5.2 Combat

**No Combat bridge was built.** The CombatEngine has no hook that expresses "these two realms are at war" or "this war has ended": its groups carry only an integer `Side` and the refusal `COMBAT_SAME_SIDE`. Mapping realms onto integer sides in a bridge would invent a contract the combat engine does not define, and nothing in the combat engine reads a war or peace fact. A war's score is therefore fed by the host (`Score`), which is where a combat result would enter once the combat engine offers a stable outcome contract. This is a limit of the present pass, not a finished integration.

## 6. Tools

`DiplomacyEngine.Cli`:

- `check <diplomacy.json>`: loads the document, prints its digest and a summary, or the refusal code.
- `simulate <diplomacy.json> <scenario.json> <steps> [--save <state.json>] [--verbose]`: scripted requests and facts over steps; prints the events and the final digest.
- `explain ... proposal|regard|mission|coalition|realm`: the reasons behind a score, a regard, odds, a coalition's share or a realm's state.
- `coverage <diplomacy.json> <locale.json>...`: every text key a document needs, found or missing, per locale (through EngineCommon's shared coverage).

Exit codes: 0 done, 1 refused or incomplete, 2 usage or file problem.

Samples (`src/DiplomacyEngine/samples`): `isles.diplomacy.json` (five realms, eight treaty types, six casus belli, four missions, six agents), its English locale (56 keys), a scenario and the economy document of the five markets used by the headless demo.

## 7. Acceptance criteria

1. A document with an unknown or missing field, a duplicate id, a dangling reference or an out-of-range number is refused with a code, and each code is named by a test (the refusal matrix).
2. A save is refused with `DIP_SAVE_FORMAT`, `DIP_SAVE_DOCUMENT`, `DIP_SAVE_STATE` (ranges, checked **before** the digest) or `DIP_SAVE_DIGEST`; a good save reopens to the same digest.
3. Two runs of the same document and requests produce the same state digest; a run resumed from a checkpoint plus the journal produces the same digest and the same event transcript.
4. No state changes on a refusal.
5. Acceptance, regard, odds and coalition shares can each be explained with the contributing parts.
6. 64 realms, 200 active treaties and 100 agents live a year of daily steps inside the budget with the bounds held (measured in section 10).
7. The component never raises more events in an Update than its cap and never drops one silently.
8. A bridge whose two documents disagree is refused when the game is assembled, naming both.
9. No catch swallows, no throwing default arm, no clock or float in state, no variant name in shared code (Roslyn source guards).
10. Existing engines' demo hashes do not change.

## 8. Limits (decided before the build)

- No geography: distance, borders and supply are not modelled; the host supplies facts if a game needs them.
- No negotiation beyond propose/respond: counter-offers are separate proposals.
- Money is the economy's: the engine asks, and a refused payment is a missed obligation.
- War outcomes are fed in by the host (`Score`); the engine does not fight.
- Intelligence is a level per observer and target, not a model of what exactly is known.
- Internal politics is factions and stability, not a simulation of individual courtiers.
- Text keys are resolved through the shared locale chain; no text is built in code.

## 9. Refusal codes

`DIP_FORMAT`, `DIP_UNKNOWN_FIELD`, `DIP_MISSING_FIELD`, `DIP_RANGE`, `DIP_DUPLICATE`, `DIP_REFERENCE`, `DIP_RESERVED_NAME`, `DIP_CLAUSE`, `DIP_RULES`, `DIP_POLITICS`, `DIP_ESPIONAGE`, `DIP_UNKNOWN_REALM`/`TYPE`/`TREATY`/`PROPOSAL`/`WAR`/`CASUS_BELLI`/`DEED`/`AGENT`/`MISSION`/`EVENT`/`SOURCE`/`SEASON`, `DIP_SAME_REALM`, `DIP_TERMS`, `DIP_BLOCKED`, `DIP_ALREADY_ACTIVE`, `DIP_AT_WAR`, `DIP_NOT_AT_WAR`, `DIP_NOT_PRINCIPAL`, `DIP_NOT_HOST_CONTROLLED`, `DIP_NOT_PARTY`, `DIP_NO_CASUS_BELLI`, `DIP_CANNOT_CANCEL`, `DIP_UNAVAILABLE`, `DIP_REVOLT`, `DIP_CAPACITY`, `DIP_STEP_ORDER`, `DIP_FACT_MISSING`, `DIP_FACT_INVALID`, `DIP_NOT_IN_WAR`, `DIP_NOT_DUE`, `DIP_SAVE_FORMAT`, `DIP_SAVE_DIGEST`, `DIP_SAVE_DOCUMENT`, `DIP_SAVE_STATE`; game layer: `DIP_CHANNEL_DUPLICATE`, `DIP_CHANNEL_UNKNOWN`, `DIP_UNKNOWN_KIND`, `DIP_NOT_QUIET`, `DIP_JOURNAL`; bridges: `DIP_CALENDAR_MISMATCH`, `DIP_ECONOMY_MISMATCH`, `DIP_WORLD_MISMATCH`, `DIP_WORLD_RULE`, `DIP_DIALOGUE_MISMATCH`, `DIP_DIALOGUE_RULE`, `DIP_TUTORIAL_RULE`, `DIP_AI_PERCEPT_KIND`, `DIP_LEADER_AXIS`, `DIP_LEADER_RULE`.

## 10. What was built and measured

### 10.1 Built

- `DiplomacyEngine.Core` (about 3,400 lines): strict reader and runtime with 46 core `DIP_*` codes (and 15 more in the game layer and bridges), immutable set, runtime split by concern (advance, regard, treaties, wars, balance, politics, espionage), digest-checked save (format, document, ranges, digest, then rules against the document), explain texts.
- `DiplomacyEngine.Cli`: check, simulate, explain, coverage (every exit code tested).
- `GameRuntime.Diplomacy`: component, channels, request port, events and catalog, transcript, clocks, journal, checkpoint, replay.
- `GameRuntime.Diplomacy.Bridges`: calendar, economy, world events (facts in, signals out), dialogue (facts and outcomes), tutorial, AI percepts, relationship (leader regard in, rulers' bonds and memories out). **Combat: none, see 5.2.**
- `GameRuntime.Diplomacy.Headless`: the isles, five realms on the Gregorian calendar with the economy of their five markets: alliance, trade, marriage and embargo signed; a vassalage refused; a non-aggression pact broken by a war (a casus belli granted to the wronged); a peace with tribute that is paid through the economy; agents sent, one exposed and captured, later released; a coalition against the strongest realm; two successions; the economy shocks the treaties and missions ask for. It prints `output-sha256`, and replays the second part of the run from a checkpoint on a fresh session. The default 240-day run prints `19D3184D3B71968A0CD61ABAA4EA242AA40407C6EF74F1FDAB8C9B1E473B025D` every time (6 treaties signed, 1 broken, 1 war declared and ended, 1 tribute paid, 1 agent exposed, 1 coalition, 6 economy shocks asked, 119 events; the replay agrees in state and in events). **Superseded (pass 49, recorded in `docs/ECONOMY_ENGINE.md` section 15.9): the hash is now `8266A7D3101EFCC2783C6CF2AEB5685288C8BE7FFBE78E84131069A84A094029`, because the embedded isles economy document changed text; the counts above are unchanged.** Money is conserved across the five markets.

### 10.2 Tests (focused on this pass, all passing)

| Suite | Tests |
|---|---|
| DiplomacyEngine.Tests | 505 |
| GameRuntime.Diplomacy.Tests | 160 |
| EngineCommon.Tests (duplicate-owner guard now covers DiplomacyEngine and expects at least seven channel registries; a shared `EnumNames` helper with its tests) | 72 |

The engine suites cover acceptance, treaties, wars, reputation, balance, espionage, politics, facts and names, saves, the refusal matrix (every code), determinism, the CLI, scale and Roslyn source guards. The runtime suite covers the component, replay, each bridge with a proof (including documents that disagree), the headless demo and the runtime source guards.

### 10.3 Measurements (Release, `docs/measurements/diplomacy-scale-2026-10-29.csv`, three runs)

64 realms (2,016 pairs), 100 agents, 220 treaties proposed and in force at once, 4 wars, 1,080 missions started over a year of 364 daily steps: an ordinary step median **1.24 to 1.39 ms** (min 0.36 to 1.22 ms, max 6.9 to 7.6 ms); the year **578 to 705 ms**; 303,664 bytes allocated by a step; a 69 KB save written in about 1.0 to 1.2 ms and opened in 2.5 to 2.7 ms; the regard of all 4,032 ordered pairs 8.0 to 9.0 ms; one acceptance score 0.06 to 0.10 ms; 3,108 changes in the year. Loading the 67 KB document took 52 to 247 ms (the first run includes JIT warm-up). The test budgets (400 ms a step, 60 s a year) are far above these medians so they catch an accidental quadratic and not a loaded machine. This was measured on one Windows machine; no other hardware or operating system.

### 10.4 Mutation checks

A runner outside the tree edits one operator on one line, builds, runs the suite (the engine suite for the core; the game-layer suite for the component and bridges), and restores the file by checksum (it stops if a restore does not match). It first checks that the unmutated tree passes, because a failing baseline would make every mutant look killed.

**What went wrong the first time.** A first run was stopped by force while a mutant was applied, which left that mutant in the tree (`AtWar(b, e) && InCoalitionAgainst(b, e)` in the shared-enemy rule, then, in a second stopped run, `_pending.Count != 0` in the component's `IsQuiet`). The 91 core results of that run were recorded against a failing baseline and **were thrown away**; the tree was found failing on a full rerun (one engine test), the lines were corrected, the runner now checks its baseline, and both rounds below were run again from a clean tree. Mutants that hang a suite are ended by a watchdog and count as killed.

- **Engine core: 91 mutants** (every second candidate in the acceptance, math, advance, regard, treaty, war, balance, politics, espionage and state-rule files). First clean round: 72 killed, 15 survived, 4 did not compile. Eight survivors were real gaps and led to `BoundaryTests` in the engine suite (entries a document refers to by index 0: the casus belli a breach, a coalition or a capture grants, and the deed witnesses remember; a tribute due in the season of index 0; a defence pact that does not concern the target; the weariness factor; a save that holds a ceasefire), each confirmed to fail with its mutant applied. Second round after those tests: **80 killed, 7 survived, 4 did not compile** (not counted as killed). The 7, one by one:
  - equivalent: `score <= RejectAt` (the ramp already gives 0 at that score), `Clamp` with `value <= minimum` (returns the minimum either way), a grant held `Until >= Step` (the advance removes a grant at that step before anything asks), a terms burden for `amount >= 0` (the burden is 0 when the amount is 0), `war.Score >= 0` choosing the winner (the code only runs for a score that is not 0), and the deed-pair key `holder * n - subject` (no two pairs collide, because the subject is always below `n`);
  - not killed: the coalition joins a realm whose regard is exactly the line (`<= line` for `< line`); exact equality of that comparison is not tested.
- **Game layer and bridges: 55 mutants** (every candidate in the component, clocks, journal, replay and the bridges). First clean round: 47 killed, 7 survived, 1 did not compile; the survivors that were real (the treaty and war counts of a standing answer, the facts a replay starts with, the calendar clock at its origin) led to the game-layer `BoundaryTests`. Second round: **51 killed, 3 survived, 1 did not compile.** The 3 are equivalent: `_pending.Count >= 0` for `> 0` (an empty loop does nothing), `wanted >= from` for `wanted > from` and `target >= from` for `target > from` (with equal values the target is the same and the loop does not run).

Not covered by any mutant: the wording of explain texts beyond the lines asserted, the headless demo's wording, the CLI usage text, and the reader's range checks (covered by the refusal matrix, not mutated). Mutants were sampled (every second or every candidate of operator swaps on lines without string literals), not exhaustive.

### 10.5 Bugs and decisions found by the work

1. Seasons first drawn as warm and cold did not fit a Gregorian calendar; the sample uses four seasons, and the calendar bridge maps by the calendar's own seasons.
2. Gregorian spring begins on day 59 (1 March) and a calendar step after N days is N: the first test expectations were wrong in both and were corrected from the calendar document, not from memory.
3. A payment that the economy refuses (not enough funds) is a missed obligation: the headless demo gives the markets a treasury large enough for the tribute to be paid, and the refusal path is tested separately (`DiplomacyEconomy` debit/credit/refund).
4. A test that renamed a fact the dialogue itself uses was refused by the dialogue loader before the bridge's own check ran; the mismatch tests declare an extra fact instead.

### 10.6 Documented limits

Section 8, plus: **no Combat bridge** (5.2); the demo's war outcome is scripted by the host; realm might is set by the host (`SetMight`) and not computed from the economy; leaders' regard is carried per pair on one relationship axis; the engine was measured and run only on Windows with the .NET 8 target, and no device or other operating system was tried.
