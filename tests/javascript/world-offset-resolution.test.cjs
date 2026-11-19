const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const R=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-regional-schematic.js');
const root=process.env.WORLD_OFFSET_RESOLUTION_ROOT;
if(!root)throw Error('WORLD_OFFSET_RESOLUTION_ROOT is required');
const world=JSON.parse(fs.readFileSync(path.join(root,'world','world-regions.json'),'utf8'));
const area=p=>{const v=p.Vertices,o=v[0];let total=0;for(let i=1;i+1<v.length;i++)total+=(v[i].X-o.X)*(v[i+1].Y-o.Y)-(v[i].Y-o.Y)*(v[i+1].X-o.X);return Math.abs(total)/2;};
const cross=(a,b,c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
function triangles(p){
 let v=p.Vertices.map(q=>({...q})),changed=true;
 while(changed&&v.length>3){changed=false;for(let i=0;i<v.length;i++)if(Math.abs(cross(v[(i+v.length-1)%v.length],v[i],v[(i+1)%v.length]))<1e-8){v.splice(i,1);changed=true;break;}}
 let signed=0;for(let i=0;i<v.length;i++)signed+=v[i].X*v[(i+1)%v.length].Y-v[(i+1)%v.length].X*v[i].Y;if(signed<0)v.reverse();
 const out=[];while(v.length>3){let found=false;for(let i=0;i<v.length;i++){
  const a=v[(i+v.length-1)%v.length],b=v[i],c=v[(i+1)%v.length];if(cross(a,b,c)<=1e-8)continue;
  if(v.some((q,j)=>j!==i&&j!==(i+1)%v.length&&j!==(i+v.length-1)%v.length&&cross(a,b,q)>=-1e-8&&cross(b,c,q)>=-1e-8&&cross(c,a,q)>=-1e-8))continue;
  out.push([a,b,c]);v.splice(i,1);found=true;break;}assert.ok(found,'independent triangulation');}out.push(v);return out;
}
function overlap(a,b){let subject=a;for(let i=0;i<3;i++){const x=b[i],y=b[(i+1)%3],input=subject;subject=[];
 for(let j=0;j<input.length;j++){const p=input[j],q=input[(j+1)%input.length],u=cross(x,y,p),v=cross(x,y,q);
  if(u>=-1e-9)subject.push(p);if((u>=0)!==(v>=0)){const t=u/(u-v);subject.push({X:p.X+t*(q.X-p.X),Y:p.Y+t*(q.Y-p.Y)});}}
 if(!subject.length)return 0;}return subject.length<3?0:area({Vertices:subject});}
function route(route,radius){
 assert.equal(route.Status,'Found');const points=route.Path.Points;assert.ok(points.length>=2);
 for(let i=1;i<points.length;i++){
  const lo=Math.min(points[i-1].X,points[i].X),hi=Math.max(points[i-1].X,points[i].X);
  assert.ok(hi<=150-radius+1e-7||lo>=160+radius-1e-7,'path crosses the full-height forbidden barrier');
 }
}
test('offset partition preserves boundary and full ownership area',()=>{
 assert.equal(R.layout(world).boxes.length,48);const definition=world.Definition;
 assert.equal(definition.OwnershipPlan.Source.Pattern.PartitionSearch.BendOffset,150);
 const regions=definition.Regions;assert.equal(regions.length,3);
 assert.ok(Math.abs(regions.reduce((sum,r)=>sum+area(r.Ownership),0)-672*448)<1e-6);
 for(let i=0;i<regions.length;i++)for(let j=0;j<i;j++){
  const left=triangles(regions[i].Ownership),right=triangles(regions[j].Ownership);
  const shared=left.reduce((sum,a)=>sum+right.reduce((n,b)=>n+overlap(a,b),0),0);
  assert.ok(shared<1e-6,`region ownership overlaps by ${shared}`);
 }
 for(const region of regions)for(const p of region.Ownership.Vertices){
  assert.ok(p.Y>=0&&p.Y<=448);
  assert.ok(p.Y===448||Math.abs(p.Y-448)>1e-10,'source-edge coordinate drifted');
 }
});
test('all source members and paths retain barrier clearance',()=>{
 assert.equal(world.Regions.reduce((n,r)=>n+r.World.AccessPlan.Sites.length,0),45);
 for(const region of world.Regions){const local=region.World,source=local.PlacementPlan.Definition.SourceWorld;
  for(const node of source.Nodes)assert.deepEqual(local.Nodes.find(n=>n.Id===node.Id),node);
  const lengths=p=>p.Vertices.map((a,i)=>{const b=p.Vertices[(i+1)%p.Vertices.length];return Math.hypot(b.X-a.X,b.Y-a.Y);}).sort((a,b)=>a-b);
  for(const site of source.AccessPlan.Sites){const current=local.AccessPlan.Sites.find(s=>s.NodeId===site.NodeId);assert.ok(current);
   const before=lengths(site.Footprint),after=lengths(current.Footprint);assert.equal(before.length,after.length);
   before.forEach((n,i)=>assert.ok(Math.abs(n-after[i])<1e-7*Math.max(1,n),'source footprint changed'));}
  for(const link of local.RoutePlan.Routes){const rule=local.RoutePlan.Rules.find(x=>x.Id===link.RuleId);assert.ok(rule);
   route(link.Forward,rule.Profile.Radius);if(link.Reverse)route(link.Reverse,rule.Profile.Radius);}}
 assert.equal(world.Connections.length,2);for(const connection of world.Connections){
  route(connection.Forward,connection.Definition.Profile.Radius);route(connection.Reverse,connection.Definition.Profile.Radius);}
});
