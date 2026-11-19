const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_SPIRAL_TERRAIN_ROOT||'generated/spiral-terrain-final-20261101';
for(const name of ['flat','slope','redesigned']){
 const world=JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
 test(`${name}: terrain-aware spiral world preserves source geometry and travel`,()=>{
  const view=S.layout(world);assert.equal(view.boxes.length,5);assert.equal(world.AccessPlan.Sites.length,4);
  assert.equal(world.RoutePlan.Routes[0].Forward.Status,'Found');assert.equal(world.RoutePlan.Routes[0].Reverse.Status,'Found');
  assert.equal(world.Surface.Terrain.GradientX,name==='flat'?0:.02);
  if(name==='redesigned'){
   assert.equal(world.Surface.Exclusions.length,1);
   assert.deepEqual(world.PlacementPlan.TemplateDefinition.Bindings[0].Population.Spiral.Center,[40,40]);
  }
 });
 test(`${name}: modified spiral source cannot silently change the saved plan`,()=>{
  const changed=structuredClone(world);changed.PlacementPlan.TemplateDefinition.Bindings[0].Population.Spiral.StartRadius++;
  assert.throws(()=>S.layout(changed),/spiral population/);
 });
}
