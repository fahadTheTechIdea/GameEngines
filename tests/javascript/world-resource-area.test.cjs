const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/resource-area';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('contained resource consumes source capacity without artificial connections',()=>{
 assert.equal(Schematic.layout(world).boxes.length,4);
 assert.equal(world.Connections.length,0);
 const a=world.ServicePlan.Allocations[0];assert.equal(a.AreaContained,true);assert.equal(a.Physical,false);assert.equal(a.Distance,null);assert.deepEqual(a.ConnectionIds,[]);
 assert.deepEqual(Object.values(world.ServicePlan.Usage),[2]);
 assert.ok(world.ContextPlan.Evaluations.some(e=>e.RuleId==='exclusion:protected-strip'&&e.Passed));
 const reserved=world.PlacementPlan.Placement.Sites[0].Reservation.Footprint.Vertices;
 assert.ok(reserved.every(p=>p.Y>17&&p.Y<=30));
 assert.ok(world.PlacementPlan.FeasibilityAttempts.some(a=>a.Rejection?.startsWith('METADATA_SERVICE_AREA_CONTAINMENT')));
 assert.ok(world.PlacementPlan.FeasibilityAttempts.some(a=>a.Rejection?.startsWith('METADATA_CONTEXT:exclusion:')));
});
test('altered area delivery or excluded-zone evidence refuses display',()=>{
 for(const mutate of [w=>w.ServicePlan.Allocations[0].AreaContained=false,w=>w.ServicePlan.Allocations[0].Physical=true,
  w=>w.ServicePlan.Allocations[0].ConnectionIds=['fake'],w=>w.ServicePlan.Sources.find(s=>s.Supplies.length).Supplies[0].Area=null,
  w=>w.ServicePlan.Sources.find(s=>s.Supplies.length).Supplies[0].Area.Vertices=[],
  w=>w.ContextPlan.Evaluations=w.ContextPlan.Evaluations.filter(e=>!e.RuleId.startsWith('exclusion:')),
  w=>w.ContextPlan.Evaluations.find(e=>e.RuleId.startsWith('exclusion:')).Passed=false]){
  const copy=structuredClone(world);mutate(copy);assert.throws(()=>Schematic.layout(copy),/service delivery|region context/);
 }
});
test('ordinary transported service and previous context records still render',()=>{
 for(const [path,count] of [['generated/shared-services/world-metadata.json',5],['generated/region-context-reviewed/world-metadata.json',4]])
  assert.equal(Schematic.layout(JSON.parse(fs.readFileSync(path))).boxes.length,count);
});
test('current renderer and embedded metadata are exact exported engine results',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const match=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(match[1]),world);
});
