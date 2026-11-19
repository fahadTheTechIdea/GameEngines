const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_WEIGHTED_ADAPTATION_ROOT||'generated/weighted-layout-adaptation-test-20261101';
const world=JSON.parse(fs.readFileSync(`${root}/world/world-metadata.json`,'utf8'));
test('actual alternate layout retains weight and complete physical program',()=>{
 const view=S.layout(world),adaptation=world.LayoutAdaptationPlan;
 assert.equal(view.boxes.length,7);assert.equal(world.AccessPlan.Sites.length,2);
 assert.deepEqual(adaptation.Attempts.map(a=>a.Accepted),[false,true]);
 assert.deepEqual(world.LocalLayoutPlan.GeneratedAreas[0].AreaPlan.RelativeWeights,[11,10]);
});
test('changing declared weight or adaptation witness refuses presentation',()=>{
 for(const mutate of [
  w=>w.LocalLayoutPlan.GeneratedAreas[0].AreaPlan.RelativeWeights[0]++,
  w=>w.LayoutAdaptationPlan.Attempts[0].Accepted=true,
  w=>w.LayoutAdaptationPlan.Attempts[1].Steps.rotation=0
 ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>S.layout(changed),/local layout|local repartition/);}
});
