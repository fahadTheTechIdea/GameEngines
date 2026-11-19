const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.WORLD_LAYERED_ROOT||'generated/layered-world';
const read=(name,file='world-metadata.json')=>JSON.parse(fs.readFileSync(`${root}/${name}/${file}`));
function near(a,b){assert.ok(Math.abs(a-b)<1e-6*Math.max(1,Math.abs(a),Math.abs(b)),`${a} / ${b}`);}
function integrate(cells,density){let area=0,mass=0,mx=0,my=0;
 for(const {Vertices:v} of cells){const a=v[0];for(let i=1;i+1<v.length;i++){
  const b=v[i],c=v[i+1],ar=((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X))/2;area+=ar;
  for(const [u,w,t] of [[2/3,1/6,1/6],[1/6,2/3,1/6],[1/6,1/6,2/3]]){
   const x=u*a.X+w*b.X+t*c.X,y=u*a.Y+w*b.Y+t*c.Y,d=density?1+.002*x:1;mass+=ar*d/3;mx+=ar*x*d/3;my+=ar*y*d/3;
  }
 }}return {area,mass,x:mx/mass,y:my/mass};}
const bounds=polygon=>({minX:Math.min(...polygon.Vertices.map(p=>p.X)),maxX:Math.max(...polygon.Vertices.map(p=>p.X)),minY:Math.min(...polygon.Vertices.map(p=>p.Y)),maxY:Math.max(...polygon.Vertices.map(p=>p.Y))});
for(const name of ['inland','coastal']){
 const world=read(name),nodes=new Map(world.Nodes.map(n=>[n.Id,n])),sites=new Map(world.AccessPlan.Sites.map(s=>[s.NodeId,s]));
 test(`${name}: full layered counts and coordinate-free program intent survive the schematic`,()=>{
  const view=Schematic.layout(world);assert.equal(view.boxes.length,61);assert.equal(world.AccessPlan.Sites.length,60);
  assert.equal(world.RoutePlan.Routes.length,35);assert.equal(world.NetworkPlan.Networks[0].Plan.Edges.length,6);
  assert.equal(world.Nodes.filter(n=>n.Kind==='compound').length,7);assert.equal(world.Nodes.filter(n=>n.Kind==='structure').length,14);
  assert.deepEqual(world.RegionPlan.Request.Seeds,[]);assert.equal(world.RegionPlan.Request.Intents.length,1);
  assert.equal(world.LocalLayoutPlan.GeneratedAreas.length,3);
  for(const l of world.LocalLayoutPlan.Definition.Layouts){assert.equal(l.Strategy,'AreaBalanced');assert.deepEqual(l.Programs,[]);assert.equal(l.RegionFrame,null);}
  assert.deepEqual(fs.readFileSync(`${root}/${name}/schematic.js`),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js'));
 });
 test(`${name}: independent area/density integration funds every actual landscape member`,()=>{
  for(const trace of world.LocalLayoutPlan.GeneratedAreas){
   const plan=trace.AreaPlan,intents=world.LocalLayoutPlan.Definition.Layouts.find(l=>l.Id===trace.LayoutId).ProgramIntents,total=intents.reduce((s,i)=>s+i.MinimumArea,0);
   assert.deepEqual(plan.RelativeWeights,intents.map(i=>i.MinimumArea));
   plan.Strata.forEach((s,i)=>{const f=integrate(s.Cells,false);near(f.area,s.Area);near(f.mass,s.Mass);near(f.x,s.Position.X);near(f.y,s.Position.Y);near(s.Mass,plan.Mass*intents[i].MinimumArea/total);});
  }
  const trace=world.PlacementPlan.PopulationAreas[0],result=world.TerrainDemandPlan.Results[0];let area=0,mass=0;
  trace.Plan.Strata.forEach((s,i)=>{const f=integrate(s.Cells,true);near(f.area,s.Area);near(f.mass,s.Mass);near(f.x,s.Position.X);near(f.y,s.Position.Y);area+=f.area;mass+=f.mass;
   const n=nodes.get(trace.NodeIds[i]);near(n.Position[0],f.x);near(n.Position[1],f.y);
  });
  near(area,trace.Plan.Area);near(mass,trace.Plan.Mass);near(mass,result.Measure.Mass);assert.equal(Math.floor(mass/250),result.Count);assert.equal(result.Count,18);assert.equal(trace.NodeIds.length,18);
 });
 test(`${name}: exact source dimensions, nested containment and all interaction connections`,()=>{
  const catalog=read(name,'catalog.json');
  for(const site of sites.values()){
   const node=nodes.get(site.NodeId),source=catalog.Archetypes.find(a=>a.Id===node.ArchetypeId).PhysicalFootprint.Polygon;
   assert.equal(source.Vertices.length,site.Footprint.Vertices.length);
   source.Vertices.forEach((p,i)=>{near(p.X+node.Position[0],site.Footprint.Vertices[i].X);near(p.Y+node.Position[1],site.Footprint.Vertices[i].Y);});
   if(node.ParentId!==world.Id){const a=bounds(site.Footprint),b=bounds(sites.get(node.ParentId).Footprint);assert.ok(a.minX>=b.minX-1e-7&&a.maxX<=b.maxX+1e-7&&a.minY>=b.minY-1e-7&&a.maxY<=b.maxY+1e-7);}
  }
  const source=world.PlacementPlan.Definition.SourceWorld;for(const n of source.Nodes)assert.deepEqual(nodes.get(n.Id),n);
  assert.deepEqual(source.RoutePlan.Routes,world.RoutePlan.Routes);assert.deepEqual(source.NetworkPlan.Networks,world.NetworkPlan.Networks);
  const graph=new Map([...sites.values()].filter(s=>s.Ports.length).map(s=>[s.NodeId,[]]));
  for(const l of world.Connections){assert.ok(graph.has(l.FromId)&&graph.has(l.ToId));graph.get(l.FromId).push(l.ToId);if(l.Bidirectional)graph.get(l.ToId).push(l.FromId);}
  const reached=new Set(),queue=[graph.keys().next().value];while(queue.length){const id=queue.shift();if(reached.has(id))continue;reached.add(id);queue.push(...graph.get(id));}assert.equal(reached.size,42);
 });
 test(`${name}: chunk ownership covers every node and every directed path exactly once`,()=>{
  const paths=world.RoutePlan.Routes.flatMap(r=>[r.Forward,r.Reverse]).concat(world.NetworkPlan.Networks.flatMap(n=>n.Plan.Edges.flatMap(e=>[e.Forward,e.Reverse])));
  assert.equal(paths.length,82);const expected=new Map([...world.Nodes.map(n=>[`Node/${n.Id}`,{X:n.Position[0],Y:n.Position[1]}]),...paths.map(p=>[`Path/${p.Id}`,p.Path.Points[0]])]);
  const chunks=world.ChunkPlan,def=chunks.Definition,cells=new Map(chunks.Cells.map(c=>[c.Id,c]));assert.equal(chunks.Members.length,143);assert.equal(expected.size,143);
  for(const m of chunks.Members){const id=`${m.Kind}/${m.Id}`,p=expected.get(id),cell=cells.get(m.OwnerChunkId);assert.ok(p&&cell);expected.delete(id);
   assert.equal(m.OwnerChunkId,`${Math.floor((p.X-def.Origin.X)/def.Width)}:${Math.floor((p.Y-def.Origin.Y)/def.Height)}`);
   near(cell.Origin.X+m.OwnerLocalAnchor.X,p.X);near(cell.Origin.Y+m.OwnerLocalAnchor.Y,p.Y);assert.ok(m.ChunkIds.includes(m.OwnerChunkId));
  }assert.equal(expected.size,0);
 });
}
test('coastal counterfactual changes generated spaces and paths while retaining mandatory source identities',()=>{
 const a=read('inland'),b=read('coastal');assert.deepEqual(a.Nodes.filter(n=>n.Kind!=='environment-feature').map(({Position,...n})=>n),b.Nodes.filter(n=>n.Kind!=='environment-feature').map(({Position,...n})=>n));
 assert.notDeepEqual(a.Nodes.map(n=>n.Position),b.Nodes.map(n=>n.Position));assert.notDeepEqual(a.LocalLayoutPlan.GeneratedAreas,b.LocalLayoutPlan.GeneratedAreas);
 assert.notDeepEqual(a.NetworkPlan.Networks,b.NetworkPlan.Networks);
});
