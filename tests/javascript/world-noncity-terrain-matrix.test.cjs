const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const path=require('node:path');
const root=process.env.WORLD_NONCITY_TERRAIN_ROOT;
if(!root)throw Error('WORLD_NONCITY_TERRAIN_ROOT is required');
const read=name=>JSON.parse(fs.readFileSync(path.join(root,name),'utf8'));
const study=read('study.json');
const area=polygon=>Math.abs(polygon.Vertices.reduce((sum,a,i,v)=>{
 const b=v[(i+1)%v.length];return sum+a.X*b.Y-b.X*a.Y;
},0))/2;
const close=(a,b)=>assert.ok(Math.abs(a-b)<=1e-7*Math.max(1,Math.abs(a),Math.abs(b)),`${a} != ${b}`);
test('every prescribed counterfactual is accounted for, including the unusable surface',()=>{
 assert.deepEqual(study.Observations.map(o=>o.Id),['flat','slope','protected','water','irregular','no-ground']);
 assert.equal(study.Generated+study.Refused+study.Invalid,6);
 assert.equal(study.Invalid,0);
 assert.equal(study.Observations.find(o=>o.Id==='flat').Status,'Generated');
 assert.equal(study.Observations.find(o=>o.Id==='no-ground').Status,'Refused');
 assert.equal(study.OnePublishedWorldReplayed,true);
});
for(const item of study.Observations.filter(o=>o.Status==='Generated'))test(`${item.Id} preserves all terrain-required physical sources and travel`,()=>{
 const world=read(item.Id+'-world.json');
 assert.equal(world.Regions.length,2);
 for(const region of world.Regions){
  const local=region.World,count=local.TerrainDemandPlan.Results[0].Count;
  assert.ok(count>0);assert.equal(local.Nodes.length,1+2*count);assert.equal(local.AccessPlan.Sites.length,2*count);
  assert.equal(local.RoutePlan.Routes.length,count-1);
  const nodes=new Map(local.Nodes.map(n=>[n.Id,n]));
  for(const site of local.AccessPlan.Sites){
   const node=nodes.get(site.NodeId);assert.ok(node);
   const source=local.FootprintDefinitions[node.ArchetypeId];assert.ok(source?.Polygon);
   close(area(site.Footprint),area(source.Polygon));
  }
  for(const route of local.RoutePlan.Routes)assert.ok(route.Forward?.Path&&route.Reverse?.Path);
 }
 assert.equal(world.Connections.length,1);
 const route=world.Connections[0];
 for(const [part,start,end] of [[route.Forward,route.From,route.To],[route.Reverse,route.To,route.From]]){
  assert.equal(part.Status,'Found');assert.deepEqual(part.Path.Points[0],start);assert.deepEqual(part.Path.Points.at(-1),end);
  let distance=0;for(let i=1;i<part.Path.Points.length;i++)distance+=Math.hypot(part.Path.Points[i].X-part.Path.Points[i-1].X,part.Path.Points[i].Y-part.Path.Points[i-1].Y);
  close(distance,part.Cost.Distance);
 }
 if(item.Id==='water'){
  // Independent sampled clearance check against the authored 37..43, 0..27
  // forbidden rectangle. The declared 0.2 radius plus 0.1 search clearance
  // must hold on all saved local and cross-region paths.
  const paths=[route.Forward.Path,route.Reverse.Path,...world.Regions.flatMap(r=>
   r.World.RoutePlan.Routes.flatMap(x=>[x.Forward.Path,x.Reverse.Path]))];
  for(const p of paths)for(let i=1;i<p.Points.length;i++){
   const a=p.Points[i-1],b=p.Points[i],length=Math.hypot(b.X-a.X,b.Y-a.Y),steps=Math.max(1,Math.ceil(length/.05));
   for(let n=0;n<=steps;n++){
    const x=a.X+(b.X-a.X)*n/steps,y=a.Y+(b.Y-a.Y)*n/steps;
    const dx=Math.max(37-x,0,x-43),dy=Math.max(-y,0,y-27);
    assert.ok(Math.hypot(dx,dy)>=.3-1e-7,`path approaches forbidden water at ${x},${y}`);
   }
  }
 }
});
