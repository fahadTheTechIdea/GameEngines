const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
for(const name of ['positive','negative','diagonal','negative-steps']){
 const folder=`${process.env.WORLD_PARTITION_ADAPTATION_ROOT||'generated/cut-adaptation'}/${name}`;
 const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
 test(`${name}: bounded source settings retain complete physical program`,()=>{
  const view=Schematic.layout(world),proof=world.LayoutAdaptationPlan,source=proof.OriginalLayouts.Layouts[0],selected=world.LocalLayoutPlan.Definition.Layouts[0];
  assert.equal(view.boxes.length,7);assert.equal(world.PlacementPlan.Placement.Sites.length,2);
  assert.equal(proof.Attempts.length,3);assert.deepEqual(proof.Attempts.map(a=>a.Accepted),[false,false,true]);
  assert.deepEqual(proof.Attempts.map(a=>a.Steps.shift),name==='negative-steps'?[0,-1,-2]:[0,1,2]);
  assert.ok(proof.Attempts.slice(0,2).every(a=>a.Rejection.startsWith('METADATA_PLACEMENT_UNRESOLVED')));
  assert.deepEqual(source.Programs,selected.Programs);assert.equal(source.Partition.Cuts[0].NormalDegrees,selected.Partition.Cuts[0].NormalDegrees);
  assert.equal(source.Partition.Cuts[0].NegativeId,selected.Partition.Cuts[0].NegativeId);assert.equal(source.Partition.Cuts[0].PositiveId,selected.Partition.Cuts[0].PositiveId);
  const cut=selected.Partition.Cuts[0];
  assert.ok(Math.abs(cut.Origin.X-(name==='diagonal'?1.969615506024416:2))<1e-8);
  assert.ok(Math.abs(cut.Origin.Y-(name==='diagonal'?.34729635533386066:0))<1e-8);
  const seed=world.RegionPlan.Regions.find(r=>r.Id===selected.RegionId).Seed.Position;
  for(const a of world.LocalLayoutPlan.Allocations)for(const cell of a.Shape.Cells)for(const p of cell.Vertices){
   const nx=name==='diagonal'?.984807753012208:name==='negative'?-1:1,ny=name==='diagonal'?.17364817766693033:0;
   const side=a.Id===cut.NegativeId?-1:1;
   assert.ok(side*(nx*(p.X-seed.X-cut.Origin.X)+ny*(p.Y-seed.Y-cut.Origin.Y))>=-1e-7);
  }
  assert.equal(world.LocalLayoutPlan.Areas[0].ResidualArea,0);
  assert.deepEqual(fs.readFileSync(`${folder}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 });
 test(`${name}: altered target, settings, bounds or selected geometry refuses`,()=>{
  for(const mutate of [
   w=>w.LayoutAdaptationPlan.Policy.Dials[0].CutId='missing',
   w=>w.LayoutAdaptationPlan.Policy.Dials[0].CutId=null,
   w=>w.LayoutAdaptationPlan.Policy.Dials[0].ProgramId='west-a',
   w=>w.LayoutAdaptationPlan.Policy.Dials[0].Step=1e8,
   w=>w.LayoutAdaptationPlan.Attempts[2].Steps.shift=0,
   w=>w.LocalLayoutPlan.Definition.Layouts[0].Partition.Cuts[0].Origin.X+=.5,
   w=>w.LayoutAdaptationPlan.Policy.Dials.push({...w.LayoutAdaptationPlan.Policy.Dials[0],Id:'duplicate'}),
   w=>w.LayoutAdaptationPlan.OriginalLayouts.Layouts[0].Partition.Cuts[0].NormalDegrees=20
  ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>Schematic.layout(changed),/local layout|local repartition/);}
 });
}
