const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_CORRIDOR_CAPACITY_PACKET||'generated/corridor-capacity',world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('shared segment has one physical limit and summed allocation evidence',()=>{
 const layout=Schematic.layout(world),group=layout.physicalCorridors[0],shared=group.SharedDependencies[0].SegmentId;
 assert.equal(world.ServicePlan.SegmentCapacities.length,5);assert.equal(world.ServicePlan.Usage[`segment:${shared}`],8);
 assert.equal(world.ServicePlan.Allocations.reduce((sum,a)=>sum+a.SegmentUses.find(u=>u.SegmentId===shared).Quantity,0),8);
 assert.equal(Object.entries(world.ServicePlan.Usage).filter(([k])=>k.startsWith('segment:')).length,5);
});
test('capacity and traversal tampering refuses display',()=>{
 for(const change of [w=>w.ServicePlan.SegmentCapacities=[],w=>w.ServicePlan.Allocations[0].SegmentUses=[],
  w=>w.ServicePlan.SegmentCapacities[0].Capacity=1,w=>w.ServicePlan.Allocations[0].SegmentUses[0].Traversals=2,
  w=>w.ServicePlan.Allocations[0].SegmentUses[0].Quantity=99,w=>w.ServicePlan.Definition.CorridorCapacities[0].Unit='wrong',
  w=>w.ServicePlan.Usage[`segment:${w.ServicePlan.SegmentCapacities[0].SegmentId}`]=999,
  w=>w.ServicePlan.SegmentCapacities.push(w.ServicePlan.SegmentCapacities[0]),w=>w.CorridorPlan=null]){
  const copy=structuredClone(world);change(copy);assert.throws(()=>Schematic.layout(copy),/service delivery/);
 }
});
test('current viewer retains unselected logical, physical and contained-resource services',()=>{
 for(const file of ['generated/shared-services/world-metadata.json','generated/corridor-reuse/world-metadata.json','generated/resource-area/world-metadata.json'])
  assert.ok(Schematic.layout(JSON.parse(fs.readFileSync(file))).boxes.length>0);
});
test('fresh packet embeds current renderer and exact engine metadata',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const match=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(match[1]),world);
});
