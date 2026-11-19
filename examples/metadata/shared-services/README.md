# Shared services without geometry

This reusable game-authored example declares one provider, one relay and two consumers. Each consumer requires four `rest-points`. The provider offers ten; both delivery chains share a link limited to eight. Generation allocates eight at the source and eight on the trunk, while retaining both consumers.

`recipe.json` owns the explicit provider bindings and directed connection chains. `catalog.json` owns supply and requirement facts. These are abstract game units, not real infrastructure flows. No terrain, building, city, physical access or artwork is inferred.

Generate with the existing `generate-metadata` command and a new output directory. The reviewed packet is `generated/shared-services`; selecting a consumer shows **logical delivery only**, its provider and chain. Physical delivery is demonstrated separately in [physical-services](../physical-services/README.md).

See [shared-service API and limits](../../../docs/WORLD_METADATA.md#shared-service-delivery) and [verification](../../../docs/evidence/2026-10-27-world-services-verification.json). Missing requirements, incompatible sources, disconnected chains, provider cycles and shared capacity exhaustion reject publication.
