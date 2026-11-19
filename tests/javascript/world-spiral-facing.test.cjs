const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_SPIRAL_FACING_ROOT||'generated/spiral-facing-final-20261101';
for(const name of ['Along','Against','Left','Right','Swapped','Contracting','Nested']){
 const mode=['Swapped','Contracting','Nested'].includes(name)?'Along':name;
 const world=JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
 test(`${name}: physical spiral, heading and route witnesses`,()=>{
  assert.equal(S.layout(world).boxes.length,name==='Nested'?6:5);
  assert.equal(world.AccessPlan.Sites.length,name==='Nested'?5:4);
  assert.equal(world.PlacementPlan.PopulationOrientations[0].Facing,mode);
 });
 test(`${name}: changed source, position or heading refuses presentation`,()=>{
  for(const mutate of [
   w=>[...w.PlacementPlan.Definition.Bindings,...(w.PlacementPlan.Definition.NestedStages||[]).flatMap(s=>s.Bindings)].filter(b=>b.Population?.Spiral).forEach(b=>b.Population.Spiral.Center[0]++),
   w=>[...w.PlacementPlan.Definition.Bindings,...(w.PlacementPlan.Definition.NestedStages||[]).flatMap(s=>s.Bindings)].filter(b=>b.Population?.Spiral).forEach(b=>b.Population.Spiral.AngleStepDegrees+=10),
   w=>w.Nodes.find(n=>n.Id===w.PlacementPlan.PopulationOrientations[0].NodeIds[0]).Position[0]++,
   w=>w.PlacementPlan.PopulationOrientations[0].TangentDegrees[0]++,
   w=>w.PlacementPlan.PopulationOrientations[0].Rotations[0]++
  ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>S.layout(changed),/population orientation|point suitability|spiral population/);}
 });
}
