const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_SPIRAL_ROOT||'generated/spiral-layout-final-20261101';
const world=JSON.parse(fs.readFileSync(`${root}/world/world-metadata.json`,'utf8'));
test('schematic displays the exact physical spiral sites and required journeys',()=>{
 const view=S.layout(world);
 assert.equal(view.boxes.length,5);assert.equal(world.AccessPlan.Sites.length,4);
 for(const [id,x,y] of [[0,60,50],[1,50,62],[2,36,50],[3,50,34]]){
  const node=world.Nodes.find(n=>n.Id===`spiral-world/sites-${id}`);
  assert.ok(Math.abs(node.Position[0]-x)<1e-7&&Math.abs(node.Position[1]-y)<1e-7);
 }
 assert.equal(world.RoutePlan.Routes[0].Forward.Status,'Found');assert.equal(world.RoutePlan.Routes[0].Reverse.Status,'Found');
});
test('unfaced spiral source, compiled bindings and positions must agree',()=>{
 for(const mutate of [
  w=>w.PlacementPlan.TemplateDefinition.Bindings[0].Population.Spiral.StartRadius++,
  w=>delete w.PlacementPlan.TemplateDefinition.Bindings[0].Population.Spiral,
  w=>w.PlacementPlan.Definition.Bindings[0].Population.Spiral.RadiusStep++,
  w=>w.PlacementPlan.Definition.Request.Anchors[0].Position.X++,
  w=>w.Nodes.find(n=>n.Id==='spiral-world/sites-0').Position[0]++
 ]){const changed=structuredClone(world);mutate(changed);assert.throws(()=>S.layout(changed),/spiral population|point suitability/);}
});
