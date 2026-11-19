const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_SEMANTIC_ROOT;
if(!root)throw new Error('Set WORLD_REGIONAL_SEMANTIC_ROOT to the generated semantic test output');
const world=JSON.parse(fs.readFileSync(path.join(root,'world','world-regions.json'),'utf8'));
test('semantic network endpoints resolve to actual complete regional content',()=>{
 assert.equal(R.layout(world).boxes.length,20);
 assert.equal(world.NetworkPlan.Networks[0].ResolvedDestinations.length,4);
});
test('changed source selectors and forged resolved endpoints are rejected',()=>{
 for(const mutate of [
  w=>w.Definition.NetworkRules[0].Destinations[0].Endpoint.Selector.NodeKind='missing',
  w=>w.NetworkPlan.Networks[0].ResolvedDestinations[0].Endpoint.NodeId='forged',
  w=>w.NetworkPlan.Networks[0].ResolvedDestinations.pop()
 ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>R.layout(changed),/Invalid regional/);}
});
