const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root='generated/connected-area-verified',read=(n,f='world-metadata.json')=>JSON.parse(fs.readFileSync(`${root}/${n}/${f}`));
const near=(a,b)=>assert.ok(Math.abs(a-b)<=1e-6*Math.max(1,Math.abs(a),Math.abs(b)),`${a}/${b}`);
const cross=(a,b,p)=>(b.X-a.X)*(p.Y-a.Y)-(b.Y-a.Y)*(p.X-a.X);
function clip(subject,polygon){let out=subject;for(let i=0;i<polygon.length;i++){const a=polygon[i],b=polygon[(i+1)%polygon.length],input=out;out=[];
 for(let j=0;j<input.length;j++){const p=input[j],q=input[(j+1)%input.length],u=cross(a,b,p),v=cross(a,b,q);if(u>=0)out.push(p);
  if((u>=0)!==(v>=0)){const t=u/(u-v);out.push({X:p.X+t*(q.X-p.X),Y:p.Y+t*(q.Y-p.Y)});}}
 }return out;}
const area=v=>v.length<3?0:Math.abs(v.slice(1,-1).reduce((s,b,i)=>s+cross(v[0],b,v[i+2]),0)/2);
function integrate(cells,coefficient){let ar=0,mass=0,mx=0,my=0;for(const {Vertices:v} of cells){const a=v[0];for(let i=1;i+1<v.length;i++){
 const b=v[i],c=v[i+1],w=cross(a,b,c)/2;ar+=w;
 for(const [u,t,z] of [[2/3,1/6,1/6],[1/6,2/3,1/6],[1/6,1/6,2/3]]){const x=u*a.X+t*b.X+z*c.X,y=u*a.Y+t*b.Y+z*c.Y,d=1+coefficient*x;mass+=w*d/3;mx+=w*x*d/3;my+=w*y*d/3;}
 }}return {area:ar,mass,x:mx/mass,y:my/mass};}
function connected(cells){const edges=cells.map(c=>c.Vertices.map((a,i)=>[a,c.Vertices[(i+1)%c.Vertices.length]])),seen=new Set([0]),pending=[0];
 function shared(a,b,c,d){const x=b.X-a.X,y=b.Y-a.Y,length=Math.hypot(x,y);if(Math.abs(cross(a,b,c))>1e-7*length||Math.abs(cross(a,b,d))>1e-7*length)return false;
  const u=((c.X-a.X)*x+(c.Y-a.Y)*y)/(length*length),v=((d.X-a.X)*x+(d.Y-a.Y)*y)/(length*length);return (Math.min(1,Math.max(u,v))-Math.max(0,Math.min(u,v)))*length>1e-7;}
 while(pending.length){const i=pending.pop();for(let j=0;j<cells.length;j++)if(!seen.has(j)&&edges[i].some(([a,b])=>edges[j].some(([c,d])=>shared(a,b,c,d)))){seen.add(j);pending.push(j);}}
 assert.equal(seen.size,cells.length,'all positive-edge-connected source pieces retained');}
const distance=(p,a,b)=>{const x=b.X-a.X,y=b.Y-a.Y,t=Math.max(0,Math.min(1,((p.X-a.X)*x+(p.Y-a.Y)*y)/(x*x+y*y)));return Math.hypot(p.X-a.X-t*x,p.Y-a.Y-t*y);};
function disjoint(cells){const bounds=cells.map(c=>({x:Math.min(...c.Vertices.map(p=>p.X)),X:Math.max(...c.Vertices.map(p=>p.X)),y:Math.min(...c.Vertices.map(p=>p.Y)),Y:Math.max(...c.Vertices.map(p=>p.Y))}));
 for(let i=0;i<cells.length;i++)for(let j=0;j<i;j++){const a=bounds[i],b=bounds[j];if(a.X<=b.x||b.X<=a.x||a.Y<=b.y||b.Y<=a.y)continue;assert.ok(area(clip(cells[i].Vertices,cells[j].Vertices))<1e-6,'no lost or double-accounted pieces');}}
test('C-shaped support retains preferred-cut refusal, analytic mass and connected selected tree',()=>{
 const plan=JSON.parse(fs.readFileSync('examples/metadata/connected-area/geometry-plan.json'));assert.equal(plan.Strata.length,2);
 assert.deepEqual(plan.PartitionAttempts.map(a=>a.Status),['Rejected','Selected']);near(plan.Area,48);near(plan.Mass,48);
 for(const s of plan.Strata){near(s.Area,24);near(s.Mass,24);near(s.MassCentroid.X,13/3);assert.ok(Math.abs(s.MassCentroid.Y-4/3)<1e-6||Math.abs(s.MassCentroid.Y-20/3)<1e-6);connected(s.Cells);}
 S.areaPartitionWitness(plan,()=>{throw Error('invalid partition');});
 for(const edit of [p=>p.PartitionAttempts[0].Ordinal=4,p=>p.PartitionAttempts[1].Cut+=1,p=>p.PartitionAttempts[1].LeftMass+=1,p=>p.PartitionAttempts.pop(),p=>p.Pattern.PartitionSearch.AxisOffsetsDegrees=[0,180]]){
  const p=structuredClone(plan);edit(p);assert.throws(()=>S.areaPartitionWitness(p,()=>{throw Error('invalid partition');}));}
});
test('metadata program spaces carry replayable connected-partition proofs through the current schematic',()=>{
 const w=read('metadata');assert.equal(S.layout(w).boxes.length,7);assert.equal(w.AccessPlan.Sites.length,2);
 for(const t of w.LocalLayoutPlan.GeneratedAreas){assert.equal(t.AreaPlan.PartitionAttempts.filter(a=>a.Status==='Selected').length,1);S.areaPartitionWitness(t.AreaPlan,()=>{throw Error('invalid');});}
});
for(const [name,coefficient,count] of [['steep-world',.002,16],['richer-world',.003,17]]){
 test(`${name}: complete source, exact density-funded count and connected full support`,()=>{
  const w=read(name),source=w.PlacementPlan.Definition.SourceWorld,trace=w.PlacementPlan.PopulationAreas[0],p=trace.Plan,original=source.LocalLayoutPlan.Allocations.find(a=>a.Id==='landscape').Shape;
  assert.equal(w.Nodes.length,43+count);assert.equal(w.AccessPlan.Sites.length,42+count);assert.equal(S.layout(w).boxes.length,43+count);assert.equal(p.Strata.length,count);
  assert.equal(w.Nodes.filter(n=>n.Kind==='compound').length,7);assert.equal(w.Nodes.filter(n=>n.Kind==='structure').length,14);assert.equal(w.Nodes.filter(n=>n.Kind==='ground-area').length,7);assert.equal(w.Nodes.filter(n=>n.Kind==='equipment').length,14);
  const nodeMap=new Map(w.Nodes.map(n=>[n.Id,n]));for(const n of source.Nodes)assert.deepEqual(nodeMap.get(n.Id),n);
  assert.deepEqual(w.RoutePlan.Routes,source.RoutePlan.Routes);assert.deepEqual(w.NetworkPlan.Networks,source.NetworkPlan.Networks);
  assert.equal(w.RoutePlan.Routes.length,35);assert.equal(w.NetworkPlan.Networks[0].Plan.Edges.length,6);assert.ok(w.LayoutAdaptationPlan.Attempts.some(a=>a.Rejection==='METADATA_POPULATION_LAYOUT_CANDIDATE_CHANGED'));
  let totalMass=0,totalArea=0;const cells=[];
  p.Strata.forEach((s,i)=>{connected(s.Cells);const f=integrate(s.Cells,coefficient);near(f.area,s.Area);near(f.mass,s.Mass);near(f.x,s.MassCentroid.X);near(f.y,s.MassCentroid.Y);near(s.Mass,p.Mass/count);totalMass+=f.mass;totalArea+=f.area;cells.push(...s.Cells);
   near(Math.min(...s.BoundaryEdges.map(e=>distance(s.Position,e.From,e.To))),s.Interior.Clearance);assert.ok(s.Interior.Clearance>=.75);
   const node=nodeMap.get(trace.NodeIds[i]);near(node.Position[0],s.Position.X);near(node.Position[1],s.Position.Y);
   for(const c of s.Cells)near(original.Cells.reduce((sum,o)=>sum+area(clip(c.Vertices,o.Vertices)),0),area(c.Vertices));
  });
  disjoint(cells);near(totalArea,original.Area);near(totalArea,p.Area);near(totalMass,p.Mass);near(totalMass,w.TerrainDemandPlan.Results[0].Measure.Mass);assert.equal(Math.floor(totalMass/250),count);assert.equal(w.TerrainDemandPlan.Results[0].Count,count);
  assert.equal(p.PartitionAttempts.filter(a=>a.Status==='Selected').length,count-1);assert.ok(p.PartitionAttempts.some(a=>a.Status==='Rejected'));S.areaPartitionWitness(p,()=>{throw Error('invalid');});
  assert.equal(w.ChunkPlan.Members.length,w.Nodes.length+82);
  assert.deepEqual(fs.readFileSync(`${root}/${name}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 });
 test(`${name}: source dimensions, protected steep terrain and declared budgets remain unchanged`,()=>{
  const w=read(name),recipe=read(name,'recipe.json'),catalog=read(name,'catalog.json');assert.equal(w.Surface.Terrain.Operations[0].Height,1.8);assert.equal(recipe.Regions.Intents[0].MinimumArea,25000);assert.equal(recipe.TerrainDemands.MaximumWork,5000000);assert.equal(recipe.LayoutAdaptation.MaximumWork,200000000);
  const nodes=new Map(w.Nodes.map(n=>[n.Id,n]));for(const site of w.AccessPlan.Sites){const n=nodes.get(site.NodeId),shape=catalog.Archetypes.find(a=>a.Id===n.ArchetypeId).PhysicalFootprint.Polygon;
   shape.Vertices.forEach((p,i)=>{near(p.X+n.Position[0],site.Footprint.Vertices[i].X);near(p.Y+n.Position[1],site.Footprint.Vertices[i].Y);});}
 });
}
test('density counterfactual changes complete derived population without modifying the required content program',()=>{
 const a=read('steep-world'),b=read('richer-world');assert.deepEqual(a.Nodes.filter(n=>n.Kind!=='environment-feature'),b.Nodes.filter(n=>n.Kind!=='environment-feature'));
 assert.deepEqual(a.Surface,b.Surface);assert.deepEqual(read('steep-world','catalog.json'),read('richer-world','catalog.json'));
 assert.equal(a.PlacementPlan.PopulationAreas[0].NodeIds.length,16);assert.equal(b.PlacementPlan.PopulationAreas[0].NodeIds.length,17);
 const recipe=read('richer-world','recipe.json'),base=read('steep-world','recipe.json');for(const r of [recipe,recipe.PopulationSource.Recipe])for(const f of r.Environment.Fields)if(f.Id==='landscape-density')for(const t of f.Terms)t.Coefficient=.002;
 assert.deepEqual(recipe,base);
});

test('viewer symbols remain UTF-8 rather than decoded Windows text',()=>{
 const script=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8');
 for(const broken of ['\u00c2\u00b7','\u00c2\u00b2','\u00e2\u20ac\u201c','\u00e2\u2020\u2019'])assert.equal(script.includes(broken),false);
 for(const symbol of ['\u00b7','\u00b2','\u2192'])assert.ok(script.includes(symbol));
});
