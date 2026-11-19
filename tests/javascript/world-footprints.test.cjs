const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_FOOTPRINT_PACKET||'generated/footprint-study-qualified';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('footprints retain exact engine vertices, scalar ranges and separate point observations',()=>{
  const plan=Schematic.layout(world);assert.equal(plan.suitability.size,4);
  for(const observation of world.EnvironmentPlan.Observations){
    assert.deepEqual(plan.suitability.get(observation.NodeId),[observation]);
    assert.equal(observation.Footprint.Footprint.Vertices.length,4);
    assert.equal(Object.keys(observation.Footprint.Ranges).length,4);
  }
  assert.ok(world.EnvironmentPlan.Observations.some(o=>o.Result.Eligible&&!o.Footprint.Eligible));
});
test('missing, shifted, contradictory and undeclared footprint results refuse display',()=>{
  for(const edit of [w=>w.EnvironmentPlan.Observations[0].Footprint=null,
    w=>w.EnvironmentPlan.Observations[0].Footprint.Footprint.Vertices[0].X++,
    w=>w.EnvironmentPlan.Observations[0].Footprint.Ranges.slope.Minimum=999,
    w=>w.EnvironmentPlan.Observations[0].Footprint.Eligible=true,
    w=>w.EnvironmentPlan.Rules[0].Footprint=null,
    w=>w.EnvironmentPlan.Observations[0].Footprint.Work=1e10]){
    const bad=structuredClone(world);edit(bad);assert.throws(()=>Schematic.layout(bad));
  }
});
test('packet contains current viewer and engine results for offline inspection',()=>{
  const source=fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8');
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),source);
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1]),world);
});
test('fit extent includes rejected footprints outside the terrain boundary',()=>{
  const changed=structuredClone(world),rule=changed.EnvironmentPlan.Rules[0];
  rule.Footprint.Vertices[0].X=-1000;
  for(const observation of changed.EnvironmentPlan.Observations){
    const node=changed.Nodes.find(n=>n.Id===observation.NodeId);
    observation.Footprint.Footprint.Vertices[0].X=node.Position[0]-1000;
  }
  const plan=Schematic.layout(changed);
  assert.ok(plan.minX<=-984);
});
