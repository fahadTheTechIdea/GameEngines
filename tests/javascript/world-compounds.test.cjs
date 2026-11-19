const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_COMPOUND_PACKET||'generated/expedition-outposts',world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('all physical constituents preserve hierarchy and engine polygons',()=>{
 const view=Schematic.layout(world);
 assert.equal(view.boxes.length,9);
 const sites=view.boxes.filter(b=>b.physicalAccess);assert.equal(sites.length,8);
 for(const b of sites){
  assert.deepEqual(b.physicalAccess,world.AccessPlan.Sites.find(s=>s.NodeId===b.id));
  assert.deepEqual(b.node.Position,world.Nodes.find(n=>n.Id===b.id).Position);
 }
 assert.equal(sites.filter(b=>b.physicalAccess.Occupancy==='Solid').length,4);
 assert.equal(sites.filter(b=>b.node.Kind==='equipment'&&b.depth===3).length,2);
});
test('nested endpoints and all directional routes remain exact',()=>{
 const view=Schematic.layout(world);assert.equal(view.routes.length,6);assert.equal(view.interfaceRoutes.length,1);
 assert.equal(view.interfaceRoutes[0].FromDomainId,'west-a');assert.equal(view.interfaceRoutes[0].ToDomainId,'west-b');
 for(const route of world.RoutePlan.Routes)for(const direction of ['Forward','Reverse']){
  const rendered=view.routes.find(r=>r.id===route.ConnectionId+'/'+direction.toLowerCase());
  assert.deepEqual(rendered.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));assert.equal(rendered.width,.6);
 }
 assert.deepEqual(view.layoutAdaptation.Attempts.map(a=>a.Accepted),[false,true]);
});
test('physical source disagreement refuses rendering',()=>{
 const copy=structuredClone(world);copy.AccessPlan.Sites.find(s=>s.Occupancy==='Solid').Footprint.Vertices[0].X+=1;
 assert.throws(()=>Schematic.layout(copy));
});
test('export embeds current renderer and complete saved metadata',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
});
