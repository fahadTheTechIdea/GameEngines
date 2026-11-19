const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const packets=['initial','moved'].map(name=>{const folder=`generated/network-population-${name}`;return {folder,world:read(`${folder}/world-metadata.json`),recipe:read(`${folder}/recipe.json`)};});
test('network population preserves its complete source program and responds to terrain',()=>{
 for(const {world,recipe} of packets){
  const source=world.PlacementPlan.Definition.SourceWorld;
  assert.equal(source.Nodes.length,8);assert.equal(world.Nodes.length,12);assert.equal(world.AccessPlan.Sites.length,11);
  for(const node of source.Nodes)assert.deepEqual(world.Nodes.find(n=>n.Id===node.Id),node);
  assert.equal(source.RoutePlan,null);assert.equal(world.RoutePlan,null);
  assert.deepEqual(world.NetworkPlan.Networks,source.NetworkPlan.Networks);
  assert.deepEqual(world.NetworkPlan.PreservedRuleIds,['journey']);assert.ok(world.NetworkPlan.PreservationWork>0);
  const plan=world.NetworkPlan.Networks[0].Plan;
  assert.equal(plan.Destinations.length,3);assert.equal(plan.Edges.length,2);assert.equal(plan.Analysis.Components.length,1);
  assert.deepEqual(plan.Edges.map(e=>e.Reason).sort(),['backbone','required']);
  const grammar=recipe.Placement.Bindings.find(b=>b.DemandId==='marker').Population;
  assert.equal(grammar.Path,null);assert.ok(plan.Edges.find(e=>e.Link.Id===grammar.Route.ConnectionId));
  assert.ok(recipe.PopulationSource.Recipe);assert.equal(recipe.Placement.SourceWorld,null);
 }
 const [a,b]=packets.map(p=>p.world);
 assert.notDeepEqual(a.NetworkPlan.Networks,b.NetworkPlan.Networks);
 assert.notDeepEqual(a.Nodes.filter(n=>n.Id.includes('/markers-')),b.Nodes.filter(n=>n.Id.includes('/markers-')));
 assert.deepEqual(a.Nodes.filter(n=>!n.Id.includes('/markers-')),b.Nodes.filter(n=>!n.Id.includes('/markers-')));
 assert.equal(fs.existsSync('generated/network-population-refused'),false);
});
test('schematic shows exact network paths and every physical member',()=>{
 for(const {world,folder} of packets){
  const view=Schematic.layout(world);assert.equal(view.boxes.length,12);assert.equal(view.boxes.filter(b=>b.physicalAccess).length,11);assert.equal(view.routes.length,4);
  for(const edge of world.NetworkPlan.Networks[0].Plan.Edges)for(const direction of ['Forward','Reverse']){
   const rendered=view.routes.find(r=>r.id===edge[direction].Id);
   assert.deepEqual(rendered.points,edge[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));assert.equal(rendered.width,.6);
  }
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 }
});
