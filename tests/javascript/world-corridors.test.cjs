const {test}=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const Schematic=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_CORRIDOR_PACKET||'generated/shared-corridors';
const world=JSON.parse(fs.readFileSync(`${folder}/world-metadata.json`));
test('shared passages and crossing expose exact engine geometry',()=>{
 const view=Schematic.layout(world),group=view.physicalCorridors[0];
 assert.equal(view.boxes.length,6);assert.equal(view.routes.length,8);assert.equal(group.Segments.length,4);assert.equal(group.Junctions.length,5);
 assert.deepEqual(group,world.CorridorPlan.Groups[0]);
 const crossing=group.Junctions.find(j=>j.Position.X===30&&j.Position.Y===20);
 assert.equal(crossing.SegmentIds.length,4);assert.equal(crossing.Transitions.length,16);
 assert.equal(group.SharedDependencies.length,2);
});
test('malformed physical connectivity refuses display',()=>{
 for(const mutate of [g=>g.Definition.Level=1,g=>g.Segments[0].Width=999,g=>g.Journeys[0].Steps.reverse(),
  g=>g.Junctions.find(j=>j.Transitions.length).Transitions.pop(),g=>g.SharedDependencies=[],
  g=>g.Segments[0].FromJunctionId='missing',g=>g.Segments[0].ForwardJourneys=['fake'],
  g=>g.Journeys[0].Steps[0].Forward=!g.Journeys[0].Steps[0].Forward,g=>g.Junctions[0].Envelope.Vertices=[]]){
  const copy=structuredClone(world);mutate(copy.CorridorPlan.Groups[0]);assert.throws(()=>Schematic.layout(copy),/physical corridor/);
 }
 const copy=structuredClone(world);copy.Surface=null;assert.throws(()=>Schematic.layout(copy),/corridors require a surface/);
});
test('unselected route and roadless packets have no synthetic corridors',()=>{
 for(const path of ['generated/wilderness-context/world-metadata.json','generated/resource-area/world-metadata.json'])
  assert.deepEqual(Schematic.layout(JSON.parse(fs.readFileSync(path))).physicalCorridors,[]);
});
test('current renderer and embedded document match exported files',()=>{
 assert.equal(fs.readFileSync(`${folder}/schematic.js`,'utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
 const match=/<script id="world-data" type="application\/json">([\s\S]*?)<\/script>/.exec(fs.readFileSync(`${folder}/schematic.html`,'utf8'));
 assert.deepEqual(JSON.parse(match[1]),world);
});
