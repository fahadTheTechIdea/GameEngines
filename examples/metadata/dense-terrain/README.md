# Full-resolution terrain metadata reference

This framework input uses the maximum supported 513×513 terrain lattice: 263169
original elevation samples on a 512m×512m domain. The lattice samples terrain;
it does not prescribe world layout, roads, blocks or storage cell positions.

The four exact basin slopes bound an altitude-128 diamond of area 32768m². The
region requires that complete area with the original 32-face and 200M-work caps.
Shared convex-face merging retains four affine pieces without resampling or
altering any source value. Two required encounter destinations follow the saved
relative layout grammar, and a land-only radius-qualified path connects them.
Storage uses separately declared 64m chunks, which do not impose roads.

The actual `generate-metadata` packet is qualified separately from the component
API cases. The session counterfactual moves the basin one meter left while
preserving the 32768m² minimum, all destinations and original limits. Raising the
edited bed by one meter then leaves insufficient eligible area and must refuse;
failed proposals preserve the accepted cache, report and world. Numeric buffer
changes retain full source data and before/after hashes; individual protected
samples remain enforceable.

The dense component matrix also covers a protected void and an affine sampled
scalar with a clamp. Noncoplanar terrain or scalar complexity can still exhaust
the finite representation budget. This is a terrain/persistence scale reference,
not a complete game-content example or proof of arbitrary dense-world support.
Godot and artwork remain deferred. See the current master tracker and evidence.
