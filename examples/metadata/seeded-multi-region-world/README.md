# Seeded multi-region world

These game-neutral inputs differ only in the seed for the declared region
partition angles. The same irregular terrain and game-authored catalog programs
produce six regions containing 30 metadata nodes and 18 physical sites, linked
by seven bidirectional journeys. In the tested pair all six ownership boundaries
and all seven forward route paths changed while the source content and terrain
remained the same.

`world-4-request.json` exercises a route that needs a second bounded incumbent
probe. It retains the original route candidate and work limits; the route still
checks the complete set of candidates that could improve the incumbent.

Pass either request to the C# `MetadataRegionalOwnershipPlanner` and
`MetadataRegionalWorldGenerator`, or to the `generate-derived-regional-world`
CLI operation. The seed orders the explicitly declared candidate split angles;
the engine checks terrain and complete physical content. Different seeds are not
guaranteed to succeed or produce distinct shapes. The input is a framework
fixture, not a complete game or a promise of organic settlement design.

Art and engine-specific runtime integration are separate downstream work.
