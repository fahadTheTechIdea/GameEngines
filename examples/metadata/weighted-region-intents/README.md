# Weighted region intents

This game-independent example requests three regions with the same hard minimum
area but allocation weights of `1`, `2` and `3`. On its uniform terrain the
regions receive those relative shares while retaining all nested structures and
required routes. The protected source hole is excluded from the allocation.

`AllocationWeight` is a desired relative share, not a guarantee that a request
is feasible. The engine still enforces each region's minimum area, clearance,
content and connections; impossible requirements fail. If a density field is
declared, the share applies to density-weighted mass rather than raw area. An
omitted weight uses that intent's minimum area as its weight.

The files are inputs to `WorldMetadataGenerator` and `generate-metadata`. This
is a framework example, not a complete game or runtime integration.
