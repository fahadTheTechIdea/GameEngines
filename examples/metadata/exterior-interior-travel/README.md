# Exterior/interior physical journeys

These framework inputs extend reusable generated rooms with a real catalog-owned
entrance and outside travel. The engine retains six source-sized room modules,
six fixtures and two purposeful outside destinations. One generated room graph,
one named exterior entrance and a regional connection form a continuous journey
from the remote outside destination to the last room.

The three cases use an unrotated regional route, a 23-degree rotated program and
a generated destination network. All consume the same generated wall geometry;
the regional path bends around closed walls. A corridor plan realizes that
regional passage. Room/door positions are generated from one room anchor and
catalog adjacency rules; outside destinations are explicit game-authored intent.

`Interiors.Rules[].Entrances` binds an exact connection ID, a room catalog port
and an optional outside endpoint port. The source port owns the opening center,
outward approach and width capacity. Inward approach depth and opening width are
declared by the interior rule. The outside leg remains outside declared room
floors; the inside leg stays in its exact room. Both directions and the entire
crossing pass common wall, content, width and terrain checks. Coincident endpoint/
approach positions do not fabricate zero-length path segments.

`MetadataInteriorPlanner.WithWalls` exposes the same generated wall solids for
shared route/network/corridor consumers. Generation, placement feedback and
saved replay use it. Interior connections and regional routes cannot both own
the same connection kind. Door width edits invalidate every physical consumer
while preserving logical topology when unchanged.

The first regional search exhausted a 100-million per-leg work limit. An exact
exclusion precheck now rejects blocked edges before terrain tracing. An optional
first-feasible search accelerator joins angularly varied endpoint-visible
candidates; bounded A* remains available. Neither changes acceptance geometry,
source content or the configured limits. Lowest-cost behavior retains its search
policy. The qualification record retains failed attempts and measured work.

Offline plot inspection also exposed an initially qualified 103–112m boundary
detour between destinations only 36m apart. The first-feasible owner now keeps
near/far representatives of visible angular sectors and tries shorter two-bend
joins before returning a one-bend incumbent. These examples independently require
at most 54m (1.5 times direct distance). That bound is a fixture acceptance test,
not an undocumented global route constraint or an optimality claim.

Current scope is single-floor convex room modules and explicit exterior bindings.
Multiple floors, movement between different ports on one destination, general
transition-domain inference and broad world composition remain open. A logical
progression witness alone does not prove arbitrary physical continuity; these
specific fixtures independently check every joined endpoint and full width.
Godot remains deferred.

Evidence: `docs/evidence/2026-10-28-entrance-travel-verification.json`.
