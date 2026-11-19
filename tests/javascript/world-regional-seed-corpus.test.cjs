const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_REGIONAL_SEED_CORPUS_ROOT;
if(!root)throw Error('WORLD_REGIONAL_SEED_CORPUS_ROOT is required');
const study=JSON.parse(fs.readFileSync(path.join(root,'study.json'),'utf8'));
const area=p=>{const v=p.Vertices;let n=0;for(let i=0;i<v.length;i++){const a=v[i],b=v[(i+1)%v.length];n+=a.X*b.Y-b.X*a.Y;}return Math.abs(n)/2;};
const close=(a,b)=>assert.ok(Math.abs(a-b)<=1e-7*Math.max(1,Math.abs(a),Math.abs(b)),`${a} != ${b}`);
for(const item of study.Observations.filter(o=>o.Status==='Generated'))test(`${item.Seed}: source-size and connected bidirectional world`,()=>{
 const world=JSON.parse(fs.readFileSync(path.join(root,item.Seed+'.json'),'utf8')).World;
 assert.equal(R.layout(world).boxes.length,20);
 for(const region of world.Regions){
  const local=region.World,nodes=new Map(local.Nodes.map(n=>[n.Id,n]));
  for(const site of local.AccessPlan.Sites){const node=nodes.get(site.NodeId);assert.ok(node);
   const source=local.FootprintDefinitions[node.ArchetypeId];assert.ok(source?.Polygon);close(area(site.Footprint),area(source.Polygon));}
 }
 const graph=new Map(world.Regions.map(r=>[r.RegionId,[]]));
 for(const connection of world.Connections){
  graph.get(connection.Definition.From.RegionId).push(connection.Definition.To.RegionId);
  graph.get(connection.Definition.To.RegionId).push(connection.Definition.From.RegionId);
  for(const [route,from,to] of [[connection.Forward,connection.From,connection.To],[connection.Reverse,connection.To,connection.From]]){
   assert.equal(route.Status,'Found');const points=route.Path.Points;assert.ok(points.length>=2);
   assert.deepEqual(points[0],from);assert.deepEqual(points.at(-1),to);
   let distance=0;for(let i=1;i<points.length;i++)distance+=Math.hypot(points[i].X-points[i-1].X,points[i].Y-points[i-1].Y);
   close(distance,route.Cost.Distance);
  }
 }
 const seen=new Set([world.Regions[0].RegionId]),todo=[...seen];
 for(let i=0;i<todo.length;i++)for(const id of graph.get(todo[i]))if(!seen.has(id)){seen.add(id);todo.push(id);}
 assert.equal(seen.size,world.Regions.length);
});
test('the corpus includes distinct network structures and authored program choices',()=>{
 const worlds=study.Observations.filter(o=>o.Status==='Generated').map(o=>JSON.parse(fs.readFileSync(path.join(root,o.Seed+'.json'),'utf8')));
 const graphs=new Set(worlds.map(r=>JSON.stringify(r.World.NetworkPlan.Networks[0].Plan.Edges.map(e=>[e.Link.FromId,e.Link.ToId].sort()).sort())));
 const choices=new Set(worlds.map(r=>JSON.stringify(r.Receipt.Attempts.at(-1).Selections)));
 assert.ok(graphs.size>=2,`only ${graphs.size} regional network structure`);
 assert.ok(choices.size>=2,`only ${choices.size} authored program choice`);
});
