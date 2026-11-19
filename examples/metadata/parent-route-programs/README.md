# Reusable parent-route programs

One catalog program now populates every matching generated parent. These framework
examples put two required equipment members in each room alongside its original
fixture, without discovering or writing per-room connection IDs beforehand.
The same program handles three or six parents; source rooms, fixtures, walls,
openings and directed paths remain exact.

`Population.Route` selects one of two current modes: an explicit `ConnectionId`,
or a parent selector with `ConnectionId: null`:

```json
{
  "ConnectionId": null,
  "Spacing": 0.5,
  "StartDistance": 0.6,
  "EndInset": 0,
  "NormalOffset": -0.8,
  "Reverse": false,
  "Parent": { "ConnectionKind": "door", "ConnectionIndex": 0 }
}
```

The engine finds connections of the declared game-authored kind incident on the
actual nested-stage parent. It selects the zero-based ordinal in ascending
connection-ID order, then consumes the qualified path departing from that parent.
If the parent is the source connection's destination, its saved reverse path is
required. `Reverse` flips this relative direction. Missing paths/directions reject;
the engine never switches to another connection to make placement succeed.

The selector requires a physical source parent and an index from 0 through 127.
Selection charges the existing placement ledger, checks cancellation and retains
only the required ordinal prefix (at most 129 transient entries). Catalog source
sizes, part counts, containment, walls, movement width and original work limits
still apply. Native read/export replays the source and the complete derivation.

| Case | Parents | Complete nodes | Physical sources | New equipment |
| --- | ---: | ---: | ---: | ---: |
| branching | 6 | 25 | 24 | 12 |
| rotated | 6 | 25 | 24 | 12 |
| compact | 3 | 13 | 12 | 6 |

`expand-rooms-edit.json` changes the compact source to six rooms while retaining
the identical content program. `missing-kind-edit.json` is an intentional refusal
against the edited state. Actual API and CLI packets/checkpoints match. The
current explicit-ID input workflow remains supported and separately exercised;
checkpoints still pin the exact engine identity.

[Evidence](../../../docs/evidence/2026-10-30-parent-route-verification.json).
This is metadata/geometry generation for a framework, not a configured game.
Broad world design, arbitrary geometry, runtime playability and the full core gate
remain open. Godot and production artwork remain deferred.
