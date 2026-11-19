# Spiral-facing physical program

These game-independent recipes place four solid sites along the same authored
spiral and connect two of them with a bidirectional journey. One faces each
site along the continuous spiral tangent; the other turns it right relative
to that tangent. `Against` and `Left` are also supported. The original source
geometry, terrain, access ports and required routes remain enforced.

The direction follows increasing member index, including on contracting and
clockwise spirals. Boundary-only `Inside` and `Outside` do not apply. Inputs
can be used with `WorldMetadataGenerator` or `generate-metadata`. This fixture
is not a complete game, asset set or runtime integration.
