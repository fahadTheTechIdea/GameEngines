# GameFrameWork engine skills

One skill per engine, written for an agent that is about to change or use that engine. Each skill states the boundary, the document/content format, the main contracts, the determinism and save rules, the CLI and the GameRuntime component, and points at the fuller HTML reference and the owned design record.

The fuller, source-verified reference for every engine is in [`docs/engines/`](../docs/engines/index.html).

| Engine | Skill | Reference |
|---|---|---|
| AIEngine | [gameframework-ai](gameframework-ai/SKILL.md) | [AIEngine.html](../docs/engines/AIEngine.html) |
| AlchemyEngine | [gameframework-alchemy](gameframework-alchemy/SKILL.md) | [AlchemyEngine.html](../docs/engines/AlchemyEngine.html) |
| CalendarEngine | [gameframework-calendar](gameframework-calendar/SKILL.md) | [CalendarEngine.html](../docs/engines/CalendarEngine.html) |
| CombatEngine | [gameframework-combat](gameframework-combat/SKILL.md) | [CombatEngine.html](../docs/engines/CombatEngine.html) |
| DialogueEngine | [gameframework-dialogue](gameframework-dialogue/SKILL.md) | [DialogueEngine.html](../docs/engines/DialogueEngine.html) |
| DiplomacyEngine | [gameframework-diplomacy](gameframework-diplomacy/SKILL.md) | [DiplomacyEngine.html](../docs/engines/DiplomacyEngine.html) |
| EconomyEngine | [gameframework-economy](gameframework-economy/SKILL.md) | [EconomyEngine.html](../docs/engines/EconomyEngine.html) |
| EngineCommon | [gameframework-common](gameframework-common/SKILL.md) | [EngineCommon.html](../docs/engines/EngineCommon.html) |
| GameContent | [gameframework-content](gameframework-content/SKILL.md) | [GameContent.html](../docs/engines/GameContent.html) |
| GameRuntime | [gameframework-runtime](gameframework-runtime/SKILL.md) | [GameRuntime.html](../docs/engines/GameRuntime.html) |
| RelationshipEngine | [gameframework-relationship](gameframework-relationship/SKILL.md) | [RelationshipEngine.html](../docs/engines/RelationshipEngine.html) |
| ScenarioEngine | [gameframework-scenario](gameframework-scenario/SKILL.md) | [ScenarioEngine.html](../docs/engines/ScenarioEngine.html) |
| SimEngine | [gameframework-simengine](gameframework-simengine/SKILL.md) | [SimEngine.html](../docs/engines/SimEngine.html) |
| SpatialEngine | [gameframework-spatial](gameframework-spatial/SKILL.md) | [SpatialEngine.html](../docs/engines/SpatialEngine.html) |
| TutorialEngine | [gameframework-tutorial](gameframework-tutorial/SKILL.md) | [TutorialEngine.html](../docs/engines/TutorialEngine.html) |
| WorldCreator | [gameframework-worldcreator](gameframework-worldcreator/SKILL.md) | [WorldCreator.html](../docs/engines/WorldCreator.html) |
| WorldEventsEngine | [gameframework-worldevents](gameframework-worldevents/SKILL.md) | [WorldEventsEngine.html](../docs/engines/WorldEventsEngine.html) |

## Rules that apply to every skill

- The engine owns its rules; the host translates events and never duplicates a threshold or target rule.
- Documents are strict: unknown or missing fields are refused, never defaulted.
- Determinism is real: seeded stateless rolls, whole-number maths, and no wall clock inside an engine.
- A save is verified against its document and state digest; never edit saved bytes.
- A faulted or mid-commit state is not a checkpoint.
- Verify with the focused test project for the engine, not a broad suite, unless explicitly asked.
- Do not describe parked or partial work as complete; `SpatialEngine` is a parked sketch and `SimEngine` has its own open plan.
