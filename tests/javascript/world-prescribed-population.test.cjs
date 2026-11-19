const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.PRESCRIBED_POPULATION_PACKET||'generated/prescribed-population-scale-accepted';
const read=name=>JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
for(const name of ['baseline','raised-terrain'])test(`${name}: all 200 prescribed placements and 194 field-derived members survive export`,()=>{
 const world=read(name),view=Schematic.layout(world),placement=world.PlacementPlan,source=placement.Definition.SourceWorld,rootPlan=placement.Placement;
 assert.equal(world.Nodes.length,223);assert.equal(world.AccessPlan.Sites.length,222);assert.equal(world.RoutePlan.Routes.length,19);
 assert.equal(rootPlan.Request.Execution,'Prescribed');assert.equal(rootPlan.Sites.length,200);assert.equal(rootPlan.SearchStates,1);
 assert.ok(rootPlan.Diagnostics.every(d=>d.Candidates===1&&d.Eligible===1));assert.equal(placement.ProgramInstances.length,14);
 assert.deepEqual(view.populationContours,placement.PopulationContours);
 assert.equal(view.populationContours.reduce((n,t)=>n+t.Contours.length,0),13);
 for(const node of source.Nodes)assert.deepEqual(world.Nodes.find(n=>n.Id===node.Id),node);
 for(const route of source.RoutePlan.Routes)assert.deepEqual(world.RoutePlan.Routes.find(r=>r.ConnectionId===route.ConnectionId),route);
 const nodes=new Map(world.Nodes.map(n=>[n.Id,n]));let count=0;
 for(const trace of view.populationContours){
  const requests=placement.TemplateDefinition.Bindings.find(b=>b.DemandId===trace.DemandId).Population.Region.Contour.Contours;let index=0;
  trace.Contours.forEach((contour,i)=>{
   for(const p of contour.Path.Points)assert.ok(Math.abs((name==='baseline'?0:.2)+.1*p.X+.04*p.Y-requests[i].Value)<1e-6,'independent affine path equation');
   for(const p of contour.Positions){assert.deepEqual(nodes.get(trace.NodeIds[index++]).Position,[p.X,p.Y]);count++;}
  });
 }
 assert.equal(count,194);
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${root}/${name}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);assert.equal(fs.readFileSync(`${root}/${name}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('terrain counterfactual moves all 194 members without dropping content or topology',()=>{
 const a=read('baseline'),b=read('raised-terrain'),members=a.Nodes.filter(n=>n.Kind==='environment-feature');
 assert.equal(members.length,194);assert.deepEqual(a.Connections,b.Connections);
 for(const node of members)assert.notDeepEqual(node.Position,b.Nodes.find(n=>n.Id===node.Id).Position);
 assert.deepEqual(a.Nodes.map(({Position,...node})=>node),b.Nodes.map(({Position,...node})=>node));
});
test('consumer enforces execution mode and original search bound',()=>{
 const search=read('baseline');search.PlacementPlan.Placement.Request.Execution='Search';assert.throws(()=>Schematic.layout(search),/Invalid placement plan/);
 const unknown=read('baseline');unknown.PlacementPlan.Placement.Request.Execution='Unknown';assert.throws(()=>Schematic.layout(unknown),/Invalid placement plan/);
 const rules=read('baseline');rules.EnvironmentPlan.Rules=Array.from({length:17},()=>({}));assert.throws(()=>Schematic.layout(rules),/Invalid environmental plan/);
});
test('consumer refuses missing exact intent, multiple candidate receipts and altered assignment states',()=>{
 const missing=read('baseline');missing.PlacementPlan.Placement.Request.Relations=missing.PlacementPlan.Placement.Request.Relations.filter(r=>r.FromId!=='lowland-features-0');assert.throws(()=>Schematic.layout(missing),/Invalid prescribed placement witness/);
 const diagnostic=read('baseline');diagnostic.PlacementPlan.Placement.Diagnostics[0].Candidates=2;assert.throws(()=>Schematic.layout(diagnostic),/Invalid prescribed placement witness/);
 const states=read('baseline');states.PlacementPlan.Placement.SearchStates=2;assert.throws(()=>Schematic.layout(states),/Invalid prescribed placement witness/);
});
