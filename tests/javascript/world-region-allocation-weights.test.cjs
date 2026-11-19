const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_REGION_WEIGHTS_ROOT||'generated/region-weight-final-20261101';
const world=JSON.parse(fs.readFileSync(`${root}/world/world-metadata.json`,'utf8'));
test('weighted regions retain the complete physical world and chosen weights',()=>{
 const view=S.layout(world),plan=S.generatedRegions(world);
 assert.equal(view.boxes.length,11);assert.equal(world.AccessPlan.Sites.length,9);
 assert.deepEqual(plan.IntentPlan.Partitions[0].AreaPlan.RelativeWeights,[1,2,3]);
});
test('changed weight or proof cannot be presented as the saved plan',()=>{
 for(const mutate of [
  p=>p.Request.Intents[0].AllocationWeight=0,
  p=>p.Request.Intents[0].AllocationWeight=2,
  p=>p.IntentPlan.Partitions[0].AreaPlan.RelativeWeights[0]=2,
  p=>p.IntentPlan.Partitions[0].AreaPlan.Strata[0].Mass++
 ]){
  const changed=structuredClone(world);mutate(changed.RegionPlan);
  assert.throws(()=>S.generatedRegions(changed),/Invalid region|Invalid generated region/);
 }
});
