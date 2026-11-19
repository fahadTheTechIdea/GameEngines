const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_PLACEMENT_PACKET||'generated/terrain-placement-feedback-final';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('generated positions, footprints, nested nodes and network geometry remain engine-owned',()=>{
  const plan=Schematic.layout(world);
  assert.equal(plan.boxes.length,7);assert.equal(plan.suitability.size,2);assert.equal(plan.routes.length,2);
  for(const binding of world.PlacementPlan.Definition.Bindings){
    const site=world.PlacementPlan.Placement.Sites.find(s=>s.DemandId===binding.DemandId);
    const review=plan.suitability.get(binding.NodeId)[0];
    assert.deepEqual(review.Footprint,site.Footprint);assert.deepEqual(review.Result,site.Point);
    assert.equal(review.Placement.CandidateSource,site.CandidateSource);
    assert.ok(review.Placement.EligibleCandidates>1);
    assert.equal(world.Nodes.filter(n=>n.ParentId===binding.NodeId).length,2);
  }
});
test('missing bindings, contradictory positions or unsuccessful placement refuse presentation',()=>{
  for(const edit of [w=>w.PlacementPlan.Placement.Status='NoFeasibleAssignment',
    w=>w.PlacementPlan.Definition.Bindings[0].NodeId='missing',
    w=>w.PlacementPlan.Placement.Sites[0].Position.X++,
    w=>w.PlacementPlan.Placement.Sites[0].Footprint.Eligible=false,
    w=>w.PlacementPlan.Placement.Diagnostics[0].Eligible=0]){
    const bad=structuredClone(world);edit(bad);assert.throws(()=>Schematic.layout(bad));
  }
});
test('current packaged viewer embeds exact generated world without recomputing placement',()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1]),world);
});
