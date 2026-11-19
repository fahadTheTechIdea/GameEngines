const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const packets=['initial','moved'].map(name=>{
 const folder=`generated/route-population-${name}`;return {folder,world:read(`${folder}/world-metadata.json`),recipe:read(`${folder}/recipe.json`)};
});
test('complete population preserves generated base members and paths in both terrain variants',()=>{
 for(const {world,recipe} of packets){
  const source=world.PlacementPlan.Definition.SourceWorld;
  assert.equal(source.Nodes.length,8);assert.equal(world.Nodes.length,12);assert.equal(world.AccessPlan.Sites.length,11);
  for(const node of source.Nodes)assert.deepEqual(world.Nodes.find(n=>n.Id===node.Id),node);
  assert.deepEqual(world.RoutePlan.Routes,source.RoutePlan.Routes);
  assert.equal(world.RoutePlan.PreservedConnectionIds.length,2);assert.ok(world.RoutePlan.PreservationWork>0);
  const grammar=recipe.Placement.Bindings.find(b=>b.DemandId==='marker').Population;
  assert.equal(grammar.Path,null);assert.equal(grammar.Route.ConnectionId,'population/explicit/approach');
  assert.ok(recipe.PopulationSource.Recipe);assert.equal(recipe.Placement.SourceWorld,null);
 }
 const [a,b]=packets.map(p=>p.world);
 assert.notDeepEqual(a.RoutePlan.Routes,b.RoutePlan.Routes);
 assert.notDeepEqual(a.Nodes.filter(n=>n.Id.includes('/markers-')),b.Nodes.filter(n=>n.Id.includes('/markers-')));
 assert.deepEqual(a.Nodes.filter(n=>!n.Id.includes('/markers-')),b.Nodes.filter(n=>!n.Id.includes('/markers-')));
 assert.equal(fs.existsSync('generated/route-population-refused'),false);
});
test('schematic renders all source-sized sites and retained route geometry exactly',()=>{
 for(const {world,folder} of packets){
  const view=Schematic.layout(world);assert.equal(view.boxes.length,12);assert.equal(view.boxes.filter(b=>b.physicalAccess).length,11);assert.equal(view.routes.length,4);
  for(const route of world.RoutePlan.Routes)for(const direction of ['Forward','Reverse']){
   const rendered=view.routes.find(r=>r.id===route.ConnectionId+'/'+direction.toLowerCase());
   assert.deepEqual(rendered.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));assert.equal(rendered.width,.6);
  }
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 }
});
