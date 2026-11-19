const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const read=p=>JSON.parse(fs.readFileSync(p));
const logical=read('generated/shared-services/world-metadata.json');
const physical=read('generated/physical-services/world-metadata.json');
test('logical service graph preserves all consumers and shared capacity',()=>{
  assert.equal(Schematic.layout(logical).boxes.length,5);
  assert.equal(logical.ServicePlan.Usage['source:service-world/supply-0/stock'],8);
  assert.equal(logical.ServicePlan.Usage['link:service-world/explicit/trunk'],8);
  assert.ok(logical.ServicePlan.Allocations.every(a=>!a.Physical&&a.Distance===null));
});
test('physical service feedback retains saved geometry and entrance-inclusive distance',()=>{
  const plan=Schematic.layout(physical),allocation=physical.ServicePlan.Allocations[0];
  assert.equal(plan.boxes.length,4);
  assert.ok(plan.feasibility.attempts.some(a=>a.Rejection?.startsWith('METADATA_SERVICE_CATCHMENT_EXCEEDED')));
  assert.ok(allocation.Physical&&allocation.Distance<=30);
  assert.ok(Math.abs(allocation.Distance-physical.RoutePlan.Routes[0].Reverse.Cost.Distance-4)<1e-8);
});
test('inconsistent quantity, chain, capacity and omitted requirements refuse display',()=>{
  for(const change of [w=>w.ServicePlan.Allocations[0].Quantity++,w=>w.ServicePlan.Allocations.pop(),
    w=>w.ServicePlan.Usage['link:service-world/explicit/trunk']=0,
    w=>w.ServicePlan.Definition.Links[0].Capacity=1,
    w=>w.ServicePlan.Allocations[0].ConnectionIds.reverse(),
    w=>w.ServicePlan.Allocations[0].Physical=true]){
    const bad=structuredClone(logical);change(bad);assert.throws(()=>Schematic.layout(bad),/service delivery/);
  }
});
test('physical distance and missing feedback are not silently accepted',()=>{
  for(const change of [w=>w.ServicePlan.Allocations[0].Distance=31,w=>w.PlacementPlan.Feedback=null]){
    const bad=structuredClone(physical);change(bad);assert.throws(()=>Schematic.layout(bad));
  }
});
test('optional service capability preserves earlier geometry packets',()=>{
  for(const [packet,count] of [['source-surroundings-identity',2],['clearance-world-reviewed',3],['capacity-world-final',10],['placement-feedback-final',4]])
    assert.equal(Schematic.layout(read(`generated/${packet}/world-metadata.json`)).boxes.length,count);
});
test('service packets contain current renderer and exact saved data',()=>{
  for(const folder of ['shared-services','physical-services']){
    assert.equal(fs.readFileSync(`generated/${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
    assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`generated/${folder}/schematic.html`,'utf8'))[1]),read(`generated/${folder}/world-metadata.json`));
  }
});
