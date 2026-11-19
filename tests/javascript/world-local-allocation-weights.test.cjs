const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_LOCAL_WEIGHTS_ROOT||'generated/local-weight-final-20261101';
const world=JSON.parse(fs.readFileSync(`${root}/world/world-metadata.json`,'utf8'));
test('weighted local programs retain complete source content and routes',()=>{
 const view=S.layout(world),plan=world.LocalLayoutPlan,area=plan.GeneratedAreas[0].AreaPlan;
 assert.equal(view.boxes.length,7);assert.equal(world.PlacementPlan.Placement.Sites.length,2);
 assert.deepEqual(area.RelativeWeights,[11,10]);assert.equal(world.RoutePlan.Routes.length,1);
});
test('changed weight or weighted witness is rejected',()=>{
 for(const mutate of [
  w=>w.LocalLayoutPlan.Definition.Layouts[0].ProgramIntents[0].AllocationWeight=0,
  w=>w.LocalLayoutPlan.Definition.Layouts[0].ProgramIntents[0].AllocationWeight=12,
  w=>w.LocalLayoutPlan.GeneratedAreas[0].AreaPlan.RelativeWeights[0]=12,
  w=>w.LocalLayoutPlan.GeneratedAreas[0].AreaPlan.Strata[0].Mass++
 ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>S.layout(changed),/local layout|local repartition/);}
});
