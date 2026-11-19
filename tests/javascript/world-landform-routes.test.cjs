const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const packets=['north','south'].map(direction=>{
 const folder=`generated/landform-${direction}-pass`;
 return {direction,folder,world:JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`,'utf8'))};
});
test('moving only the generated pass preserves all complete source-sized compounds',()=>{
 const [a,b]=packets.map(p=>p.world);
 assert.deepEqual(a.Nodes,b.Nodes);assert.deepEqual(a.AccessPlan.Sites,b.AccessPlan.Sites);
 assert.equal(a.CatalogSha256,b.CatalogSha256);assert.notEqual(a.RecipeSha256,b.RecipeSha256);
 assert.notDeepEqual(a.Surface.HeightField.Heights,b.Surface.HeightField.Heights);
 assert.notDeepEqual(a.RoutePlan,b.RoutePlan);
 for(const {world} of packets){const view=Schematic.layout(world);assert.equal(view.boxes.length,9);assert.equal(view.boxes.filter(b=>b.physicalAccess).length,8);}
});
test('all six directional paths retain engine coordinates and width without waypoints',()=>{
 for(const {world} of packets){
  const view=Schematic.layout(world);assert.equal(view.routes.length,6);
  assert.ok(world.RoutePlan.Rules.every(r=>r.Search.Waypoints.length===0));
  for(const route of world.RoutePlan.Routes)for(const direction of ['Forward','Reverse']){
   const rendered=view.routes.find(r=>r.id===route.ConnectionId+'/'+direction.toLowerCase());
   assert.deepEqual(rendered.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));
   assert.equal(rendered.width,.6);assert.equal(route[direction].SearchModel,'terrain-feature-visibility-v1');
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
