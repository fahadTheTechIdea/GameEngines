const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/catalog-supply-policy-reviewed';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('registered policy records cover the exact active consumer requirements',()=>{
  assert.equal(Schematic.layout(world).boxes.length,4);
  assert.equal(world.ServicePlan.PolicyEvaluations.length,4);
  assert.ok(world.ServicePlan.PolicyEvaluations.every(p=>p.PolicyId==='modern.siting'));
  const source=world.ServicePlan.Sources.find(s=>s.NodeId==='placement-feedback/sites-0');
  assert.deepEqual(source.RemainingRequirements,['catalog-context']);
  const footprint=world.PlacementPlan.Footprints[0].Polygon.Vertices;
  assert.equal(Math.abs(footprint[1].X-footprint[0].X),8);
  assert.equal(Math.abs(footprint[2].Y-footprint[1].Y),10);
});
test('missing, forged, duplicate and wrong-source policy results refuse display',()=>{
  for(const edit of [w=>w.ServicePlan.PolicyEvaluations=null,w=>w.ServicePlan.PolicyEvaluations[0].PolicyId='invented',
    w=>w.ServicePlan.PolicyEvaluations[0].SourceId='other-source',
    w=>w.ServicePlan.PolicyEvaluations[0]=w.ServicePlan.PolicyEvaluations[1]]){
    const bad=structuredClone(world);edit(bad);assert.throws(()=>Schematic.layout(bad),/service delivery/);
  }
});
test('unregistered logical services still have no catalog callback requirement',()=>{
  const earlier=JSON.parse(fs.readFileSync('generated/shared-services/world-metadata.json'));
  assert.equal(Schematic.layout(earlier).boxes.length,5);
});
test('review packet contains the current renderer and exact engine output',()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1]),world);
});
