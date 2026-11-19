const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_REGION_SPANS_PACKET||'generated/region-alternate-cli-world';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`,'utf8'));
test('blocked first span selects a separate qualified span around a hole',()=>{
 const view=Schematic.layout(world),proof=world.RegionPlan.IntentPlan.Relations[0],route=view.interfaceRoutes[0];
 assert.equal(proof.SharedSegments.length,2);assert.notDeepEqual(route.Span,proof.SharedSpan);
 assert.deepEqual(route.Span,proof.SharedSegments[1]);assert.deepEqual(route.Attempts.map(a=>a.Accepted),[false,true]);
 assert.equal(world.Nodes.length,7);assert.ok(route.Forward&&route.Reverse);
});
test('missing, invented and mismatched span witnesses refuse display',()=>{
 for(const mutate of [w=>w.RegionPlan.IntentPlan.Relations[0].SharedSegments.pop(),
  w=>w.RegionPlan.IntentPlan.Relations[0].SharedSegments[1].From.X++,
  w=>w.RoutePlan.Routes[0].Interface.Span.From.X++,w=>delete w.RegionPlan.IntentPlan.Relations[0].SharedSegments]){
  const copy=structuredClone(world);mutate(copy);assert.throws(()=>Schematic.layout(copy));
 }
});
