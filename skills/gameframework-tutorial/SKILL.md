---
name: gameframework-tutorial
description: "Use GameFrameWork's TutorialEngine for onboarding: the first-run language/story/entry journey, steps completed by observed events and facts, escalating hints after inactivity, contextual one-shot tips, skills and mastery, skip, replay, resume, accessibility settings and save. Use when a game needs a how-to-play intro, guided first days, contextual hints or tips."
---

# TutorialEngine

Reference: `docs/engines/TutorialEngine.html`; research and measurements `docs/TUTORIAL_ENGINE.md`.
Code: `src/TutorialEngine/TutorialEngine.Core` (net8.0), CLI; component `src/GameRuntime/GameRuntime.Tutorial`; demo `GameRuntime.Tutorial.Headless`.

## Boundary

- Owns: journey, steps/dependencies, event counting, skills/mastery, hints, tips, skip/replay/resume, accessibility flags, save.
- Does not own: rendering, highlight implementation, audio, input mapping, or the text (keys only).
- Time is the step counter; progress depends only on state, events and host facts.

## Document (format 1)

```
{ "format": 1, "id": "...", "time": { "stepsPerDay": 1440 },
  "limits": { "maxActiveSteps": 1, "maxVisibleTips": 2, "tipGap": 0 },
  "facts": [...], "events": [...], "inputs": [...], "skills": [...],
  "journey": { languages, defaultLanguage, pages, entries },
  "steps": [...], "tips": [...] }
```

Strict JSON: unknown/missing fields, duplicates, dangling references, invalid conditions and unreachable steps are refused (`TUT_*`). Start from `samples/valley.tutorial.json`.

## Steps

A step is available when all `requires` are satisfied and one `requiresAny` (when given) was really done. Then:
`when` false → Bypassed; skill mastered → Mastered; otherwise Active until `complete`.
Statuses: Pending, Active, Done, Skipped, Mastered, Bypassed. `maxActiveSteps` is usually 1.
Completion uses facts; `event.<id>.since_step` counts only events since the step began.

## Hints

After `idle` steps without activity, hints escalate `nudge` → `explain` → `show`. `Hints()` returns `HintView(Step, Level, Kind, Key, Target, Motion)`; the host presents it. Activity (`Touch`/`Resume`) resets idle.

## Tips

A tip shows while `when` holds, at most `maxShows`, at least `cooldown` steps apart, for `duration` steps, after `afterStep` when given. `priority` breaks ties; `maxVisibleTips` and `tipGap` bound display. Ends: Shown, Dismissed, Expired, Withdrawn.

## Skills

Evidence events add points; at `mastered` the player is taken to know the skill, so its steps are Mastered and its tips are hidden. This replaces a separate "experienced player" flag.

## Journey and accessibility

Journey: Language → Story → Entry → Done (`SelectLanguage`, `StoryNext`/`Back`/`Skip`, `ChooseEntry`, `ReplayStory`).
`SetReducedMotion(bool)` turns Pulse/Slide into None; `SetTextScale(permille)`; `SetTipsEnabled(bool)` suppresses tips without losing progress. All saved.

## Running

```
var set     = TutorialLoader.Load(json);
var runtime = TutorialRuntime.Create(set, startStep: 0);
runtime.Start();
runtime.Observe("structure_built");
TutorialChanges c = runtime.Advance(step, hostFacts);
runtime.SkipStep("shelter");
```

`TutorialChanges` lists step/skill/hint/tip/journey changes. `StepState(id)`, `Hints()`, `VisibleTips()`, `ProgressPermille()`, `StateDigest()`.

## Facts

`event.<id>.count|.ago|.since_step`, `step.<id>.status|.done`, `tip.<id>.shows|.acknowledged|.visible`, `skill.<id>.points|.mastered`, `tutorial.phase|.progress`, `journey.state|.language`, `setting.reduced_motion|.text_scale|.tips`. Anything else is a declared host fact. `ReadFacts(prefix)` exports them.

## Save

`TutorialSave` + `StateDigest()`. A save names the document digest; mismatch (`TUT_SAVE_DOCUMENT`) or tampering (`TUT_SAVE_STATE`) is refused. `Replay()` restarts the tutorial; `ResetTips()` clears tip history.

## Runtime component

`TutorialComponent` applies observe/evaluate/controls/journey requests in `Update`. `TutorialBridges` maps game events (build complete, menu opened) into observations; the input component maps actions to events. A checkpoint waits for no pending request.

## CLI

`check`, `simulate <scenario> <steps> [--save]`, `explain step|tip|journey [--fact id=value]`, `coverage`. Exit 0 done, 1 refused/incomplete, 2 usage/file problem.

Tests: `dotnet run --project src/TutorialEngine/TutorialEngine.Tests -c Release`.
