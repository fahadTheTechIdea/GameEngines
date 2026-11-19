const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/clearance-world-reviewed';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('physical footprint and reservation remain distinct exact engine polygons',()=>{
  const plan=Schematic.layout(world);assert.equal(plan.boxes.length,3);assert.equal(plan.routes.length,2);
  for(const binding of world.PlacementPlan.Definition.Bindings){
    const site=world.PlacementPlan.Placement.Sites.find(s=>s.DemandId===binding.DemandId),entries=plan.suitability.get(binding.NodeId);
    assert.equal(entries.length,2);
    assert.deepEqual(entries[0].Footprint,site.Footprint);assert.deepEqual(entries[1].Footprint,site.Reservation);
    assert.equal(entries[1].Placement.IsReservation,true);
    const edge=p=>Math.hypot(p[1].X-p[0].X,p[1].Y-p[0].Y);
    assert.ok(Math.abs(edge(site.Footprint.Footprint.Vertices)-4)<1e-8);
    assert.ok(Math.abs(edge(site.Reservation.Footprint.Vertices)-10)<1e-8);
    assert.deepEqual(plan.boxes.find(b=>b.id===binding.NodeId).physicalAccess.Footprint,site.Footprint.Footprint);
  }
});
test('missing, conflicting, moved or unqualified reservation data refuses presentation',()=>{
  for(const edit of [w=>w.PlacementPlan.Placement.Sites[0].Reservation=null,
    w=>w.PlacementPlan.Footprints[0].Reservation=null,
    w=>w.PlacementPlan.Placement.Sites[0].Reservation.Footprint.Vertices[0].X++,
    w=>w.PlacementPlan.Placement.Sites[0].Reservation.Eligible=false,
    w=>w.PlacementPlan.Placement.Request.Demands[0].Reservation=null]){
    const bad=structuredClone(world);edit(bad);assert.throws(()=>Schematic.layout(bad));
  }
});
test('optional reservation leaves capacity and feedback outputs intact',()=>{
  for(const [packet,nodes] of [['capacity-world-final',10],['placement-feedback-final',4]]){
    assert.equal(Schematic.layout(JSON.parse(fs.readFileSync(`generated/${packet}/world-metadata.json`))).boxes.length,nodes);
  }
});
test('export embeds current viewer and unchanged engine data',()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1]),world);
});
