# TutorialEngine: research, boundary, document, criteria and result

Pass 37, 2026-10-29. The fourth of six planned engines (Calendar in pass 34, Relationship in pass 35, Economy in pass 36; Tutorial/Onboarding, WorldEvents, Diplomacy follow). Written before the code; section 10 records what was built and measured.

## 1. What already owns this (evidence, and the decision)

Read in the source on 2026-10-29:

| Owner | What it holds | Decision |
|---|---|---|
| `EngineCommon` | strict JSON reading, the one condition grammar (ScenarioEngine's), digest, the locale machinery (tables, chains, CLDR plurals, coverage), the Roslyn source guards (`EngineCommon.TestSupport`) | **Reused.** Documents are strict JSON; completion, branches and tip contexts use the shared condition grammar over facts; words are locale keys through `LocaleChain`/`CoverageRules`; the save digest is `Digest.Of`; the source rules are the shared guards |
| `GameRuntime.Channels` | the one `ChannelRegistry` and `ChannelProtocol`, `JournalText`, `EventTranscript` | **Reused.** `TutorialChannels` is a sealed subclass (rank 4, the next free one after dialogue 0, calendar 1, relationship 2, economy 3); the journal uses `JournalText`; the transcript is `EventTranscript` |
| `GameRuntime.Core` | `RuntimeSession`, the one `RuntimeClock`, deferred events, `InputState` (polled, written between Updates) | **Reused.** The tutorial has no clock: the tick is the only time; the player's presses are read from `InputState` in an Update (`TutorialInputComponent`, as `DialogueInputComponent` does) |
| `CalendarEngine` | the one coordinate of time, ticks per day, `cal.*` facts | **Reused through its contract** (`docs/CALENDAR_ENGINE.md` section 5): a tutorial step is a whole number of calendar ticks when the game wants hint timing in game time (`TutorialCalendar`) |
| `DialogueEngine` | conversations, external facts (`ext.*`), end results | A step can be spoken by a conversation and finished by its end (`TutorialDialogueSpeaker`); conversations read the tutorial's facts (`TutorialDialogueFacts`) |
| `EconomyEngine`, `RelationshipEngine` | trades, bonds, accepted interactions as events | Steps are completed by what really happened there (`TutorialEconomyObserver`, `TutorialRelationshipObserver`); neither engine learns that a tutorial exists |
| `AIEngine` | percepts, utility, behaviour trees; knowledge is not truth | A guide agent is told where the player is as a percept (`TutorialAiBridge`) and acts on what it was last told |
| the team's rules (`C:\Users\f_ald\.claude\CLAUDE.md`, "Required first-run app journey" and "Onboarding is a primary product feature", 2026-10-08) | language first, then the story, then account/host/join; progress saved at once; returning users skip; replay; one main task per step, visible progress, Next/Back, Skip, saved progress, replay; permissions at the point of use | **Built in.** The first-run journey is a state machine of the engine (section 5.6), not a convention of the host |
| nobody | tutorial documents with steps, prerequisites and branches, hints that escalate by idle time, contextual one-shot tips, skill mastery, skip/replay/resume, accessibility settings, the first-run journey | **The tutorial engine** |

The engine is a pure library over a step counter. It has no clock, no files, no world, no float and no random source (a tutorial has nothing to roll). The game layer's clock stays the only clock.

## 2. Research (searched 2026-10-29)

P means the page itself was fetched and a question answered on it (the fetch tool summarises the page with a small model, so a statement is quoted from that answer, not read by me line by line); S means a search summary or snippet only; N means not reached.

- **Nielsen Norman Group, "Mobile-App Onboarding" (nngroup.com/articles/mobile-app-onboarding).** P. Onboarding raises interaction cost and strains memory; its research found deck-of-cards tutorials "didn't improve task performance". It is justified when the app needs information to start, depends on context or preferences, or has unusual features. Contextual help beats an up-front tutorial ("it's often more effective to train users by showing them tips in context"); promote a feature when it is actionable, not at first launch; any deck of cards needs a highly visible Skip.
- **Talin, a framework for dynamic tutorials based on the skill atoms theory (AIIDE, ojs.aaai.org/index.php/AIIDE/article/view/13040).** P (the abstract page only). A per-player mastery model, so information appears only when needed and experts are not interrupted by tooltips about mechanics they already master.
- **GDC: "Prime, Teach, Observe: Tutorializing Innovative Mechanics" (Asher Vollmer, 2014) and "Mastering Kombat" (Mortal Kombat 11 tutorial mode, 2020).** S (search abstracts only; the GDC Vault pages were not read). Teaching without patronising; a flexible structure that gives players the knowledge they want for their style.
- **Duolingo's onboarding.** S (Appcues and other secondary commentary in search results; no Duolingo document). Value before sign-up: the first lesson comes before the account is asked for, and later prompts escalate. The commentary's figures (a 20 percent lift in daily active users, 8.9 percent conversion) have no primary source and are not used.
- **Portal, Factorio, Hades.** S for Portal and Factorio (blog posts: Portal teaches through level design with little explicit text; Factorio's tutorial is minimal) and one critical essay ("Wordless Tutorials are Overrated", critpoints.net, 2016: a wordless system that players could not grasp until a tutorial video was added). Hades: no onboarding analysis was found. **Celeste, Stardew Valley, Civilization: not reached.**
- **Apple Human Interface Guidelines, "Onboarding".** N. The fetch returned only the page title. The fetch tool then offered a summary from its own memory; it is not used as a source here.
- **Material Design onboarding guidance (m2.material.io, m3.material.io).** N. Both fetches returned only a page title.
- **Game Developer (formerly Gamasutra) writing on tutorials.** N. One fetch returned a section listing with no tutorial article.
- **The team's own rules** (see section 1): P, read in full.

What this research can and cannot support: the contextual-help, skip and mastery conclusions rest on NN/g and Talin (both fetched); the GDC, Duolingo, Portal and Factorio points are second-hand and are used only as direction, never as a number. The engine's thresholds (idle times, durations, gaps) are the document's: no source gives a figure that holds across games.

## 3. Adopt, adapt, avoid

| Source idea | Decision |
|---|---|
| NN/g: contextual tips beat up-front tutorials; promote a feature when it is actionable | **Adopt.** Tips are contextual: a tip shows while its context condition holds, never before the step it follows (`afterStep`), never for a skill the player has, and not at all during the first-run journey. They are one-shot by default (`maxShows`) and the player can say "got it" |
| NN/g: a deck of cards needs a highly visible Skip and may not help | **Adapt.** The first-run story is required by the team's rule, so it stays, but it is Back/Next/Skip at every page, one idea per page, saved at every page, and replayable from preferences. The tutorial proper is not a deck: its steps are doing, and a step can be skipped (when the document allows) or the whole tutorial skipped |
| NN/g: test whether onboarding helps before adding it | **Adopt as a measurable duty of the host, not of the engine.** The engine records what a player did and how long a step took (`ActivatedAt`, `DoneAt`, hint level reached) so a host can measure; it does not send telemetry |
| Talin / skill atoms: a mastery model; experts are not interrupted | **Adopt.** Events are evidence for skills (points, a mastery threshold); a step or a tip for a mastered skill is not taught or shown. **Avoid** a decaying or probabilistic model: points only rise, the threshold is the document's. A full replay ignores mastery because the player asked for it |
| Portal: teach by doing, with the world | **Adopt in the form the engine can own:** a step completes when the player did the thing (an event counted from the moment the step began, `since_step`), not when text was read. How the level teaches is the game's |
| Vollmer: do not patronise; observe | **Adopt.** A step whose completion already holds when it begins completes at once (when it reads facts rather than `since_step`); mastery skips what the player already does; hints only after idle time |
| Wordless-tutorial critique: wordless is not always functional | **Adopt as a fallback ladder.** Hints escalate from a nudge to an explanation to showing the target (`show` names a highlight target); the text is always available as a key |
| Duolingo: value before sign-up | **Not adopted as a default** and not contradicted: the team's rule puts the story before account/host/join; whether an account step may be deferred further is a choice of `entries` (`guest`) and of the host |
| Idle-time hints (a hint after N seconds) | **Adapt.** Idle time is counted in steps of the game's own time (so a paused game is not stuck, and a calendar bridge makes it game time) from the later of the player's latest activity and the step's beginning. A hint level only rises while a step is taught; acting again restarts the count for the next level, it does not take the hint away. No source gives a good idle time: the document chooses |
| Modal walls of text | **Avoid.** A step is one task with a short instruction key; a tip has a duration; nothing blocks the game. The engine never pauses anything |
| Motion that cannot be turned off | **Avoid.** Every authored motion (`pulse`, `slide`) becomes `none` under the player's reduced-motion setting, in the data the events carry |
| Fixed-size text | **Adapt.** A text scale (1000 to 3000 permille) lengthens tip durations so enlarged text can still be read; the host draws the text |
| Wall-clock timers, floats | **Avoid.** Steps are whole numbers; the same document, calls and facts always give the same state |

## 4. Layer boundary

- `TutorialEngine.Core` (pure library): document format 1, strict loader, `TutorialSet`, `TutorialRuntime` (steps, hints, tips, mastery, the journey, settings), facts, explanations, save/open, locale keys and coverage. No clock, no files, no Godot, no world.
- `GameRuntime.Tutorial` (game layer): `TutorialComponent` with Init and Update only, requests as events, answers in `Update`, its channel registry (`ChannelRegistry`, rank 4), deferred events with a bounded queue and back-pressure, checkpoint, journal and replay; `TutorialInputComponent` (polls `InputState`).
- `GameRuntime.Tutorial.Bridges`: calendar (clock, document check, host facts), dialogue (facts; a step spoken and finished by a conversation), economy and relationship observers (steps completed by real events), AI (a guide agent).
- `GameRuntime.Tutorial.Headless`: the valley demo. `TutorialEngine.Cli`: check, simulate, explain, coverage.

## 5. The model

### 5.1 Time

A step is a whole number given by the caller (`MoveTo`); it never goes back (`TUT_STEP_ORDER`) and the same step is allowed. The document states `time.stepsPerDay`, which is the contract with the calendar bridge: a tutorial step is `ticks per day / steps per day` calendar ticks. Nothing happens in `MoveTo`; `Evaluate` does the work, so the order of a tick is: time moves, the player's requests are applied (stamped with the new step), then the tutorial is evaluated against the host facts of that moment. A long jump costs one evaluation, not one per step: hints and tips are thresholds, not accumulations.

### 5.2 Steps

A step is one thing to teach (one main task). It has a localisation key for its title and for its instruction, an optional highlight `target` (an opaque id for the UI), a `motion`, `skippable`, an optional `skill`, `requires` (all of these steps satisfied), `requiresAny` (one of these really done, or every one bypassed), an optional `when` (a branch), a `complete` condition and up to four `hints`. Status: `pending`, `active`, `done`, `skipped`, `mastered`, `bypassed`; the last four satisfy later steps.

Prerequisites name only steps declared earlier (`TUT_PREREQUISITE` otherwise), so there are no cycles by construction. When a pending step's prerequisites are met: if `when` is false it is **bypassed** (the branch not taken: a gamepad player skips the mouse step); if its skill is mastered (and the tutorial is not being replayed in full) it is **mastered**; otherwise it becomes **active** if fewer than `limits.maxActiveSteps` steps are active (one: one main task at a time). An active step completes when `complete` holds, or becomes mastered if the player gains its skill. This repeats until nothing changes (a step already satisfied completes at once, and one completion can make the next available), at most steps+1 passes.

`requiresAny` is how two branches meet again: a bypassed alternative does not count (it is the branch not taken), a really done, skipped or mastered one does, and if every alternative was bypassed the step goes on rather than wait for ever.

### 5.3 Completion over observed facts and events

The host reports what the player did with `Observe(event, count)`. The engine counts each event and remembers when it last happened, and offers these facts to every condition: `event.<id>.count` (ever), `event.<id>.ago` (steps since, -1 never) and `event.<id>.since_step` (the count since the step began; **only a step's completion may read it**, `TUT_SCOPE` otherwise). A step that wants "the player built a structure" reads `event.structure_built.since_step >= 1`; one that wants "already did" reads `.count`. Also: `step.<id>.status` and `.done`, `tip.<id>.shows`, `.acknowledged` and `.visible`, `skill.<id>.points` and `.mastered`, `tutorial.phase`, `tutorial.progress`, `journey.state`, `journey.language`, and the settings; any other id is a host fact declared in the document (`facts`) and checked on every evaluation (`TUT_FACT_MISSING`, `TUT_FACT_INVALID`). Counts saturate at one billion, the limit of the shared fact model.

### 5.4 Hints

Hints are `{idle, kind, key}`, with idle strictly rising and kind never gentler than the one before (`nudge`, `explain`, `show`; `TUT_HINT` otherwise). The idle time of an active step is `Step - max(latest activity, step began)`; activity is any observation, a touch (any key the document has no event for), a skip, a dismiss or a resume. The hint level is the number of hints whose idle has passed. It only rises while the step is taught; a long jump gives one change to the strongest level reached. Acting restarts the count (the next level is due that many steps after the latest activity) but does not take the hint away; completing or skipping the step does. `Resume` (the player came back to a saved game) restarts the count, so time away is not taken for being stuck.

### 5.5 Tips

A tip `{when, cooldown, maxShows, duration, priority, skill, afterStep}` shows when it has a place: tips are on, the journey is done, it is not showing, not acknowledged, not out of shows, its skill is not mastered, the step it follows is satisfied, its cooldown since it last ended has passed, the global `tipGap` since any tip began has passed, fewer than `maxVisibleTips` are showing, and its context holds; the highest priority has the place first. It ends when its duration passes (**expired**), when its context stops holding or its skill is gained (**withdrawn**), or when the player says "got it" (**dismissed**, acknowledged: never again). The duration is lengthened by the text scale and fixed when the tip begins.

### 5.6 The first-run journey

`language -> story (page by page) -> entry -> done`. Choosing the language starts the story (and, at any other time, is the change of language from preferences; the journey does not move). `StoryNext` at the last page ends the story and offers the entry; `StoryBack` from the first page goes back to the language and from the entry to the last page; `StorySkip` goes to the entry and is remembered. `ChooseEntry` (sign in, register, host, join or guest, as the document offers) is refused before the story is over (`TUT_JOURNEY_ORDER`). A saved journey that is done is a returning player: nothing is asked again. `ReplayStory` (from preferences) shows the story again from its first page and ends in done, not in the entry. A deep-link intent (`join:room.7`) can be set at any point and waits. Every change is a change the host saves at once (`TutorialChanges.Persist`; the component raises `TutorialSaveNeeded`). The pages are keys: title, body and an illustration description.

### 5.7 Skills, mastery, skip, replay, resume

Events are evidence for skills (`points` each); a skill is mastered at its threshold and points never fall. Skipping a step needs `skippable`; skipping the tutorial marks every unsatisfied step skipped and stops teaching (tips go on). `Replay` starts every step over, ignores mastery (the player asked for the whole tutorial), keeps what the player did (event counts, skill points) and the tips' memory (`ResetTips` forgets that).

### 5.8 Accessibility

`SetReducedMotion` makes every authored motion `none` in the hint and tip data; `SetTextScale` (1000 to 3000 permille) lengthens tip durations (`ceil(duration x scale / 1000)`); `SetTipsEnabled` turns tips off (a showing tip is withdrawn at once). The three are part of the save. Right-to-left languages are the locale tables' (`TextDirection`); the engine supplies keys.

### 5.9 Bounds and the save

Document ceilings: 1,000 steps, 256 tips, 128 events, 64 skills, 64 host facts, 32 pages, 64 languages, 4 hints per step, 16 prerequisites, 8 active steps, 64 visible tips. A save checks, in this order: the format, the document, the **ranges of every value (before the digest is read)**, the digest, and last what the state says about the document (steps taught only while running, never more than the limit, prerequisites met, tips within their limits, skill points the events could have given). It never repairs.

## 6. Document format 1 (summary)

One strict JSON object, every field present: `format, id, time, limits, facts, events, inputs, skills, journey, steps, tips`. An unknown, missing or repeated field, a number out of range, a bad id, a reference that does not resolve, a bad condition, a prerequisite declared later, hints that do not escalate, `since_step` outside a step's completion, a host fact in the engine's namespaces, an input named like a control and a journey with a default language it does not offer are refused whole with a code (`samples/valley.tutorial.json` is a complete example).

## 7. Localisation

A document holds keys only. `TutorialNames.RequiredKeys` lists every key (skills, step titles and instructions, hints, tips, story titles, bodies and illustration descriptions); `Coverage` reports missing keys, messages no document uses and placeholders a key does not give, through the shared `CoverageRules`; rendering uses the shared `LocaleChain`. `LanguageSupport` says which languages the journey offers that have no table (they fall back to the default through the chain). Only English is shipped (`samples/valley.en.json`).

## 8. Acceptance criteria (fixed before coding)

| # | Criterion | Check |
|---|---|---|
| T1 | Strict loader: every document refusal code is reached by one input; a declared code that no case reaches fails | Refusal matrix over `TutorialCodes` by reflection |
| T2 | Steps: prerequisites, alternatives (a bypassed one does not count), branches bypass, the active limit, chains complete in one evaluation, skip, finish | Tests |
| T3 | Completion by events since the step began, by host facts and by other steps; what happened before the step does not count for it | Tests |
| T4 | Hints: the level is the number of hints due, at exactly the idle time; a jump gives one change; activity restarts the count and keeps the hint; completion clears it; resume | Tests with boundaries |
| T5 | Tips: duration exact, cooldown exact, maximum shows, acknowledge, withdraw, gap, priority, the visible limit, 50 at once, after-step, skill, journey, tips off | Tests |
| T6 | Mastery: points, once-only report, a pending step mastered, an active step mastered, a replay ignores it | Tests |
| T7 | Skip step, skip tutorial, replay, resume, reset tips | Tests |
| T8 | Journey: nothing after the language before it; no entry before the story; Back, Next, Skip; replay; language change; intent; the language is in a save taken at once; a returning player is done | Tests |
| T9 | Accessibility: reduced motion in hint and tip data, text scale in durations, tips off | Tests |
| T10 | Save: the digest changes with every kind of change; ranges are checked before the digest; then the document; 70 forgeries with recomputed digests are refused as state | Tamper tests |
| T11 | Every state reached by 300 random sequences of 80 calls saves and opens to the same state; the same sequence gives the same state; a save opened mid-sequence goes on identically | Random-sequence test |
| T12 | No clock, no float, no random source, no file access in the core and the game layer; no silent catch; no throwing catch-all arm; nothing thrown from a coalesce; the bridges never name the other engines' runtimes; the registry and locale owners are not duplicated | Roslyn guards, each shown to fail; the duplicate-owner guard covers the new engine |
| T13 | Component: requests only queue and are answered in Update in arrival order (a query last); an unserved channel is refused in the same tick; events are deferred, bounded per Update and owed, none lost or reordered; time waits while too many are owed; one SaveNeeded per Update with a persisted change; a clock or fact fault is reported once | Tests |
| T14 | Replay: a journal replayed from a checkpoint, from the start, or from other quiet ticks ends in the same digest and the same events; a missing request or a changed argument or fact is detected | Tests; the demo does it on every run |
| T15 | Integrations, one proof each: calendar (clock, document check, game time, pause), dialogue (facts; a step spoken and finished by a conversation), economy and relationship (steps completed by real events), AI (a guide told by percept) | Tests |
| T16 | Cost: 200 steps and 50 tips showing at once, with a budget set from measurements and repeats | `ScaleTests`, `docs/measurements` |
| T17 | Cli: check, simulate, explain, coverage; every mistake has an exit code | Tests |
| T18 | Every new test is mutation-checked; survivors are reported | Section 10 |

## 9. Game-layer design notes

- **One clock.** The session `RuntimeClock` is the only clock. `TutorialComponent` asks an `ITutorialClock` for the step of this tick: `TickTutorialClock(ticksPerStep)`, the calendar's (`TutorialCalendar.Clock`, read after the calendar component has moved this tick, so the tutorial component is added after the calendar), and `JournalTutorialClock` (the steps a recorded run used).
- **Channels.** `TutorialChannels` is a sealed subclass of the one `ChannelRegistry`, rank 4. Unserved channel: `TUT_CHANNEL_UNKNOWN` in the same tick; second component on a channel: `TUT_CHANNEL_DUPLICATE` at Init. Two players are two components on two channels.
- **Requests and answers.** One event, `TutorialRequested(requestId, kind, text, number, channel)`, with twenty kinds (`TutorialRequestKind`: observe, touch, start, skip-step, skip-tutorial, replay, reset-tips, resume, dismiss-tip, language, story-next, story-back, story-skip, entry, replay-story, intent, reduced-motion, text-scale, tips, progress). All only queue; each is answered in the next Update in arrival order with `TutorialApplied` (and the changes it made) or `TutorialRefused` with the code; an unknown kind is refused (`TUT_UNKNOWN_KIND`), not dropped. A refusal leaves the tutorial as it was (tested by digest). `progress` is answered after the tutorial has been evaluated, so it describes the tick the player just had.
- **Deferred, bounded events.** The changes the evaluation made are queued as owed closures in a fixed order (journey, settings, skills, steps, hints, tips, phases) and at most `maxEvents` (default 2,000) are raised per Update, oldest first, none lost. While `maxOwed` (default 20,000) are owed, time does not move. `IsQuiet` is false while any are owed, and a checkpoint is refused (`TUT_NOT_QUIET`). The changes a request makes are raised with its answer.
- **Saving at once.** A change the save holds (anything but a hint) is followed by one `TutorialSaveNeeded` per Update, after the events of the change. The host takes a checkpoint when it sees it and the component is quiet (the demo does).
- **Input.** `TutorialInputComponent` polls `InputState` in its own Update (it must be added before the tutorial component to be answered in the same tick): an action the document maps is an observation; `tutorial.skip-step`, `tutorial.skip`, `tutorial.replay`, `tutorial.dismiss-tip`, `journey.next`, `journey.back`, `journey.skip`, `journey.language`, `journey.entry`, `journey.replay-story` are their requests; any other press is a touch; releases are ignored.
- **Journal.** Text, one line per entry: `F|tick|facts`, `S|tick|step`, `R|tick|channel|kind|requestId|text|number`; escapes shared with the calendar (`JournalText`). Replay needs a tick rate that divides 1,000,000. The transcript is `EventTranscript`.
- **Calendar bridge.** `TutorialCalendar.Check` refuses a pair of documents at assembly (steps per day must divide the calendar's ticks per day; a host fact in the calendar's `cal.` namespace must be a calendar fact of the same type whose range the calendar's fits inside; the other host facts are the game's). `HostFacts` merges the calendar's facts with the game's and refuses a fact both give. Hints then count game time: a paused calendar gives no hints, a sped-up one gives them sooner in session time.
- **Dialogue bridge.** `TutorialDialogueFacts` gives the tutorial's facts as `ext.*` external facts and `Check` refuses a dialogue document whose declarations disagree in type or range or name `since_step`. `TutorialDialogueSpeaker` starts a conversation when its step becomes active and, when that conversation (and only one it started, optionally with a named result) ends, tells the tutorial the rule's event happened.
- **Economy and relationship observers.** Rules (trader and good; actor and bond or interaction, each optionally empty for any) turn `EconomyTradeExecuted`, `RelationshipBondChanged` (set) and an accepted `RelationshipInteractionPerformed` into observations on the tutorial's channel. A refused trade, a cleared bond or a refused interaction is not told.
- **AI bridge.** `TutorialAiBridge` posts the percept `step`, `hint_level`, `progress`, `phase` to the guide agent when it changed; the AI document's percept kind is checked against both documents.
- **What a checkpoint omits:** the registry (fixed by assembly) and the tick (kept by the caller: `TutorialComponent.Tick`).

## 10. Result

### 10.1 Criteria

| # | Result | Where |
|---|---|---|
| T1 | Met. 32 codes (12 document, 16 call, 4 save), each reached by a case; the matrix reads the constants by reflection and fails when a code is declared and not reached or reached and not declared. `TUT_UNKNOWN_KIND`, `TUT_NOT_QUIET`, the channel codes, the journal code and each bridge's code belong to the game layer and are reached by its tests | `RefusalMatrixTests`, `ComponentTests`, bridge tests |
| T2, T3 | Met | `StepTests`, `BoundaryTests` |
| T4 | Met, with the boundaries of every threshold | `HintTests` |
| T5 | Met, including 50 tips showing at once | `TipTests` |
| T6, T7 | Met | `MasteryTests`, `StepTests` |
| T8 | Met. Order is enforced, the language is in a save taken right after it is chosen, a returning player loads done | `JourneyTests`, the demo |
| T9 | Met | `HintTests`, `TipTests`, `ComponentTests` |
| T10 | Met. 72 forgeries each with a recomputed digest are refused as state; every field of the save is shown to be in its digest | `SaveTests`, `BoundaryTests` |
| T11 | Met. 300 sequences of 80 calls, 40 pairs of runs compared, 40 save-and-continue comparisons | `BoundsTests` |
| T12 | Met. The guards are shown to fail by synthetic sources (a `double`, a `?? throw`) and by mutants of the code; the duplicate-owner guard now reads the tutorial projects and expects five channel registries | `SourceGuardTests`, `RuntimeSourceGuardTests`, `DuplicationGuardTests` |
| T13 | Met | `ComponentTests`, `InputTests` |
| T14 | Met. From a checkpoint, from the start, from seven other quiet ticks, with a clock of five ticks to a step and for 25,000 ticks | `ReplayTests`, `ReplayClockTests`, the demo |
| T15 | Met, one proof each | `CalendarIntegrationTests`, `DialogueIntegrationTests`, `EngineEventsIntegrationTests`, `AiIntegrationTests` |
| T16 | Met | 10.4 |
| T17 | Met, every exit code | `CliTests`, `ExplainAndFactsBoundaryTests` |
| T18 | Met; survivors listed in 10.5 | 10.5 |

### 10.2 What the work found, and decided (source followed)

- **A branch that merges again.** The first version counted a bypassed alternative as satisfied, so `requiresAny: [look-mouse, look-pad]` was satisfied the moment the other branch was bypassed and the menu step began before the player had looked around. Now a bypassed alternative does not count; if every alternative was bypassed the step goes on rather than wait for ever (`AlternativesMet`, one function used by the runtime and by the save rules). Found by the mastery test of the sample.
- **Every reachable state must pass the save's rules.** Rules written from the design refused states the runtime really makes: a running tutorial whose last step was just skipped (before the next evaluation), a tip showing when tips were turned off or the story replayed, a replayed story that kept the old "skipped" mark. Each rule was either dropped (the first) or the operation made to leave a valid state at once (turning tips off or replaying the story withdraws the showing tips; a story replay clears the mark). The random-sequence test saves and opens the state after every call and would find another.
- **A failure reported over and over.** The first version cleared one failure memory on any success, so a clock fault was reported again every tick when evaluation succeeded in between. Clock and evaluation now keep their own memory and a failure is reported once until that same call works again.
- **A query is answered after the evaluation.** Answered with the requests, a progress query described the tick before what the player had just done.
- **Defaults that refuse.** The request event's number defaulted to 0, which `observe` refuses (`TUT_COUNT`); it now defaults to 1.
- **A build trap.** MSBuild treats `valley.en.json` as a culture-specific resource and puts it in a satellite assembly; the demo embeds it with `WithCulture="false"`.
- **Observations do not demand a save.** `Observe` changes the saved state (counts, skill points) but not progress, so it does not raise `TutorialSaveNeeded`; the counts are saved with the next change of progress. A crash loses at most the observations since then, never a step or a journey change.
- **Decided, with the source followed:** contextual, one-shot tips and a visible Skip (NN/g); a mastery model that skips what the player already does (Talin); the first-run order and persistence (the team's rule). Idle times, durations and gaps are the document's because no source gives a figure that holds across games.

### 10.3 The demo (`GameRuntime.Tutorial.Headless`)

A newcomer chooses French, goes back, chooses English, reads four story pages (the first save, taken right after the language, reopens at the story in French), enters as a host with a deep-link intent waiting, and starts the tutorial. She presses a key the tutorial hears, stalls on looking around (a hint at 640), opens the menu (a tip shows), pauses the game (a tip shows; she dismisses it), stalls 2,780 ticks on building (hints at 1550, 2150 and 3350), builds, skips the road step, hires a worker; the mayor then speaks (the step "talk" is a conversation; its first line is the "halfway" one because the conversation reads `ext.tutorial.progress`), the conversation ends and the step is done; she trades, makes a friend, saves, grows the valley and the tutorial finishes. A request for an event the document lacks and one for a channel nobody serves are refused. She replays the tutorial, enlarges the text, turns reduced motion on, skips the whole tutorial, replays the story and skips it. A second player on his own channel does the first things quickly, so the steps for the skills he has are mastered and never taught. The checkpoint is taken at tick 2000 (the first quiet tick from there) and the rest is replayed on a fresh session: state and events match (84 events). The printed output is hashed: `21C23990AF5240DAAE809518F73A78F500C286EDD8C1F4524A911890667536D6` on every run (checked three times, and by the test that compares two runs).

### 10.4 Cost (this machine, AMD Ryzen 5 5600X, Release, 2026-10-29; `ScaleTests`, three runs, median of the repeats; `docs/measurements/tutorial-scale-2026-10-29.csv`)

200 steps (a chain with a branch every tenth step and three hints on each) and 50 tips that all show at once and stay (the worst case for an evaluation, which reads every showing tip's context).

| Measure | Result (three runs) | Budget in the test |
|---|---|---|
| load the document (JIT included, one run) | 48 to 52 ms | not budgeted |
| first evaluation (JIT included) | 5.1 to 6.6 ms | 50 ms |
| one ordinary evaluation, 50 tips showing (median of 15) | 0.056 to 0.065 ms | 1 ms |
| allocation of an evaluation | 16,560 bytes | 64 KB |
| one observation | below 0.001 ms | 0.1 ms |
| walking all 200 steps, one observation each (JIT included) | 16 to 17 ms | not budgeted |
| save write (median of 5; the first call, JIT, up to 26 ms) | 0.34 to 0.38 ms | 10 ms |
| save open (median of 5; the first call up to 12 ms) | 0.73 to 0.83 ms | 20 ms |
| state digest | 0.10 to 0.15 ms | not budgeted |
| save size | 20 KB | not budgeted |

An evaluation costs about a twentieth of a thousandth of a second here; the budgets are 16 to 25 times the medians so that a loaded machine does not fail them, and a regression of an order of magnitude does. The work is linear in the steps and tips (an evaluation visits every step and every tip once); no timing was taken for documents far beyond the ceilings (1,000 steps, 256 tips).

### 10.5 Mutation checking

Mutants are single-expression changes applied by a script outside the tree, to a copy of the source, built and run against the test project; the script restored each file and compared its checksum every time. **265 distinct mutants** over the core, the Cli, the component, clocks, journal and replay, the input component, the five bridges and the demo. Round 1: 226 mutants (core 152, game layer 74): 186 killed, 40 survived, none failed to compile. The survivors were judged one by one: 33 showed missing tests (a cooldown counted from step zero, a host fact exactly at its limit, a text of exactly 4,096 characters, the limits of the text scale, all alternatives bypassed, a skipped alternative, a bypassed alternative with another still to do, limits of the document, a save of exactly the longest size, the fields of the digest, the end of the hint ladder in the explanation, a cooldown boundary in the explanation, the journal lines with a part too many, a replay of no ticks, a shared journal, a calendar of two ticks to a step, the first instant, a conversation ending on another channel or twice, a trade of another good, a refused or other-actor interaction, a bad option equal to the limit, back-pressure with a limit of one, a touch not being something to save) and were killed once `BoundaryTests` and the other new tests were written; 1 (`Array.Clear` of a pending step's counts after a replay) was killed by a new state rule plus a test; 1 (the `Distinct()` of the events a completion reads) and 1 (`Journey.Page = 0` on choosing the language) were dead code, and both were removed rather than tested; 4 were judged equivalent or unobservable (below). Round 2 re-ran the 35 survivors against the new tests: 33 killed, 2 survived (one was the dead `Distinct()`, one the replay's channel filter, which needed a transcript comparison; killed in round 3). Round 3: 39 further mutants of code the first rounds did not touch (explanations, the Cli's actions, the input mapping, the events' contents, the request names, the bridges' counters): 27 killed at once, 2 did not compile (not counted as killed), 10 survived and were killed after eleven more tests (the ranges of the engine's own facts, the shortest explanations, the Cli's observe, text-scale and reduced-motion actions, a replay clock of five ticks to a step and a replay of 25,000 ticks).

**Final: 257 killed, 6 unkilled, 2 did not compile (a mutant that cannot compile is not counted as killed).** The six:
- `args.Count < 2` to `< 1` in the Cli: equivalent (one argument matches no command and gives the same usage, exit 2).
- `var first = ... ? 4 : 5` to `? 5 : 5` in `explain journey`: equivalent (any extra argument after `journey` is the same mistake, exit 2).
- `value > int.MaxValue` to `>=` in the request's number conversion: equivalent (int.MaxValue is outside every range the requests take, so the same code is raised either way).
- `Journey.Page = 0` on choosing the language: dead (the page is already 0 at the language); removed from the code.
- `Distinct()` on the events a completion reads: dead (one fact per event); removed from the code.
- `same = state == digest` without the events comparison in the demo: **survives and is not covered**: the demo's exit code for a replay whose events differ while its state matches cannot be made to happen by a test without breaking the engine.

Not covered by any mutant, so their tests are not shown to fail: the wording of the Cli's usage text, the `JournalTutorialClock` beyond the cases tested, the order of the change events inside `EventsOf` (tested as a whole by the bounded-events comparison, not by mutants), and the demo's printed lines beyond the ones the test names.

### 10.6 Suites

TutorialEngine 170; GameRuntime.Tutorial 127 (the proofs of the calendar, the dialogue engine, the economy, the relationships and the AI engine are in it); EngineCommon 64 (the duplicate-owner guard now covers the tutorial projects); and, unchanged and passing, EconomyEngine 412, GameRuntime.Economy 118, RelationshipEngine 201, GameRuntime.Relationship 92, CalendarEngine 206, GameRuntime.Calendar 86, DialogueEngine 1,200, AIEngine 209, CombatEngine 139, GameContent 356, GameRuntime 559, ScenarioEngine 90. `dotnet build GameFrameWork.slnx` 0 warnings, 0 errors. Demos: Briefing go `201EA953...F219261` and wait `7FEE8EF5...F71FEE`, calendar `22848928...A57C`, village `C0758AD7...DE2F`, economy `0A9B16B3...3889` are unchanged.

### 10.7 Not done, and why

- **WorldEvents and Diplomacy**, the two remaining engines of the six.
- **A SimEngine bridge.** The task allowed economy, relationship or SimEngine for "steps completed by real engine events"; economy and relationship were built.
- **Permission prompts at the point of use.** The team's rule asks that a permission be requested when the feature needs it. The engine can teach a step and complete it from the event; asking the operating system is the host's, and no engine type models a permission.
- **Account, host and join screens.** The engine owns the order and the state (language, story, entry, intent); the screens, the account service and the join flow are the host's.
- **Other locales.** Only English is shipped. The coverage tool reports the journey languages that have no table.
- **Verification on a device** with a right-to-left language, enlarged text and a screen reader: the engine supplies the keys, the motion, the text scale and the durations; whether a Godot adapter draws them well is not verified. No Godot adapter exists.
- **Research not reached:** Apple's HIG onboarding page, Material Design's onboarding guidance, GDC Vault talks, Celeste, Stardew Valley, Civilization, Hades (section 2). Conclusions that rest on them are not drawn.
- **Measurement of onboarding itself** (completion, drop-off, time to first action): the engine records the facts a host would need but sends nothing, and nothing here shows that this tutorial teaches better than another.
- **Documents far beyond the ceilings,** and an evaluation that visits only the steps and tips that changed (it visits all, which the measurements show to be cheap at 200 and 50).
