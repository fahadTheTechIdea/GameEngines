const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.AREA_POPULATION_PACKET||'generated/area-population-accepted';
const read=name=>JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
const near=(a,b,message)=>assert.ok(Math.abs(a-b)<1e-7*Math.max(1,Math.abs(b)),`${message}: ${a} vs ${b}`);
// Independent degree-two triangle quadrature: three edge midpoints integrate
// affine density and its first moments exactly. C# uses barycentric formulas.
function integrate(cells,sign){let area=0,mass=0,mx=0,my=0;
 for(const cell of cells){const v=cell.Vertices,a=v[0];for(let i=1;i+1<v.length;i++){
  const b=v[i],c=v[i+1],ar=((b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X))/2;area+=ar;
  for(const [p,q] of [[a,b],[b,c],[c,a]]){const x=(p.X+q.X)/2,y=(p.Y+q.Y)/2,rho=3+sign*(.01*x+.004*y),dm=ar*rho/3;mass+=dm;mx+=x*dm;my+=y*dm;}
 }}return {area,mass,x:mx/mass,y:my/mass};
}
for(const name of ['baseline','reversed-density'])test(`${name}: all required content and independently integrated area witnesses survive export`,()=>{
 const w=read(name),view=Schematic.layout(w),source=w.PlacementPlan.Definition.SourceWorld,sign=name==='baseline'?1:-1;
 assert.equal(w.Nodes.length,223);assert.equal(w.AccessPlan.Sites.length,222);assert.equal(w.RoutePlan.Routes.length,19);assert.equal(w.PlacementPlan.ProgramInstances.length,14);
 assert.deepEqual(view.populationAreas,w.PlacementPlan.PopulationAreas);assert.equal(view.populationAreas.length,3);assert.equal(w.PlacementPlan.PopulationContours,null);
 const nodes=new Map(w.Nodes.map(n=>[n.Id,n]));let count=0;
 for(const trace of view.populationAreas){
  const facts=trace.Plan,expectedArea={lowland:4039/2,highland:445455/272,woodland:68}[trace.RegionId];
  const expectedMass=(sign===1?{lowland:75240547/15000,highland:108930607659/18496000,woodland:25806/125}:{lowland:106514453/15000,highland:72815032341/18496000,woodland:25194/125})[trace.RegionId];
  near(facts.Area,expectedArea,'independent rational habitat area');near(facts.Mass,expectedMass,'independent rational habitat mass');
  facts.Strata.forEach((stratum,i)=>{
   const actual=integrate(stratum.Cells,sign);near(actual.area,stratum.Area,'polygon area');near(actual.mass,stratum.Mass,'quadrature mass');near(actual.x,stratum.Position.X,'quadrature weighted X');near(actual.y,stratum.Position.Y,'quadrature weighted Y');
   near(stratum.Mass,facts.Mass/facts.Strata.length,'equal required mass');assert.deepEqual(nodes.get(trace.NodeIds[i]).Position,[stratum.Position.X,stratum.Position.Y]);count++;
  });
 }
 assert.equal(count,194);for(const node of source.Nodes)assert.deepEqual(w.Nodes.find(n=>n.Id===node.Id),node);
 for(const route of source.RoutePlan.Routes)assert.deepEqual(w.RoutePlan.Routes.find(r=>r.ConnectionId===route.ConnectionId),route);
 const html=fs.readFileSync(`${root}/${name}/schematic.html`,'utf8');assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(html)[1]),w);
 assert.match(html,/<input type="checkbox" data-toggle="population">/);assert.equal(fs.readFileSync(`${root}/${name}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('density response moves all 194 members while source compound geometry and paths remain exact',()=>{
 const a=read('baseline'),b=read('reversed-density'),sa=a.PlacementPlan.Definition.SourceWorld,sb=b.PlacementPlan.Definition.SourceWorld;
 for(const node of a.Nodes.filter(n=>n.Kind==='environment-feature'))assert.notDeepEqual(node.Position,b.Nodes.find(n=>n.Id===node.Id).Position);
 for(const key of ['Nodes','RoutePlan','RegionPlan','LocalLayoutPlan','Surface'])assert.deepEqual(sa[key],sb[key]);
 assert.deepEqual(a.Connections,b.Connections);
});
test('consumer refuses missing, unbalanced, displaced and altered cell witnesses',()=>{
 const missing=read('baseline');missing.PlacementPlan.PopulationAreas=[];assert.throws(()=>Schematic.layout(missing),/Invalid population area witness/);
 const mass=read('baseline');mass.PlacementPlan.PopulationAreas[0].Plan.Strata[0].Mass+=1;assert.throws(()=>Schematic.layout(mass),/Invalid population area witness/);
 const point=read('baseline');point.PlacementPlan.PopulationAreas[0].Plan.Strata[0].Position.X+=.1;assert.throws(()=>Schematic.layout(point),/Invalid population area witness/);
 const cell=read('baseline');cell.PlacementPlan.PopulationAreas[0].Plan.Strata[0].Cells[0].Vertices[0].X+=2;assert.throws(()=>Schematic.layout(cell),/Invalid population area witness/);
});
