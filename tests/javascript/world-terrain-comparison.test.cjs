const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_TERRAIN_COMPARISON_ROOT;
if(!root)throw Error('WORLD_TERRAIN_COMPARISON_ROOT is required');
const study=JSON.parse(fs.readFileSync(path.join(root,'study.json'),'utf8'));
const area=p=>{let total=0;const v=p.Vertices;for(let i=0;i<v.length;i++){const a=v[i],b=v[(i+1)%v.length];total+=a.X*b.Y-b.X*a.Y;}return Math.abs(total)/2;};
const close=(a,b)=>assert.ok(Math.abs(a-b)<=1e-7*Math.max(1,Math.abs(a),Math.abs(b)),`${a} != ${b}`);
test('records every declared landscape including refusals',()=>{
 assert.deepEqual(study.Observations.map(x=>x.Name),['flat','hillside','valley','coastal-edge']);
 assert.equal(study.Generated+study.Refused,4);
 assert.equal(study.Generated,4,'all fixed terrain cases must generate; refusals remain in study.json');
 assert.ok(study.OnePublishedWorldReplayed);
});
for(const entry of study.Observations.filter(x=>x.Status==='Generated'))test(`${entry.Name}: saved sites preserve source areas and paths connect all regions`,()=>{
 const result=JSON.parse(fs.readFileSync(path.join(root,entry.Name+'.json'),'utf8'));
 const world=result.World;
 assert.equal(world.Regions.length,4);
 assert.equal(world.Regions.reduce((n,r)=>n+r.World.Nodes.length,0),20);
 const graph=new Map(world.Regions.map(r=>[r.RegionId,[]]));
 let sites=0;
 for(const region of world.Regions){
  const local=region.World,nodes=new Map(local.Nodes.map(n=>[n.Id,n]));
  for(const site of local.AccessPlan.Sites){const node=nodes.get(site.NodeId);assert.ok(node);
   const source=local.FootprintDefinitions[node.ArchetypeId];assert.ok(source?.Polygon);close(area(site.Footprint),area(source.Polygon));sites++;}
 }
 assert.equal(sites,12);assert.equal(world.Connections.length,3);
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
 assert.equal(seen.size,4);
});
