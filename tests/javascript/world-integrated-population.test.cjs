const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const terrain=process.env.WORLD_INTEGRATED_TERRAIN_ROOT||'generated/integrated-population-test-terrain-20261101';
const nested=process.env.WORLD_INTEGRATED_NESTED_ROOT||'generated/integrated-population-test-nested-20261101';
for(const [name,root,folder,count] of [['spiral',terrain,'integrated',5],['original',terrain,'integrated-original',5],['path',terrain,'integrated-path',5],['program',nested,'ProgramIntegrated',11]]){
 const file=`${root}/${folder}/world-metadata.json`;
 test(`${name}: integrated population displays all sources and travel`,()=>{
  const world=JSON.parse(fs.readFileSync(file,'utf8')),view=S.layout(world);
  assert.equal(view.boxes.length,count);assert.equal(world.AccessPlan.Sites.length,count-1);
  assert.equal(world.RoutePlan.Routes[0].Forward.Status,'Found');assert.equal(world.RoutePlan.Routes[0].Reverse.Status,'Found');
 });
 test(`${name}: altered attempt or compiled population is rejected`,()=>{
  const world=JSON.parse(fs.readFileSync(file,'utf8'));
  const altered=structuredClone(world);altered.PlacementPlan.PopulationAdaptation.Work++;
  assert.throws(()=>S.layout(altered),/population adaptation/);
  const changed=structuredClone(world),plan=changed.PlacementPlan;
  if(name==='program')plan.Definition.NestedStages[0].Bindings[0].Population.Spiral.StartRadius++;
  else if(name==='spiral'||name==='original')plan.Definition.Bindings[0].Population.Spiral.StartRadius++;
  else plan.Definition.Bindings[0].Population.Path.Spacing++;
  assert.throws(()=>S.layout(changed),/population adaptation/);
 });
}
