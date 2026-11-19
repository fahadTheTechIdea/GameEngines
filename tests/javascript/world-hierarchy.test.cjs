const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_HIERARCHY_PACKET||'generated/hierarchical-placement-feedback-final';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));

test('all nested catalog polygons retain source dimensions, parent lineage and focus bounds',()=>{
  const plan=Schematic.layout(world);
  assert.equal(plan.boxes.length,11);assert.equal(plan.suitability.size,10);assert.equal(plan.routes.length,2);
  assert.equal(world.PlacementPlan.NestedStages.length,6);
  for(const [nodeId,entries] of plan.suitability){
    const review=entries[0],source=review.Placement.FootprintSource;
    assert.ok(Array.isArray(source.RemainingRequirements));
    assert.equal(source.NodeId,nodeId);
    const node=world.Nodes.find(n=>n.Id===nodeId);
    if(review.Placement.ParentNodeId)assert.equal(node.ParentId,review.Placement.ParentNodeId);
    const box=plan.boxes.find(b=>b.node.Id===nodeId);
    assert.ok(box.catalogFootprint);assert.ok(box.focusBounds.width>0&&box.focusBounds.height>0);
    const vertices=source.Polygon.Vertices;
    assert.equal(box.focusBounds.width,Math.max(...vertices.map(p=>p.X))-Math.min(...vertices.map(p=>p.X)));
    assert.equal(box.focusBounds.height,Math.max(...vertices.map(p=>p.Y))-Math.min(...vertices.map(p=>p.Y)));
  }
});

test('missing source, duplicate stages or targets, wrong parent and moved child refuse display',()=>{
  for(const edit of [w=>w.PlacementPlan.NestedStages[0].Footprints.pop(),
    w=>w.PlacementPlan.NestedStages[1].Id=w.PlacementPlan.NestedStages[0].Id,
    w=>w.PlacementPlan.Definition.NestedStages[0].Bindings[1].NodeId=w.PlacementPlan.Definition.NestedStages[0].Bindings[0].NodeId,
    w=>w.PlacementPlan.NestedStages[0].ParentNodeId='missing',
    w=>w.PlacementPlan.NestedStages[0].Placement.Sites[0].Position.X++]){
    const bad=structuredClone(world);edit(bad);assert.throws(()=>Schematic.layout(bad));
  }
});

test('export packages current viewer and exact engine hierarchy',()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1]),world);
});
