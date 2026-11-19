const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_ADAPTIVE_LAYOUT_GATE;if(!root)throw new Error('WORLD_ADAPTIVE_LAYOUT_GATE must identify the current gate output');
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8'));
const area=ps=>Math.abs(ps.reduce((s,p,i)=>s+p.X*ps[(i+1)%ps.length].Y-p.Y*ps[(i+1)%ps.length].X,0))/2;
const signed=ps=>ps.reduce((s,p,i)=>s+p.X*ps[(i+1)%ps.length].Y-p.Y*ps[(i+1)%ps.length].X,0);
const cross=(a,b,p)=>(b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);
const near=(a,b)=>assert.ok(Math.abs(a-b)<1e-6,`${a} != ${b}`);
const edges=ps=>ps.map((a,i)=>[a,ps[(i+1)%ps.length]]);
function intersection(subject,clip){
 let output=subject,orientation=Math.sign(signed(clip));assert.ok(orientation);
 for(const[a,b]of edges(clip)){
  const input=output;output=[];if(!input.length)break;
  for(let i=0;i<input.length;i++){
   const p=input[i],q=input[(i+1)%input.length],dp=cross(a,b,p)*orientation,dq=cross(a,b,q)*orientation;
   if(dp>=-1e-9)output.push(p);
   if((dp>=-1e-9)!==(dq>=-1e-9)){const t=dp/(dp-dq);output.push({X:p.X+t*(q.X-p.X),Y:p.Y+t*(q.Y-p.Y)});}
  }
 }return output;
}
function pointDistance(p,a,b){const dx=b.X-a.X,dy=b.Y-a.Y,t=Math.max(0,Math.min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/(dx*dx+dy*dy)));return Math.hypot(p.X-a.X-t*dx,p.Y-a.Y-t*dy);}
function inside(p,ps){let hit=false;for(const[a,b]of edges(ps)){if(pointDistance(p,a,b)<1e-7)return true;if((a.Y>p.Y)!==(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)hit=!hit;}return hit;}
function distance(a,b,c,d){if(cross(a,b,c)*cross(a,b,d)<0&&cross(c,d,a)*cross(c,d,b)<0)return 0;return Math.min(pointDistance(a,c,d),pointDistance(b,c,d),pointDistance(c,a,b),pointDistance(d,a,b));}
const modes=['baseline','protected-hole','steep-cliff','lagoon-coast'];
const directions=w=>[...w.RoutePlan.Routes.flatMap(r=>[r.Forward,r.Reverse].map(result=>({result,profile:w.RoutePlan.Rules.find(v=>v.Id===r.RuleId).Profile}))),
 ...w.NetworkPlan.Networks.flatMap(n=>n.Plan.Edges.flatMap(e=>[e.Forward,e.Reverse].map(result=>({result,profile:n.Plan.Options.Profile}))))];
for(const mode of modes){
 test(`${mode}: complete catalog source program and exact neutral output`,()=>{
  const w=read(`${mode}/world/world-metadata.json`),catalog=read(`${mode}/catalog.json`),before=JSON.stringify(w),view=S.layout(w);
  assert.equal(w.Nodes.length,94);assert.equal(w.AccessPlan.Sites.length,91);assert.equal(w.PlacementPlan.ProgramInstances.length,35);
  assert.equal(w.Nodes.filter(n=>n.Kind==='building').length,28);assert.equal(w.Nodes.filter(n=>n.Kind==='equipment').length,56);
  assert.equal(view.boxes.length,94);assert.equal(view.routes.length,74);
  for(const site of w.AccessPlan.Sites){
   const node=w.Nodes.find(n=>n.Id===site.NodeId),source=catalog.Archetypes.find(a=>a.Id===node.ArchetypeId).PhysicalFootprint.Polygon;
   assert.equal(site.Footprint.Vertices.length,source.Vertices.length);
   for(let i=0;i<source.Vertices.length;i++){near(site.Footprint.Vertices[i].X,node.Position[0]+source.Vertices[i].X);near(site.Footprint.Vertices[i].Y,node.Position[1]+source.Vertices[i].Y);}
  }
  for(const {result}of directions(w))assert.deepEqual(view.routes.find(r=>r.id===result.Id).points,result.Path.Points.map(p=>({x:p.X,y:p.Y})));
  assert.equal(fs.readFileSync(path.join(root,mode,'world/schematic.js'),'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  assert.equal(JSON.stringify(w),before);
 });
 test(`${mode}: independent suitable area, non-overlapping allocations and full-width passage clearance`,()=>{
  const w=read(`${mode}/world/world-metadata.json`),boundary=w.Surface.Boundary.Vertices;
  const terrainMasks=[...w.Surface.Exclusions.map(e=>e.Boundary.Vertices),...w.Surface.Patches.filter(p=>p.Id==='cliff').map(p=>p.Boundary.Vertices)];
  const suitable=area(boundary)-terrainMasks.reduce((sum,p)=>sum+area(p),0);near(suitable,w.RegionPlan.SuitableArea);near(area(boundary),w.RegionPlan.BoundaryArea);
  const cells=w.LocalLayoutPlan.Allocations.flatMap(a=>a.Shape.Cells.map(c=>({id:a.Id,region:a.RegionId,points:c.Vertices})));
  for(const cell of cells){
   assert.ok(cell.points.every(p=>inside(p,boundary)));
   for(const hole of terrainMasks)near(area(intersection(cell.points,hole)),0);
  }
  for(let i=0;i<cells.length;i++)for(let j=i+1;j<cells.length;j++)near(area(intersection(cells[i].points,cells[j].points)),0);
  for(const row of w.LocalLayoutPlan.Areas){
   const measured=cells.filter(c=>c.region===row.RegionId).reduce((sum,c)=>sum+area(c.points),0);
   near(measured,row.AllocatedArea);near(row.RegionArea,row.AllocatedArea+row.ResidualArea);
  }
  near(cells.reduce((sum,c)=>sum+area(c.points),0),suitable);
  for(const cell of cells.filter(c=>c.region==='west'))assert.ok(cell.points.every(p=>p.X<=100+1e-7),'grid is confined to its own region');
  const forbidden=[...terrainMasks,...w.AccessPlan.Sites.filter(s=>s.Occupancy==='Solid').map(s=>s.Footprint.Vertices)];
  for(const {result,profile}of directions(w))for(let i=1;i<result.Path.Points.length;i++){
   const a=result.Path.Points[i-1],b=result.Path.Points[i];assert.ok(inside(a,boundary)&&inside(b,boundary));
   for(const[c,d]of edges(boundary))assert.ok(distance(a,b,c,d)>=profile.Radius-1e-6);
   for(const polygon of forbidden){assert.ok(!inside(a,polygon)&&!inside(b,polygon));for(const[c,d]of edges(polygon))assert.ok(distance(a,b,c,d)>=profile.Radius-1e-6);}
  }
 });
}
test('same mandatory program on four terrains changes partitions while preserving every physical source',()=>{
 const base=read('baseline/world/world-metadata.json'),observations=read('observations.json');assert.equal(observations.length,4);
 const expected={'baseline':0,'protected-hole':64,'steep-cliff':108,'lagoon-coast':409};
 for(const mode of modes){const w=read(`${mode}/world/world-metadata.json`);assert.deepEqual(w.Nodes,base.Nodes);assert.deepEqual(w.AccessPlan.Sites,base.AccessPlan.Sites);near(base.RegionPlan.SuitableArea-w.RegionPlan.SuitableArea,expected[mode]);}
});
test('roadless resource/interaction roles use the same complete geometry and area denominator',()=>{
 const base=read('baseline/world/world-metadata.json'),w=read('roadless-resources/world/world-metadata.json');
 assert.equal(w.Nodes.length,94);assert.equal(w.Nodes.filter(n=>n.Kind==='resource-unit').length,28);assert.equal(w.Nodes.filter(n=>n.Kind==='interaction-point').length,56);
 assert.deepEqual(w.LocalLayoutPlan,base.LocalLayoutPlan);assert.deepEqual(w.AccessPlan.Sites,base.AccessPlan.Sites);
 assert.equal(w.RoutePlan,null);assert.equal(w.NetworkPlan,null);assert.equal(w.CorridorPlan,null);assert.equal(S.layout(w).routes.length,0);
});
test('one declared repair preserves the unrelated owning strategy, source sizes and protected origin',()=>{
 const recipe=read('owning-repair/recipe.json'),w=read('owning-repair/world/world-metadata.json'),cp=read('owning-repair/world/generation-session.json');
 assert.equal(w.Nodes.length,13);assert.equal(w.AccessPlan.Sites.length,4);assert.equal(w.LayoutAdaptationPlan.Attempts.length,2);
 assert.deepEqual(w.LayoutAdaptationPlan.Attempts.map(a=>a.Accepted),[false,true]);
 assert.deepEqual(w.LocalLayoutPlan.Definition.Layouts.find(l=>l.Id==='east-layout'),recipe.LocalLayouts.Layouts.find(l=>l.Id==='east-layout'));
 assert.equal(cp.ProtectedPositions[0].Definition.Id,'east-origin');assert.deepEqual(cp.ProtectedPositions[0].Position,[63,10]);
 for(const site of w.AccessPlan.Sites){near(area(site.Footprint.Vertices),84);}
});
