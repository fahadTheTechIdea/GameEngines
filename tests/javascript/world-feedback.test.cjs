const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/placement-feedback-final';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));

test('feasibility inspection preserves engine attempts and accepted geometry',()=>{
  const plan=Schematic.layout(world);
  assert.deepEqual(plan.feasibility.attempts,world.PlacementPlan.FeasibilityAttempts);
  assert.ok(plan.feasibility.attempts.some(a=>a.Rejection?.startsWith('METADATA_ACCESS_APPROACH_BLOCKED')));
  assert.ok(plan.feasibility.attempts.some(a=>a.Rejection?.startsWith('METADATA_ROUTE_UNRESOLVED')));
  assert.deepEqual(world.Nodes.find(n=>n.Id==='placement-feedback/sites-0').Position,[10.5,22.75]);
  const route=plan.routes.find(r=>!r.reverse);
  assert.deepEqual(route.points[0],{x:10.5,y:16.75});
  assert.deepEqual(route.points.at(-1),{x:35,y:25});
  assert.ok(route.points.every(p=>p.y>16));
});
test('incomplete, reordered or malformed feasibility diagnostics reject',()=>{
  for(const edit of [w=>w.PlacementPlan.Feedback=null,w=>w.PlacementPlan.FeasibilityAttempts.pop(),
    w=>w.PlacementPlan.FeasibilityAttempts.shift(),w=>w.PlacementPlan.FeasibilityAttempts[0].Accepted=true,
    w=>w.PlacementPlan.FeasibilityAttempts[0].CandidateSha256='bad',
    w=>w.PlacementPlan.FeasibilityAttempts[0].Work=Number.MAX_SAFE_INTEGER]){
    const invalid=structuredClone(world);edit(invalid);assert.throws(()=>Schematic.layout(invalid));
  }
});
test('feedback export embeds current viewer and exact engine data',()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1]),world);
});
