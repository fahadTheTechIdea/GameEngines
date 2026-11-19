const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.ENTRANCE_TRAVEL_PACKET||'generated/exterior-interior-travel-accepted';
for(const name of ['regional-route','rotated-route','regional-network']){
 const read=()=>JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
 test(`${name}: whole outside/inside packet retains every wall, entrance and physical route`,()=>{
  const world=read(),view=Schematic.layout(world),assembly=world.InteriorPlan.Results[0];
  assert.equal(world.Nodes.length,15);assert.equal(world.AccessPlan.Sites.length,12);
  assert.equal(assembly.Walls.length,35);assert.equal(assembly.Openings.length,6);
  assert.equal(view.routes.length,14);assert.deepEqual(view.interiorAssemblies,[assembly]);
  const entry=assembly.Openings.find(o=>o.Entrance);
  assert.equal(entry.ConnectionId,'generated-interior/explicit/entry');
  const port=world.AccessPlan.Sites.find(s=>s.NodeId==='generated-interior/rooms-0').Ports[0];
  assert.deepEqual(entry.FromApproach,port.ApproachPosition);
  assert.ok(Math.abs((entry.From.X+entry.To.X)/2-port.BoundaryPosition.X)<1e-8);
  assert.ok(Math.abs((entry.From.Y+entry.To.Y)/2-port.BoundaryPosition.Y)<1e-8);
  const routes=[...assembly.Routes,...(world.RoutePlan?.Routes||[]),...(world.NetworkPlan?.Networks||[]).flatMap(n=>n.Plan.Edges.map(e=>({ConnectionId:e.Link.Id,Forward:e.Forward,Reverse:e.Reverse})))];
  for(const route of routes)for(const direction of ['Forward','Reverse']){
   const shown=view.routes.find(r=>r.id===route[direction].Id);
   assert.deepEqual(shown.points,route[direction].Path.Points.map(p=>({x:p.X,y:p.Y})));
   assert.equal(shown.width,.4);
  }
  const links=new Map(world.Connections.map(c=>[c.Id,c])),physical=new Map(routes.map(r=>[r.ConnectionId,r]));
  let current='generated-interior/trail-1',last=null;
  for(const id of world.ProgressionPlan.Results[0].Paths[0].ConnectionIds){
   const link=links.get(id),forward=current===link.FromId,path=physical.get(id)[forward?'Forward':'Reverse'].Path.Points;
   if(last)assert.deepEqual(path[0],last);
   last=path.at(-1);current=forward?link.ToId:link.FromId;
  }
  assert.equal(current,'generated-interior/rooms-5');
  assert.equal(world.CorridorPlan.Groups.length,1);
  const regional=world.RoutePlan?.Routes[0].Forward||world.NetworkPlan.Networks[0].Plan.Edges[0].Forward;
  assert.ok(regional.Cost.Distance<=54,'regional journey respects the fixture quality bound');
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${root}/${name}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
  assert.equal(fs.readFileSync(`${root}/${name}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 });
 test(`${name}: missing entrance binding and stale endpoint refuse`,()=>{
  const missing=read();missing.InteriorPlan.Definition.Rules[0].Entrances=[];
  assert.throws(()=>Schematic.layout(missing),/Invalid generated interior/);
  const endpoint=read();endpoint.InteriorPlan.Results[0].Routes.find(r=>r.ConnectionId==='generated-interior/explicit/entry').Forward.Path.Points[0].X+=1;
  assert.throws(()=>Schematic.layout(endpoint),/Invalid route geometry/);
 });
}
