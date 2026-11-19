const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_RESILIENCE_PACKET||'generated/physical-resilience-92d092679f984c829150135ca4fc744e/cycle/world';
const world=JSON.parse(fs.readFileSync(folder+'/world-metadata.json'));

test('viewer retains every exact physical failure witness without changing geometry',()=>{
 const before=JSON.stringify(world),view=S.layout(world),group=view.physicalCorridors[0];
 assert.deepEqual(group,world.CorridorPlan.Groups[0]);assert.equal(view.boxes.length,5);assert.equal(view.routes.length,8);
 assert.equal(group.Resilience.Alternatives.length,8);assert.equal(group.Segments.length,4);
 assert.equal(JSON.stringify(world),before);
 for(const a of group.Resilience.Alternatives){
  assert.equal(a.Steps.length,3);assert.ok(a.Steps.every(s=>s.SegmentId!==a.FailedSegmentId));
 }
});
test('missing, duplicate, failed, reversed or discontinuous alternatives refuse display',()=>{
 for(const change of [g=>g.Resilience=null,g=>g.Resilience.Version++,g=>g.Resilience.Alternatives.pop(),
  g=>g.Resilience.Alternatives[0]=g.Resilience.Alternatives[1],g=>g.Resilience.Work=0,
  g=>g.Resilience.Alternatives[0].JourneyId='missing',g=>g.Resilience.Alternatives[0].FailedSegmentId='missing',
  g=>g.Resilience.Alternatives[0].Steps[0].SegmentId=g.Resilience.Alternatives[0].FailedSegmentId,
  g=>g.Resilience.Alternatives[0].Steps[0].Forward=!g.Resilience.Alternatives[0].Steps[0].Forward,
  g=>g.Resilience.Alternatives[0].Steps.reverse(),g=>g.Resilience.Alternatives[0].Steps.pop(),
  g=>g.Definition.RequireSingleSegmentFailureTolerance=false]){
  const copy=structuredClone(world);change(copy.CorridorPlan.Groups[0]);assert.throws(()=>S.layout(copy),/physical corridor/);
 }
});
test('physical failure policy ownership is explicit and consistent',()=>{
 for(const value of [false,null,1,'true']){
  const copy=structuredClone(world);copy.CorridorPlan.Definition.Groups[0].RequireSingleSegmentFailureTolerance=value;
  assert.throws(()=>S.layout(copy),/physical corridor/);
 }
 const copy=structuredClone(world);copy.CorridorPlan.Definition.Groups[0].RequireSingleSegmentFailureTolerance=false;
 copy.CorridorPlan.Groups[0].Definition.RequireSingleSegmentFailureTolerance=false;copy.CorridorPlan.Groups[0].Resilience=null;
 assert.equal(S.layout(copy).physicalCorridors[0].Resilience,null);
});
