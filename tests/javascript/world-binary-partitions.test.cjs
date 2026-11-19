const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
for(const folder of ['generated/binary-partition-published','generated/binary-partition-alternative']){
 const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
 test(`${folder}: engine cuts, complete content and analytical half-planes`,()=>{
  const view=Schematic.layout(world);assert.equal(view.boxes.length,7);assert.deepEqual(view.localLayouts,world.LocalLayoutPlan);
  const layout=world.LocalLayoutPlan.Definition.Layouts[0],seed=world.RegionPlan.Regions.find(r=>r.Id===layout.RegionId).Seed.Position;
  assert.equal(layout.Strategy,'BinaryPartition');assert.equal(world.PlacementPlan.Placement.Sites.length,2);
  assert.equal(layout.Partition.Cuts[0].Origin.X,2);assert.equal(layout.Partition.Cuts[0].NormalDegrees,0);
  for(const a of world.LocalLayoutPlan.Allocations){
   const sign=a.Id===layout.Partition.Cuts[0].NegativeId?-1:1;
   for(const c of a.Shape.Cells)for(const p of c.Vertices)assert.ok(sign*(p.X-seed.X-2)>=-1e-7);
  }
  assert.equal(world.LocalLayoutPlan.Areas[0].ResidualArea,0);
  assert.deepEqual(fs.readFileSync(`${folder}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 });
 test(`${folder}: malformed or geometrically contradictory cuts refuse presentation`,()=>{
  for(const mutate of [
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Partition.RootId='absent',
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Partition.Cuts[0].PositiveId=w.LocalLayoutPlan.Definition.Layouts[0].Partition.Cuts[0].NegativeId,
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Partition.Cuts[0].NormalDegrees=361,
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Partition.Cuts[0].Origin.X=10,
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Programs[0].Influence=1
  ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>Schematic.layout(changed),/local layout|local repartition/);}
 });
}
