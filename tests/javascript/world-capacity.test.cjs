const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder='generated/capacity-world-final';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('capacity-driven content retains every generated footprint and constituent',()=>{
  const plan=Schematic.layout(world);
  assert.equal(plan.boxes.length,10);assert.equal(plan.suitability.size,3);assert.equal(plan.routes.length,4);
  const allocation=world.CapacityPlan.Allocations[0];
  assert.equal(allocation.Count,3);assert.equal(allocation.Request.RequiredQuantity,25);assert.equal(allocation.ProvidedQuantity,30);
  assert.deepEqual(allocation.NodeIds,['capacity-world/camps-0','capacity-world/camps-1','capacity-world/camps-2']);
  for(const id of allocation.NodeIds){
    const box=plan.boxes.find(b=>b.id===id),position=world.Nodes.find(n=>n.Id===id).Position;
    assert.equal(box.node.Position[0],position[0]);assert.equal(box.node.Position[1],position[1]);
    assert.equal(plan.boxes.filter(b=>b.node.ParentId===id).length,2);
  }
});
test('capacity inspection rejects inconsistent counts, source quantities, units and membership',()=>{
  for(const edit of [w=>w.CapacityPlan.Allocations[0].ProvidedQuantity=29,w=>w.CapacityPlan.Allocations[0].Count=2,
    w=>w.CapacityPlan.Allocations[0].NodeIds[0]='capacity-world',w=>w.CapacityPlan.Allocations[0].Source.Quantity=11,
    w=>w.CapacityPlan.Allocations[0].Source.Unit='other',w=>w.CapacityPlan.Allocations.push(w.CapacityPlan.Allocations[0])]){
    const bad=structuredClone(world);edit(bad);assert.throws(()=>Schematic.layout(bad));
  }
});
test('same capacity facts display in topology-only metadata and optional absence stays supported',()=>{
  const logical=structuredClone(world);logical.Surface=null;logical.EnvironmentPlan=null;logical.NetworkPlan=null;logical.PlacementPlan=null;
  logical.Nodes.forEach(n=>n.Position=null);
  assert.equal(Schematic.layout(logical).boxes.length,10);
  const earlier=JSON.parse(fs.readFileSync('generated/placement-feedback-final/world-metadata.json'));
  assert.equal(Schematic.layout(earlier).feasibility.attempts.length,54);
});
test('export embeds exact capacity metadata and current viewer',()=>{
  assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
  assert.deepEqual(JSON.parse(/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'))[1]),world);
});
