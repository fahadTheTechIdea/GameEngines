const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_BENT_TERRAIN_EDIT_ROOT;
if(!root)throw Error('WORLD_BENT_TERRAIN_EDIT_ROOT is required');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name,'world-regions.json'),'utf8'));
const initial=read('initial'),moved=read('moved'),restored=read('restored');
const equal=(a,b)=>assert.deepEqual(a,b);
function checkBarrier(route,radius,left,right){
 assert.equal(route.Status,'Found');const p=route.Path.Points;assert.ok(p.length>=2);
 for(let i=1;i<p.length;i++){
  const lo=Math.min(p[i-1].X,p[i].X),hi=Math.max(p[i-1].X,p[i].X);
  assert.ok(hi<=left-radius+1e-7||lo>=right+radius-1e-7,`route ${route.Id} crosses barrier`);
 }
}
for(const [name,world,left] of [['initial',initial,150],['moved',moved,115],['restored',restored,150]])test(`${name}: full content and swept routes avoid the terrain barrier`,()=>{
 assert.equal(R.layout(world).boxes.length,48);assert.equal(world.Connections.length,2);
 assert.equal(world.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),45);
 for(const r of world.Regions){const w=r.World,source=w.PlacementPlan.Definition.SourceWorld;
  for(const node of source.Nodes)equal(w.Nodes.find(n=>n.Id===node.Id),node);
  for(const link of w.RoutePlan.Routes){const rule=w.RoutePlan.Rules.find(x=>x.Id===link.RuleId);assert.ok(rule);
   checkBarrier(link.Forward,rule.Profile.Radius,left,left+10);if(link.Reverse)checkBarrier(link.Reverse,rule.Profile.Radius,left,left+10);}}
 for(const c of world.Connections){checkBarrier(c.Forward,c.Definition.Profile.Radius,left,left+10);checkBarrier(c.Reverse,c.Definition.Profile.Radius,left,left+10);}
});
test('moved terrain changes boundaries and routes; restored source reproduces the original',()=>{
 const selected=w=>w.Definition.OwnershipPlan.Partition.PartitionAttempts.filter(a=>a.Status==='Selected').map(a=>a.Bend);
 assert.notDeepEqual(selected(initial),selected(moved));assert.notDeepEqual(initial.Definition.Regions.map(r=>r.Ownership),moved.Definition.Regions.map(r=>r.Ownership));
 assert.notDeepEqual(initial.Connections.map(c=>c.Forward.Path),moved.Connections.map(c=>c.Forward.Path));
 const lengths=p=>p.Vertices.map((a,i)=>{const b=p.Vertices[(i+1)%p.Vertices.length];return Math.hypot(b.X-a.X,b.Y-a.Y);}).sort((a,b)=>a-b);
 const originals=new Map(initial.Regions.flatMap(r=>r.World.AccessPlan.Sites.map(s=>[r.RegionId+'/'+s.NodeId,lengths(s.Footprint)])));
 for(const region of moved.Regions)for(const site of region.World.AccessPlan.Sites){
  const before=originals.get(region.RegionId+'/'+site.NodeId),after=lengths(site.Footprint);assert.ok(before);assert.equal(before.length,after.length);
  after.forEach((v,i)=>assert.ok(Math.abs(v-before[i])<1e-7*Math.max(1,v,before[i]),'source edge length changed'));
 }
 equal(initial,restored);assert.equal(fs.existsSync(path.join(root,'blocked')),false);
});
