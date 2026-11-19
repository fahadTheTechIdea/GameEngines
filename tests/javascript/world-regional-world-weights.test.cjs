const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_WEIGHTS_ROOT||'generated/regional-weight-world-test-20261101';
const world=JSON.parse(fs.readFileSync(`${root}/edited/world-regions.json`,'utf8'));
test('regional view retains each weighted region and physical program',()=>{
 const view=R.layout(world),p=world.Definition.OwnershipPlan;
 assert.equal(view.boxes.length,30);assert.equal(world.Connections.length,7);
 assert.deepEqual(p.Partition.RelativeWeights,[1,3,1,1,1,2]);
 assert.equal(world.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),18);
});
test('region weights and ownership witnesses must agree',()=>{
 for(const mutate of [
  w=>w.Definition.OwnershipPlan.Source.Regions[0].Weight=2,
  w=>w.Definition.OwnershipPlan.Partition.RelativeWeights[0]=2,
  w=>w.Definition.OwnershipPlan.Partition.Strata[0].Mass++
 ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>R.layout(changed),/Invalid regional|Invalid region/);}
});
