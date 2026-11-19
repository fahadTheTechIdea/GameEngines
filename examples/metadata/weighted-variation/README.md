# Weighted catalog variation

This framework example gives one authored marker variant twelve tickets and two
others one ticket each. The same seed and node identities produce the same choices.
Every selected marker keeps its complete nested component, original footprint and
required placement constraints. Weights influence frequency across identities;
they do not promise exact counts in a small world.

The input is engine-independent metadata. Use `WorldMetadataGenerator` or the
`generate-metadata` CLI, and `ReadPublishedAsync` or `verify-metadata` to replay
the published source package. The example declares no game, artwork or Godot.
