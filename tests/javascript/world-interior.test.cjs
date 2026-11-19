const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/interior-journey',world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`,'utf8'));
test('all physical room, corridor, opening and content polygons survive rendering',()=>{
 const view=Schematic.layout(world);
 assert.equal(view.boxes.length,13);assert.equal(world.AccessPlan.Sites.length,12);
 assert.equal(world.AccessPlan.Sites.filter(s=>s.Occupancy==='Solid').length,3);
 assert.equal(world.PlacementPlan.Definition.Request.AllowBoundaryContact,true);
 for(const site of world.AccessPlan.Sites)assert.deepEqual(view.boxes.find(b=>b.id===site.NodeId).physicalAccess,site);
});
test('all twelve directional paths use exact saved meter coordinates',()=>{
 const view=Schematic.layout(world);assert.equal(view.routes.length,12);
 for(const route of world.RoutePlan.Routes)for(const direction of ['Forward','Reverse']){
  const rendered=view.routes.find(r=>r.id===route.ConnectionId+'/'+direction.toLowerCase());
  assert.deepEqual(rendered.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));
  assert.equal(rendered.width,.6);
 }
 const witness=world.ProgressionPlan.Results[0].Paths[0];
 const expected=[[5,5],[9,5],[15,5],[19,5],[19,9],[19,13],[19,17]];
 witness.ConnectionIds.forEach((id,i)=>{
  const points=world.RoutePlan.Routes.find(r=>r.ConnectionId===id).Forward.Path.Points;
  assert.deepEqual(points.map(p=>[p.X,p.Y]),[expected[i],expected[i+1]]);
 });
});
test('source disagreement refuses rendering',()=>{
 const copy=structuredClone(world);copy.AccessPlan.Sites.find(s=>s.Occupancy==='Solid').Footprint.Vertices[0].X+=1;
 assert.throws(()=>Schematic.layout(copy));
});
test('packet embeds current renderer and complete world',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
