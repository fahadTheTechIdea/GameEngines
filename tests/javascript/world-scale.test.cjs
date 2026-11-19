const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const world=JSON.parse(fs.readFileSync('generated/scale-qualification-final/areas-16-sites-7/world-metadata.json','utf8'));
test('whole 577-node program renders every source polygon at engine coordinates',()=>{
 const view=Schematic.layout(world);
 assert.equal(view.boxes.length,577);
 const nodes=new Map(world.Nodes.map(n=>[n.Id,n]));
 const access=Schematic.physicalAccess(world,nodes);
 assert.equal(access.size,576);
 for(const site of world.AccessPlan.Sites){
  const node=nodes.get(site.NodeId),source=world.FootprintDefinitions[node.ArchetypeId];
  assert.deepEqual(site.Footprint.Vertices,source.Polygon.Vertices.map(p=>({X:p.X+node.Position[0],Y:p.Y+node.Position[1]})));
 }
});
test('access limit and complete occupancy classification remain enforced',()=>{
 const tooMany=structuredClone(world);tooMany.AccessPlan.Sites=Array(1025).fill(world.AccessPlan.Sites[0]);
 assert.throws(()=>Schematic.layout(tooMany),/Invalid physical access plan/);
 const missing=structuredClone(world);missing.AccessPlan.Sites.pop();
 assert.throws(()=>Schematic.layout(missing),/Incomplete occupancy classification/);
 const stages=structuredClone(world);stages.PlacementPlan.NestedStages.push(stages.PlacementPlan.NestedStages[0]);
 assert.throws(()=>Schematic.placementObservations(stages,new Map(stages.Nodes.map(n=>[n.Id,n]))),/Invalid placement stages/);
});
