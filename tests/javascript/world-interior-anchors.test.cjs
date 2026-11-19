const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root='generated/interior-anchors-verified',read=(n,f='world-metadata.json')=>JSON.parse(fs.readFileSync(`${root}/${n}/${f}`));
const near=(a,b)=>assert.ok(Math.abs(a-b)<=1e-6*Math.max(1,Math.abs(a),Math.abs(b)),`${a}/${b}`);
const cross=(a,b,p)=>(b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);
const distance=(p,a,b)=>{const x=b.X-a.X,y=b.Y-a.Y,t=Math.max(0,Math.min(1,((p.X-a.X)*x+(p.Y-a.Y)*y)/(x*x+y*y)));return Math.hypot(p.X-a.X-t*x,p.Y-a.Y-t*y);};
const clearance=(p,edges)=>Math.min(...edges.map(e=>distance(p,e.From,e.To)));
for(const n of ['uniform','weighted'])test(`${n}: effective anchors, complete source geometry and schematic retain bounded interior witnesses`,()=>{
 const w=read(n),catalog=read(n,'catalog.json');assert.equal(Schematic.layout(w).boxes.length,7);assert.equal(w.AccessPlan.Sites.length,2);assert.equal(w.RoutePlan.Routes.length,1);
 const placement=w.PlacementPlan;assert.equal(placement.AllocationAnchorResults.length,2);
 for(const proof of placement.AllocationAnchorResults){const a=w.LocalLayoutPlan.Allocations.find(a=>a.Id===proof.AllocationId),p=proof.Interior;
  near(clearance(p.Position,a.Shape.BoundaryEdges),p.Clearance);assert.ok(p.Clearance>=8);assert.ok(p.UpperBound-p.Clearance<=.05);
  assert.deepEqual(placement.Definition.Request.Anchors.find(a=>a.Id===proof.AnchorId).Position,p.Position);
 }
 for(const site of w.AccessPlan.Sites){const node=w.Nodes.find(n=>n.Id===site.NodeId),source=catalog.Archetypes.find(a=>a.Id===node.ArchetypeId).PhysicalFootprint.Polygon;
  source.Vertices.forEach((p,i)=>{near(p.X+node.Position[0],site.Footprint.Vertices[i].X);near(p.Y+node.Position[1],site.Footprint.Vertices[i].Y);});}
 if(n==='weighted')for(const trace of w.LocalLayoutPlan.GeneratedAreas)for(const s of trace.AreaPlan.Strata){let mass=0,mx=0,my=0;const ground=w.Surface.Patches[0];
  for(const {Vertices:v} of s.Cells){const a=v[0];for(let i=1;i+1<v.length;i++){const b=v[i],c=v[i+1],area=cross(a,b,c)/2;
   for(const [u,t,z] of [[2/3,1/6,1/6],[1/6,2/3,1/6],[1/6,1/6,2/3]]){const x=u*a.X+t*b.X+z*c.X,y=u*a.Y+t*b.Y+z*c.Y;
    const elevation=ground.Elevation+ground.GradientX*(x-ground.Origin.X)+ground.GradientY*(y-ground.Origin.Y),d=1+.1*elevation;
    mass+=area*d/3;mx+=area*x*d/3;my+=area*y*d/3;}
  }}near(mass,s.Mass);near(mx/mass,s.MassCentroid.X);near(my/mass,s.MassCentroid.Y);
  near(clearance(s.Position,s.BoundaryEdges),s.Interior.Clearance);
 }
 assert.deepEqual(fs.readFileSync(`${root}/${n}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
});
test('holed area preserves excluded mass centroid while its declared representative satisfies analytic clearance',()=>{
 const plan=JSON.parse(fs.readFileSync('generated/interior-anchor-current-inputs/holed-area-plan.json')),s=plan.Strata[0],optimum=7-Math.sqrt(24);
 near(plan.Area,104);near(plan.Mass,104);near(s.MassCentroid.X,6);near(s.MassCentroid.Y,5);
 near(clearance(s.Position,s.BoundaryEdges),s.Interior.Clearance);assert.ok(s.Interior.Clearance<=optimum+1e-8&&optimum-s.Interior.Clearance<=.01&&s.Interior.UpperBound>=optimum-1e-8);
 const validate=copy=>Schematic.interiorWitness(plan.Pattern.Interior,copy.Interior,copy,copy.Position,()=>{throw Error('invalid witness');});validate(s);
 for(const change of [c=>c.Interior.Clearance+=1,c=>c.Interior.UpperBound+=1,c=>c.Interior.Evaluations=0,c=>c.Position={X:6,Y:5}]){const copy=structuredClone(s);change(copy);assert.throws(()=>validate(copy));}
});
test('allocation viewer rejects altered clearance, missing results, duplicate and orphan witnesses',()=>{
 const source=read('weighted');for(const mutate of [w=>w.PlacementPlan.AllocationAnchorResults[0].Interior.Clearance+=1,w=>w.PlacementPlan.AllocationAnchorResults.pop(),w=>w.PlacementPlan.AllocationAnchorResults.push(w.PlacementPlan.AllocationAnchorResults[0]),w=>w.PlacementPlan.AllocationAnchorResults[0].AllocationId='orphan']){
  const w=structuredClone(source);mutate(w);assert.throws(()=>Schematic.layout(w),/Invalid local layout plan/);
 }
});
function clip(subject,triangle){let out=subject;for(let i=0;i<3;i++){const a=triangle[i],b=triangle[(i+1)%3],input=out;out=[];
 for(let j=0;j<input.length;j++){const p=input[j],q=input[(j+1)%input.length],u=cross(a,b,p),v=cross(a,b,q);if(u>=0)out.push(p);
  if((u>=0)!==(v>=0)){const t=u/(u-v);out.push({X:p.X+t*(q.X-p.X),Y:p.Y+t*(q.Y-p.Y)});}}
 }return out;}
const area=v=>v.length<3?0:Math.abs(v.slice(1,-1).reduce((sum,b,i)=>sum+cross(v[0],b,v[i+2]),0)/2);
const within=(p,t)=>t.every((a,i)=>cross(a,t[(i+1)%t.length],p)>=-1e-9);
function segmentTriangle(a,b,t){if(within(a,t)||within(b,t))return 0;let result=Infinity;
 for(let i=0;i<3;i++){const c=t[i],d=t[(i+1)%3],u=cross(a,b,c),v=cross(a,b,d),s=cross(c,d,a),z=cross(c,d,b);
  if(u*v<0&&s*z<0)return 0;result=Math.min(result,distance(a,c,d),distance(b,c,d),distance(c,a,b),distance(d,a,b));}
 return result;
}
test('steep source independently preserves all physical content and full-width paths outside every excluded-grade triangle',()=>{
 const w=read('steep-source'),f=w.Surface.HeightField,catalog=read('steep-source','catalog.json'),nodes=new Map(w.Nodes.map(n=>[n.Id,n]));
 assert.equal(w.Nodes.length,43);assert.equal(w.AccessPlan.Sites.length,42);assert.equal(Schematic.layout(w).boxes.length,43);near(w.RegionPlan.SuitableArea,25650);
 assert.equal(w.Nodes.filter(n=>n.Kind==='compound').length,7);assert.equal(w.Nodes.filter(n=>n.Kind==='structure').length,14);
 assert.equal(w.PlacementPlan.AllocationAnchorResults.length,7);assert.equal(w.LayoutAdaptationPlan.Attempts.length,20);
 assert.equal(w.Surface.Terrain.Operations[0].Height,1.8);assert.equal(w.RegionPlan.Request.Intents[0].MinimumArea,25000);
 for(let y=0;y<f.Rows;y++)for(let x=0;x<f.Columns;x++){const px=15*x,py=15*y,d=Math.hypot(px-60,Math.max(30-py,0,py-60)),t=Math.max(0,1-d/20);near(f.Heights[y*f.Columns+x],.01*px+.005*py+1.8*t*t*(3-2*t));}
 const point=id=>({X:f.Origin.X+(id%f.Columns)*f.StepX,Y:f.Origin.Y+Math.floor(id/f.Columns)*f.StepY}),blocked=[];
 for(let y=0;y<f.Rows-1;y++)for(let x=0;x<f.Columns-1;x++){const a=y*f.Columns+x,b=a+1,c=a+f.Columns,d=c+1;
  for(const ids of [[a,b,d],[a,d,c]]){const [p,q,r]=ids.map(point),det=cross(p,q,r),h=ids.map(i=>f.Heights[i]);
   const gx=((h[1]-h[0])*(r.Y-p.Y)-(h[2]-h[0])*(q.Y-p.Y))/det,gy=((q.X-p.X)*(h[2]-h[0])-(r.X-p.X)*(h[1]-h[0]))/det;
   if(Math.hypot(gx,gy)>.1)blocked.push([p,q,r]);
  }}assert.ok(blocked.length>0);
 assert.ok(blocked.every(t=>t.every(p=>within(p,w.Surface.Boundary.Vertices))));near(area(w.Surface.Boundary.Vertices)-blocked.reduce((sum,t)=>sum+area(t),0),w.RegionPlan.SuitableArea);
 assert.equal(w.Nodes.filter(n=>n.Kind==='ground-area').length,7);assert.equal(w.Nodes.filter(n=>n.Kind==='equipment').length,14);
 assert.ok(w.RoutePlan.Rules.every(r=>r.Profile.MaximumSlope===.1&&r.Profile.Radius===.3));

 for(const s of w.AccessPlan.Sites){const n=nodes.get(s.NodeId),source=catalog.Archetypes.find(a=>a.Id===n.ArchetypeId).PhysicalFootprint.Polygon;
  source.Vertices.forEach((p,i)=>{near(p.X+n.Position[0],s.Footprint.Vertices[i].X);near(p.Y+n.Position[1],s.Footprint.Vertices[i].Y);});
  for(const t of blocked)assert.ok(area(clip(s.Footprint.Vertices,t))<1e-7);
 }
 for(const stage of [w.PlacementPlan.Placement,...w.PlacementPlan.NestedStages.map(s=>s.Placement)])for(const s of stage.Sites)if(s.Reservation)for(const t of blocked)assert.ok(area(clip(s.Reservation.Footprint.Vertices,t))<1e-7);
 const paths=w.RoutePlan.Routes.flatMap(r=>[r.Forward,r.Reverse]).concat(w.NetworkPlan.Networks.flatMap(n=>n.Plan.Edges.flatMap(e=>[e.Forward,e.Reverse])));assert.equal(paths.length,82);
 for(const path of paths)for(let i=1;i<path.Path.Points.length;i++)for(const t of blocked)assert.ok(segmentTriangle(path.Path.Points[i-1],path.Path.Points[i],t)>=.3-1e-7,'full-width grade clearance');
 const graph=new Map(w.AccessPlan.Sites.map(s=>[s.NodeId,[]]));for(const l of w.Connections){graph.get(l.FromId).push(l.ToId);if(l.Bidirectional)graph.get(l.ToId).push(l.FromId);}
 const seen=new Set(),queue=[graph.keys().next().value];while(queue.length){const id=queue.shift();if(seen.has(id))continue;seen.add(id);queue.push(...graph.get(id));}assert.equal(seen.size,42);
 assert.deepEqual(fs.readFileSync(`${root}/steep-source/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
});
