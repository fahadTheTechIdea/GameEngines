const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/region-context-reviewed';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('context keeps source dimensions and displays recorded successful region checks',()=>{
  assert.equal(Schematic.layout(world).boxes.length,4);
  assert.equal(world.ContextPlan.Sources[0].PolicyId,'modern.siting');
  assert.equal(world.ContextPlan.Evaluations.length,2);
  assert.ok(world.ContextPlan.Evaluations.every(e=>e.Passed));
  const site=world.PlacementPlan.Placement.Sites[0].Footprint.Footprint.Vertices;
  assert.equal(Math.max(...site.map(p=>p.X))-Math.min(...site.map(p=>p.X)),8);
  assert.equal(Math.max(...site.map(p=>p.Y))-Math.min(...site.map(p=>p.Y)),10);
  assert.ok(site.every(p=>p.Y>=15));
  assert.ok(world.PlacementPlan.FeasibilityAttempts.some(a=>a.Rejection?.startsWith('METADATA_CONTEXT:region-containment')));
});
test('tampered context, missing coverage, altered policy and failed decisions refuse display',()=>{
  for(const mutate of [w=>w.ContextPlan=null,w=>w.ContextPlan.Evaluations=[],w=>w.ContextPlan.Evaluations[0].Passed=false,
    w=>w.ContextPlan.Sources=[],w=>w.ContextPlan.Sources[0].PolicyId='forged',w=>w.ContextPlan.Sources[0].Tags=[],
    w=>w.ContextPlan.Definition.Regions[0].Use='incompatible',w=>w.ContextPlan.Evaluations[0].RelatedId='missing',
    w=>w.ContextPlan.Evaluations[1]=w.ContextPlan.Evaluations[0]]){
    const copy=structuredClone(world);mutate(copy);assert.throws(()=>Schematic.layout(copy),/region context/);
  }
});
test('current renderer still reads earlier metadata without context',()=>{
  const prior=JSON.parse(fs.readFileSync('generated/shared-services/world-metadata.json'));
  assert.equal(Schematic.layout(prior).boxes.length,5);
});
test('review output contains exact current renderer and engine JSON',()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  const embedded=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1];
  assert.deepEqual(JSON.parse(embedded),world);
});

test('generic wilderness context preserves the required edge buffer and detects altered evidence',()=>{
  const world=JSON.parse(fs.readFileSync('generated/wilderness-context/world-metadata.json'));
  assert.equal(Schematic.layout(world).boxes.length,4);
  const separation=world.ContextPlan.Evaluations.find(e=>e.RuleId==='separation:rest-buffer');
  assert.ok(separation.Distance>=10);
  assert.ok(world.ContextPlan.Sources.every(s=>s.PolicyId===null));
  separation.Distance=9;assert.throws(()=>Schematic.layout(world),/region context/);
});
