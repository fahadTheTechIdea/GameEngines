const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const packets=['initial','moved','local'].map(name=>{const folder=`generated/region-population-${name}`;return {name,folder,world:read(`${folder}/world-metadata.json`),recipe:read(`${folder}/recipe.json`)};});
test('complete source and region-derived population survive publication',()=>{
 for(const {world,recipe,name} of packets){
  const source=world.PlacementPlan.Definition.SourceWorld;
  assert.equal(world.Nodes.length,7);assert.equal(source.Nodes.length,3);assert.equal(world.AccessPlan.Sites.length,6);
  for(const node of source.Nodes)assert.deepEqual(world.Nodes.find(n=>n.Id===node.Id),node);
  assert.deepEqual(world.RegionPlan,source.RegionPlan);assert.deepEqual(world.LocalLayoutPlan,source.LocalLayoutPlan);
  assert.equal(world.RoutePlan,null);assert.equal(world.NetworkPlan,null);
  const grammar=recipe.Placement.Bindings.find(b=>b.DemandId==='resources').Population;
  assert.equal(grammar.Path,null);assert.equal(grammar.Boundary,null);assert.equal(grammar.Route,null);
  assert.equal(grammar.Region.RegionId,'west');assert.equal(grammar.Region.AllocationId,name==='local'?'reserve':null);
  for(const d of world.PlacementPlan.Placement.Request.Demands.filter(d=>d.Id.startsWith('resources-'))){
   assert.equal(d.RegionId,'west');assert.equal(d.AllocationId,name==='local'?'reserve':null);
  }
 }
 assert.equal(fs.existsSync('generated/region-population-refused'),false);
});
test('river edit moves only dependent resources by the analytical displacement',()=>{
 const [a,b]=packets.map(p=>p.world);
 assert.notDeepEqual(a.RegionPlan,b.RegionPlan);
 for(const n of a.Nodes){const moved=b.Nodes.find(m=>m.Id===n.Id);
  if(n.Id.includes('/resources-')){assert.ok(Math.abs(moved.Position[0]-n.Position[0]-5)<1e-7);assert.ok(Math.abs(moved.Position[1]-n.Position[1])<1e-7);}
  else assert.deepEqual(moved,n);
 }
});
test('renderer retains exact region, local-allocation and physical geometry',()=>{
 for(const {world,folder} of packets){
  const view=Schematic.layout(world);
  assert.equal(view.boxes.length,7);assert.equal(view.boxes.filter(b=>b.physicalAccess).length,6);assert.equal(view.routes.length,0);
  assert.deepEqual(view.generatedRegions,world.RegionPlan);assert.deepEqual(view.localLayouts,world.LocalLayoutPlan);
  for(const site of world.AccessPlan.Sites)assert.deepEqual(view.boxes.find(b=>b.id===site.NodeId).physicalAccess,site);
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 }
});
