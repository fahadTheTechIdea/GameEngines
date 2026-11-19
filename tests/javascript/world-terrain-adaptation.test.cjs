const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const root=process.env.WORLD_TERRAIN_ADAPTATION_ROOT;
if(!root)throw Error('WORLD_TERRAIN_ADAPTATION_ROOT is required');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const base=read('initial/world-regions.json'),wet=read('wetland/world-regions.json');
const initial=read('initial-request.json'),changed=read('wetland-request.json');
// Clip a line segment against a slightly inset rectangle. A boundary touch is
// separate from crossing the forbidden interior and is not accepted as crossing.
function enters(a,b,padding=0){
 let lo=0,hi=1;const e=1e-7;
 for(const [v,d,min,max] of [[a.X,b.X-a.X,22-padding+e,38+padding-e],[a.Y,b.Y-a.Y,30-padding+e,48+padding-e]]){
  if(d===0){if(v<=min||v>=max)return false;continue;}
  const t1=(min-v)/d,t2=(max-v)/d;lo=Math.max(lo,Math.min(t1,t2));hi=Math.min(hi,Math.max(t1,t2));
  if(lo>=hi)return false;
 }
 return hi>0&&lo<1;
}
function paths(world){return world.Connections.flatMap(c=>[c.Forward.Path.Points,c.Reverse.Path.Points]);}
function crossings(world,padding=0){return paths(world).flatMap(points=>points.slice(1).map((p,i)=>enters(points[i],p,padding))).filter(Boolean).length;}
test('only the declared terrain patch changes',()=>{
 assert.equal(initial.Template.Surface.Patches.length,0);
 assert.equal(changed.Template.Surface.Patches.length,1);
 assert.equal(changed.Template.Surface.Patches[0].Kind,'bog');
 changed.Template.Surface.Patches=[];assert.deepEqual(changed,initial);
});
test('the terrain intersects the old route and every new route avoids it',()=>{
 assert.ok(crossings(base)>0,'original routes must actually cross the new wetland');
 assert.equal(crossings(wet),0,'new paths must avoid forbidden terrain');
 assert.equal(crossings(wet,.4),0,'new paths retain the declared .3 radius and .1 clearance');
});
test('both worlds preserve complete physical content and connected two-way graph',()=>{
 for(const world of [base,wet]){
  assert.equal(world.Regions.length,4);assert.equal(world.Regions.reduce((n,r)=>n+r.World.Nodes.length,0),20);
  assert.equal(world.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),12);assert.equal(world.Connections.length,3);
  const graph=new Map(world.Regions.map(r=>[r.RegionId,[]]));
  for(const c of world.Connections){graph.get(c.Definition.From.RegionId).push(c.Definition.To.RegionId);graph.get(c.Definition.To.RegionId).push(c.Definition.From.RegionId);
   for(const [route,start,end] of [[c.Forward,c.From,c.To],[c.Reverse,c.To,c.From]]){assert.equal(route.Status,'Found');assert.deepEqual(route.Path.Points[0],start);assert.deepEqual(route.Path.Points.at(-1),end);}}
  const seen=new Set([world.Regions[0].RegionId]),todo=[...seen];for(let i=0;i<todo.length;i++)for(const next of graph.get(todo[i]))if(!seen.has(next)){seen.add(next);todo.push(next);}
  assert.equal(seen.size,4);
 }
});
