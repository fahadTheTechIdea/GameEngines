const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
for(const name of ['direct','alternative']){
 const folder=`${process.env.WORLD_ANCHOR_PARTITIONS_ROOT||'generated/anchor-partitions'}/${name}`,world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
 test(`${name}: derived tree and exact physical geometry retain source provenance`,()=>{
  const view=Schematic.layout(world),plan=world.LocalLayoutPlan,source=plan.Definition.Layouts[0];
  assert.equal(view.boxes.length,7);assert.equal(view.interfaceRoutes.length,1);assert.equal(world.PlacementPlan.Placement.Sites.length,2);
  assert.equal(source.Strategy,'AnchorBisection');assert.equal(source.Partition,null);
  assert.equal(plan.GeneratedPartitions.length,1);assert.equal(plan.GeneratedPartitions[0].LayoutId,source.Id);
  const tree=plan.GeneratedPartitions[0].Partition;assert.equal(tree.Cuts.length,1);assert.equal(tree.RootId,tree.Cuts[0].Id);
  const cut=tree.Cuts[0];assert.deepEqual(cut.Origin,{X:2,Y:0});assert.equal(cut.NormalDegrees,0);
  assert.equal(cut.NegativeId,'west-a');assert.equal(cut.PositiveId,'west-b');
  assert.equal(plan.Allocations.find(a=>a.Id==='west-a').Shape.Area,440);assert.equal(plan.Allocations.find(a=>a.Id==='west-b').Shape.Area,360);
  assert.equal(plan.Areas[0].ResidualArea,0);
  if(name==='alternative'){assert.equal(world.LayoutAdaptationPlan.Attempts.length,2);assert.deepEqual(world.LayoutAdaptationPlan.Attempts.map(a=>a.Accepted),[false,true]);}
  assert.deepEqual(fs.readFileSync(`${folder}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 });
 test(`${name}: missing, malformed or contradictory generated trees refuse display`,()=>{
  for(const mutate of [
   w=>w.LocalLayoutPlan.GeneratedPartitions=[],
   w=>w.LocalLayoutPlan.GeneratedPartitions[0].LayoutId='absent',
   w=>w.LocalLayoutPlan.GeneratedPartitions.push(structuredClone(w.LocalLayoutPlan.GeneratedPartitions[0])),
   w=>w.LocalLayoutPlan.GeneratedPartitions[0].Partition.Cuts[0].Origin.X+=5,
   w=>w.LocalLayoutPlan.GeneratedPartitions[0].Partition.Cuts[0].PositiveId='west-a',
   w=>w.LocalLayoutPlan.GeneratedPartitions[0].Partition.Cuts[0].Purpose='',
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Partition={RootId:'west-a',Cuts:[]},
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Programs[0].Anchor.X+=1
  ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>Schematic.layout(changed),/local layout|local repartition/);}
 });
}
