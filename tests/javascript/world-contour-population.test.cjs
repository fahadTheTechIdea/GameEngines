const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const root=process.env.CONTOUR_POPULATION_PACKET||'generated/field-contour-population-accepted';
const read=name=>JSON.parse(fs.readFileSync(`${root}/${name}/world-metadata.json`,'utf8'));
for(const name of ['baseline','raised-terrain'])test(`${name}: complete source program and exact generated contour paths survive export`,()=>{
 const world=read(name),view=Schematic.layout(world),source=world.PlacementPlan.Definition.SourceWorld;
 assert.equal(world.Nodes.length,37);assert.equal(world.AccessPlan.Sites.length,36);assert.equal(world.RoutePlan.Routes.length,19);
 assert.equal(world.PlacementPlan.ProgramInstances.length,14);assert.equal(world.PlacementPlan.PopulationContours.length,3);
 assert.deepEqual(view.populationContours,world.PlacementPlan.PopulationContours);
 for(const node of source.Nodes)assert.deepEqual(world.Nodes.find(n=>n.Id===node.Id),node);
 for(const route of source.RoutePlan.Routes)assert.deepEqual(world.RoutePlan.Routes.find(r=>r.ConnectionId===route.ConnectionId),route);
 const nodes=new Map(world.Nodes.map(n=>[n.Id,n]));let members=0;
 for(const trace of view.populationContours){
  assert.equal(trace.FieldId,'height');assert.equal(trace.Contours.length,1);
  const value=trace.RegionId==='lowland'?-4:trace.RegionId==='highland'?4:0;
  for(const p of trace.Contours[0].Path.Points)assert.ok(Math.abs((name==='baseline'?0:.2)+.1*p.X+.04*p.Y-value)<1e-6,'independent affine contour-path oracle');
  trace.Contours[0].Positions.forEach((p,i)=>{assert.deepEqual(nodes.get(trace.NodeIds[i]).Position,[p.X,p.Y]);members++;});
 }
 assert.equal(members,8);
 const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${root}/${name}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(embedded[1]),world);
 assert.equal(fs.readFileSync(`${root}/${name}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('terrain edits change every contour-derived feature and retain exact counts',()=>{
 const a=read('baseline'),b=read('raised-terrain');
 const features=a.Nodes.filter(n=>n.Kind==='environment-feature');assert.equal(features.length,8);
 for(const node of features)assert.notDeepEqual(node.Position,b.Nodes.find(n=>n.Id===node.Id).Position);
 assert.notDeepEqual(a.PlacementPlan.PopulationContours,b.PlacementPlan.PopulationContours);
 assert.deepEqual(a.Connections,b.Connections);
});
test('consumer refuses missing traces, displaced member witnesses and detached contour paths',()=>{
 const missing=read('baseline');missing.PlacementPlan.PopulationContours=[];assert.throws(()=>Schematic.layout(missing),/Invalid population contour witness/);
 const position=read('baseline');position.PlacementPlan.PopulationContours[0].Contours[0].Positions[0].X+=.1;assert.throws(()=>Schematic.layout(position),/Invalid population contour witness/);
 const path=read('baseline');path.PlacementPlan.PopulationContours[0].Contours[0].Path.Points.forEach(p=>p.X+=1);assert.throws(()=>Schematic.layout(path),/Invalid population contour witness/);
});
