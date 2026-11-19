const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_LOCAL_LAYOUT_PACKET||'generated/local-layouts',world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('mixed layout renderer preserves exact engine programs, geometry and content',()=>{
 const view=Schematic.layout(world),plan=view.localLayouts;
 assert.deepEqual(plan,world.LocalLayoutPlan);assert.equal(plan.Allocations.length,7);assert.equal(view.boxes.length,22);
 assert.deepEqual(plan.Definition.Layouts.map(l=>l.Strategy),['PowerDiagram','OrientedGrid']);
 for(const a of plan.Areas)assert.ok(Math.abs(a.RegionArea-a.AllocatedArea-a.ResidualArea)<1e-7);
 for(const entries of view.suitability.values())for(const e of entries)if(e.Placement?.AllocationId)
  assert.ok(plan.Allocations.some(a=>a.Id===e.Placement.AllocationId&&a.RegionId===e.Placement.RegionId));
 assert.equal(plan.Interfaces.length,2);
});
test('missing allocations, broken accounting and invalid required spans refuse display',()=>{
 for(const mutate of [w=>w.LocalLayoutPlan=null,w=>w.LocalLayoutPlan.Allocations.pop(),w=>w.LocalLayoutPlan.Allocations[0].Shape.Cells=[],
  w=>w.LocalLayoutPlan.Allocations[0].Shape.Area+=1,w=>w.LocalLayoutPlan.Areas[0].ResidualArea+=1,
  w=>w.LocalLayoutPlan.Allocations[0].RegionId='missing',w=>w.LocalLayoutPlan.Allocations[0].Program.Anchor.X+=1,
  w=>w.LocalLayoutPlan.Interfaces=[],w=>w.LocalLayoutPlan.Interfaces[0].SelectedSpan.To.X+=1,
  w=>w.LocalLayoutPlan.Interfaces[0].Position.X+=1,w=>w.LocalLayoutPlan.RegionPlanSha256='invalid',
  w=>w.PlacementPlan.Placement.Request.Demands[0].AllocationId='missing']){
  const copy=structuredClone(world);mutate(copy);assert.throws(()=>Schematic.layout(copy),/local layout/);
 }
});
test('unselected region, capacity and source-area viewers remain usable',()=>{
 for(const file of ['generated/terrain-regions/world-metadata.json','generated/corridor-capacity/world-metadata.json','generated/resource-area/world-metadata.json'])
  assert.equal(Schematic.layout(JSON.parse(fs.readFileSync(file))).localLayouts,null);
});
test('current packet embeds exact source renderer and engine metadata',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
