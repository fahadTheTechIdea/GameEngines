---
name: gameframework-dialogue
description: "Use GameFrameWork's DialogueEngine for conversations and quests: branching lines with guards and effects, choices with availability and timeouts, random and variant nodes, calls, priority and interrupts, quest stages, content warnings, kept history, save and hot reload, localisation sheets, and Yarn Spinner or Ink import. Use when a game has talking characters, a script, quests or story beats driven by facts."
---

# DialogueEngine

Reference: `docs/engines/DialogueEngine.html`; audits and research `docs/DIALOGUE_ENGINE.md`.
Code: `src/DialogueEngine/DialogueEngine.Core` (net8.0), CLI; component `src/GameRuntime/GameRuntime.Dialogue`.

## Boundary

- Owns: graph checks, execution, choices, random/variant, calls, priority/queue, quests, content warnings, history, save, hot reload, locale tooling, Yarn/Ink import.
- Does not own: rendering, portraits/audio assets, the speaker roster (`ISpeakerCatalog`), timers (the host calls `Timeout`), or translated text.

## Load (format 3)

```
var set     = DialogueLoader.Load(json, speakerCatalog);   // validates the whole graph
var runtime = new DialogueRuntime(set, seed);
var facts   = new Dictionary<string, FactValue> { ["met_guard"] = FactValue.From(true) };
```

Document sections: `variables`, `externalFacts`, `speakers`, `quests`, `conversations`, optional `historyLimit` and `contentWarnings`. Text is always a key + typed args; no prose in the document.
Refused at load: unknown fields, duplicates, unknown speakers/portraits, unreachable nodes, waitless loops, call cycles, silent chains, undeclared content warnings.

## Nodes

`line` (waits unless a skip warning passes it; `glue`, `alternative`), `choice` (guard, `showWhenUnavailable`, weight, effects, timeout), `branch` (ordered cases + otherwise), `random` (weighted, seeded), `effects`, `call`/`return`, `variant` (seen/shuffle modes + `exhausted`), `end`.
Effects: `set`, `add`, `startQuest`, `setStage`, `finishQuest`, `signal`.

## Running

```
DialogueStep step = runtime.Start("main", facts);  // Line | Choices | Ended | Queued
step = runtime.Next(facts);
step = runtime.Choose("option-id", facts);
step = runtime.Timeout(facts);
var why = runtime.ExplainChoice(facts);            // per-option availability + reason
runtime.Interrupt("alarm", facts);
```

- `onBusy` decides what a second start does: refuse (`DLG_BUSY`), queue, or preempt by priority.
- Unavailable options are hidden or disabled; choosing one is refused (`DLG_OPTION_UNAVAILABLE`).
- Step and call-depth limits protect the session (`DLG_STEP_LIMIT`, `DLG_CALL_DEPTH`).
- Every step carries the events raised: variable, quest, signal, conversation, skipped.

## Quests

`QuestDef`: id, title key, start stage, ordered stages. Each stage has a journal key, guard→next rules and an optional terminal status. `StartQuest` and `UpdateQuests(external)` return events. Statuses: NotStarted, Active, Completed, Failed, Abandoned. A quest that cannot advance is refused (`DLG_QUEST_LOOP`).

## Content warnings

Map a tag to an external fact and `skip` or `replace`. A skipped line is passed without waiting; a replaced line shows its declared `alternative` key. Undeclared tag → `DLG_CW_UNDECLARED`; missing fact → `DLG_CW_FACT`; replace without alternative → `DLG_CW_ALTERNATIVE`.

## Localisation

`DialogueSet.Keys` (placeholder names) and `KeyUses` (translator context). `TranslationSheet` exports/imports sheets and rejects stale/unknown keys and bad placeholders. `LocaleCoverage` reports missing/extra/malformed. Plurals use CLDR categories.

## Save, history, reload

- `DialogueSave.Write(runtime)` / `DialogueSave.Open(set, bytes)`; `StateDigest`/`Canonical` for journals.
- Saves verify the document digest (`DLG_SAVE_DOCUMENT`) and state digest (`DLG_SAVE_STATE`).
- `HistoryLog` keeps up to `historyLimit` lines/answers with speaker, key, args, chosen option, seen/skipped/replaced.
- `Reload(next, mode, map)` hot-swaps; `ReloadMap` follows renames. Incompatible changes are refused (`DLG_RELOAD_INCOMPATIBLE`) unless the mode allows dropping state.

## Importers

`YarnImporter` / `InkImporter` produce a document, a locale table and a report. `ImportCapabilities` states Converted / Approximated / Dropped / Refused per feature with a sample — never silent loss.

## Runtime component

`DialogueChannels.Install(session)` before `Init`. Requests: start, advance, choose, quest-start, quests-update, reload, interrupt, timeout, skip, history. Results: line-shown, choices-shown, ended, signal-raised, quest-changed, variable-changed, reloaded, refused, skipped, conversation-changed, history lists. `DialogueCheckpoint` saves session state. `DialogueTriggers` starts conversations from world facts; `DialogueInputComponent` maps input.

## CLI

`check`, `explain`, `export-strings`, `import-strings`, `coverage`, `graph`, `import yarn|ink`, `import matrix`, `reload-map`. Exit codes 0 done, 1 refused/incomplete, 2 usage/file problem.

Tests: `dotnet run --project src/DialogueEngine/DialogueEngine.Tests -c Release`.
