# Weighted regional world

This game-independent source requests six complete programs on one irregular
terrain surface. The authored regional `Weight` values assign relative
ownership shares of 1:3:1:1:1:2 in canonical region order, while retaining the
same minimum area, 30 nodes, 18 physical sites and seven bidirectional journeys.

The `generate-derived-regional-world` CLI operation and the C# regional planner
and generator accept `request.json`. With no ownership density field, these are
relative area shares. With a density field they are mass shares. If a region
declares a capacity policy, its effective weight is its authored weight times
its measured required area. All minimum, suitability, content and route constraints
remain hard, so a requested distribution can fail.

This regional `Weight` is separate from `AllocationWeight` on intents sharing
one suitable component inside a region plan. This is an engine fixture, not a
complete game, visual asset set or runtime integration.
