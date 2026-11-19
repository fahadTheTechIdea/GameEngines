# Complete mixed-world resource profiles

These are framework inputs, not a configured game. They retain seven terrain-based
compound families, thirteen structures, nine ground areas, thirteen equipment
members, actual outside structures, generated rooms/content and logical progression.
Each landscape object is an exact 1×1 source funded by measured habitat mass and
bound to its own density-weighted stratum. Storage cells do not place objects.

| Folder / support amount per landscape member | Landscape members | Complete nodes | Physical members |
| --- | ---: | ---: | ---: |
| `40` | 118 | 191 | 186 |
| `20` | 236 | 309 | 304 |
| `10` | 473 | 546 | 541 |
| `5` | 946 | 1,019 | 1,014 |

Every folder contains a full `definition.json` and `edit.json`. The edit replaces
the exact 10×6 production hall with a 14×10 source and a matching entrance port.
All funded landscape members, parent sizes, movement profiles and original work,
cache and transport limits remain fixed. Unrelated local/progression domains and
the required terrain → outside entrance → rooms → progression journey/return stay
intact. These explicit transitions join independent coordinate spaces; they do
not synthesize physical bridges or runtime transition logic.

`count-limit/definition.json` requests support amount4. Its required funded count
exceeds the existing 1024 demand ceiling. Generation refuses with
`METADATA_TERRAIN_DEMAND_COUNT_LIMIT`; it does not clamp the population or publish
a partial world. Only declared catalog/demand count scope was expanded for these
profiles. Solver budgets stay fixed, and every exact source footprint remains required.

The [public Core-only SDK host](../../worldcreator-workflow/README.md) consumes each
accepted pair through `--domains <definition.json> <edit.json>
<new-output-directory>`. The current CLI uses `domain-session start`, then
`domain-session edit`. Outputs contain complete worlds, colored schematics and
source-history checkpoints. No AI agent, artwork profile, mockup or Godot is needed.

## Observed resource scope

Fresh processes measured create, validation, publication, source edit, validation,
clean comparison and checkpoint restore. The baseline, streaming-only and final
shared-read-only JSON implementations retain exact world bytes, physical sources,
paths, work and cache sizes. Publication retry receipts remain visible and can
change packet size independently of world data.

| Profile | Baseline cumulative allocation, GiB | Final cumulative allocation, GiB |
| --- | ---: | ---: |
| `40` | 15.59 | 13.31 |
| `20` | 15.89 | 13.69 |
| `10` | 16.93 | 14.71 |
| `5` | 18.82 | 16.56 |

Those are total allocation volumes across seven measured phases, not live-memory
requirements. One process ran per case/implementation, on the recorded Windows
/.NET10 host. Timings, peaks, cold initialization and Windows publication retries
are retained individually. These observations are not controlled throughput or
universal memory guarantees. Streaming alone barely changed total lifecycle
allocation; reusing immutable serializer metadata in the shared owners produced
the final observed reduction. The public mutable JSON factory and specialized
strict readers remain available and isolated.

The largest profile has many small landscape contributors; it is not a benchmark
of a thousand complex buildings. The complete terrain domain has 988 physical
members, below the existing per-domain1024 access ceiling. The additional26
physical members belong to the separate local domain. Whole-world scale,
independent terrain paging, unbounded streaming, temporal gameplay, browser visual
acceptance and production art remain separate work. Godot stays deferred.

See [commands, comparisons, source hashes and remaining gates](../../../docs/evidence/2026-10-30-composed-resources-verification.json).

Actual browser inspection of the complete amount5 packet and its exact SVG exports
is recorded in the [inspection evidence](../../../docs/evidence/2026-10-30-browser-inspection-verification.json).
This is technical geometry/control qualification; owner design and runtime acceptance remain open.
