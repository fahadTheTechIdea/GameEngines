const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_ENVIRONMENT_PACKET||'generated/environment-study';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('review retains every authored node and exact environmental decisions without running generation',()=>{
  const plan=Schematic.layout(world);
  assert.equal(plan.boxes.length,5);assert.equal(plan.suitability.size,4);
  let accepted=0,rejected=0;
  for(const observation of world.EnvironmentPlan.Observations){
    assert.deepEqual(plan.suitability.get(observation.NodeId),[observation]);
    const r=observation.Result;
    if(r.Eligible){accepted++;assert.ok(r.Score>=0&&r.Score<=1);}else{rejected++;assert.equal(r.Score,null);assert.ok(r.Rejections.length>0);}
  }
  assert.equal(accepted,1);assert.equal(rejected,3);assert.equal(plan.routes.length,0);
});
test('missing endpoints, contradictory outcomes, unknown fields and required rejection refuse presentation',()=>{
  for(const edit of [w=>w.EnvironmentPlan.Observations[0].NodeId='missing',w=>w.EnvironmentPlan.Observations[0].Result.Observation.Surface.Position.X++,
    w=>w.EnvironmentPlan.Observations[1].Result.Score=.9,w=>w.EnvironmentPlan.Observations[0].Result.Observation.Values.unknown=1,
    w=>w.EnvironmentPlan.Observations.pop(),w=>w.EnvironmentPlan.Rules[0].RequireEligible=true]){
    const bad=structuredClone(world);edit(bad);assert.throws(()=>Schematic.layout(bad));
  }
});
test('packet embeds current viewer and explicit whole-footprint limitation',()=>{
  const source=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8');
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),source);assert.match(source,/Whole footprint not evaluated/);
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1]),world);
});
