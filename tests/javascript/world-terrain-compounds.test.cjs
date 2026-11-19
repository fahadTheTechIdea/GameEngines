const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const packets=['north','south'].map(direction=>{
 const folder=`generated/terrain-compounds-${direction}`;
 return {direction,folder,world:JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`))};
});
test('only terrain changes; complete physical content and source identities remain fixed',()=>{
 const [a,b]=packets.map(p=>p.world);
 assert.deepEqual(a.Nodes,b.Nodes);assert.deepEqual(a.AccessPlan.Sites,b.AccessPlan.Sites);assert.equal(a.CatalogSha256,b.CatalogSha256);
 assert.notEqual(a.RecipeSha256,b.RecipeSha256);assert.notDeepEqual(a.RoutePlan,b.RoutePlan);
 for(const {world} of packets){const view=Schematic.layout(world);assert.equal(view.boxes.length,9);assert.equal(view.boxes.filter(b=>b.physicalAccess).length,8);}
});
test('terrain-derived shared crossings and all physical routes use exact engine coordinates',()=>{
 for(const {direction,world} of packets){
  const view=Schematic.layout(world);assert.equal(view.interfaceRoutes[0].Center.X,22);assert.equal(view.interfaceRoutes[0].Center.Y,direction==='north'?14:6);
  assert.equal(world.RegionPlan.BoundaryArea,784);assert.equal(world.RegionPlan.SuitableArea,776);assert.equal(world.RegionPlan.IneligibleArea,8);
  assert.equal(view.routes.length,6);
  for(const route of world.RoutePlan.Routes)for(const d of ['Forward','Reverse']){
   assert.deepEqual(view.routes.find(r=>r.id===route.ConnectionId+'/'+d.toLowerCase()).points,route[d].Path.Points.map(p=>({x:p.X,y:p.Y})));
  }
 }
});
test('both exported packets embed current renderer and complete metadata',()=>{
 for(const {folder,world} of packets){
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
 }
});
