const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const read=p=>JSON.parse(fs.readFileSync(p,'utf8'));
const packets=['initial','moved'].map(name=>({name,folder:`generated/protection-${name}`,world:read(`generated/protection-${name}/world-metadata.json`)}));
test('complete protected program and exact display geometry survive publication',()=>{
 for(const {world,folder} of packets){
  const source=world.PlacementPlan.Definition.SourceWorld,view=Schematic.layout(world);
  assert.equal(world.Nodes.length,12);assert.equal(source.Nodes.length,8);assert.equal(world.AccessPlan.Sites.length,11);
  assert.equal(world.RoutePlan,null);assert.equal(world.NetworkPlan,null);
  assert.deepEqual(world.ContextPlan.Protections,source.ContextPlan.Protections);
  assert.deepEqual(view.protections,world.ContextPlan.Protections);
  for(const n of source.Nodes)assert.deepEqual(world.Nodes.find(m=>m.Id===n.Id),n);
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
  assert.deepEqual(JSON.parse(embedded[1]),world);
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 }
 assert.equal(fs.existsSync('generated/protection-refused'),false);
});
test('moving the view changes only its envelope and retains all physical content',()=>{
 const [a,b]=packets.map(p=>p.world);
 assert.deepEqual(a.Nodes,b.Nodes);assert.deepEqual(a.AccessPlan,b.AccessPlan);
 assert.deepEqual(a.ContextPlan.Protections[0],b.ContextPlan.Protections[0]);
 const [v,w]=[a,b].map(p=>p.ContextPlan.Protections.find(p=>p.Definition.Id==='view'));
 assert.deepEqual(v.Centerline.Points,[{X:5,Y:5},{X:45,Y:5}]);
 assert.deepEqual(w.Centerline.Points,[{X:5,Y:6},{X:45,Y:6}]);
 assert.equal(v.Boundary.Vertices.length,w.Boundary.Vertices.length);
 v.Boundary.Vertices.forEach((p,i)=>{assert.ok(Math.abs(p.X-w.Boundary.Vertices[i].X)<1e-9);assert.ok(Math.abs(p.Y+1-w.Boundary.Vertices[i].Y)<1e-9);});
});
test('display refuses missing protection records, stale anchors and absent evaluations',()=>{
 const reject=change=>{const world=structuredClone(packets[0].world);change(world);assert.throws(()=>Schematic.layout(world),/Invalid (protected gameplay space|region context record)/);};
 reject(w=>w.ContextPlan.Protections=null);
 reject(w=>w.ContextPlan.Protections.pop());
 reject(w=>w.ContextPlan.Protections[0].Boundary.Vertices[0].X++);
 reject(w=>w.ContextPlan.Protections[1].Centerline.Points[0].Y++);
 reject(w=>w.ContextPlan.Protections[1].Boundary.Vertices[0].X=NaN);
 reject(w=>w.ContextPlan.Evaluations=w.ContextPlan.Evaluations.filter(e=>!e.RuleId.startsWith('exclusion:')));
});
