# Connected multi-level framework world

This example composes three generated independent coordinate domains. Each level
contains16 exact source rooms,16 required fixtures and16 actual doorways. The
rectangular first/last levels deliberately reuse the same local IDs and identical
local world data; the middle level uses hexagonal room sources. Identity remains
`(DomainId, NodeId)`, never a local ID or world hash alone.

The existing C# engine owns topology, physical placement, walls, openings, paths,
endpoint selection, source edits and checkpoint replay. JavaScript inspects saved
geometry. This is a framework input, not a game setup, art profile or genre switch.
No mockup, image generation or Godot project is needed.

`definition.json` preserves the qualified16-room loop sources pinned by
`source-inputs.json`. Two explicit bidirectional level transitions bind to actual
incident doorway approaches via `InteriorOpening`. The first-to-last journey and
return each retain38 saved source segments/transitions across all three levels.
All48 rooms remain locally connected; no required fixture or edge is omitted.
Transitions are declared game relationships between independent coordinate
frames. They do not supply continuous3D stairs, portal artwork or physics.

`larger-fixtures-edit.json` increases all16 middle-level fixture sources from
0.8×0.5m to1.2×0.8m. Other source shapes, counts, inputs and budgets remain fixed.
The CLI and unchanged public SDK example produce identical99-node/96-source
worlds and byte-identical checkpoints before/after the edit. The edited run
reuses34/48 local stages; complete source-history restore equals accepted state
and edited output equals clean generation. Repeated edit IDs, a stale base hash
and a missing incident-doorway selector refuse without partial publication.

The native pipeline is `GenerateDomains`/`PublishDomainsAsync`, or reported
`MetadataDomainGenerationSession` create/edit/publish/restore. The existing
[public SDK workflow](../../worldcreator-workflow/README.md) consumes these files
through its `--domains` mode. The CLI supports `generate-world-domains` and
`domain-session`. Authors edit reusable definitions/catalogs, not saved geometry.

The80×80m surfaces are available search/query domains. Actual floor geometry is
identified by `InteriorPlan.Results.RoomNodeIds` and those nodes' exact
`AccessPlan.Sites.Footprint` polygons. Do not fill the entire search rectangle as
playable room flooring or mistake it for complete authored world coverage.
Source-room floor area, equipment solids, generated walls and walkable area are
distinct quantities. Per-domain partial coverage remains explicit.

This is a complete declared three-level metadata program. It is not a completed
game, owner design acceptance, general3D generation or a replacement for the
explicitly refused24-room single searched-stage inputs. The16-demand cap applies
to a searched placement stage, not to the complete world.

[Verification, actual commands, source identities and runtime provenance](../../../docs/evidence/2026-10-30-multi-level-world-verification.json).
