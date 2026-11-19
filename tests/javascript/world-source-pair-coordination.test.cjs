const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_SOURCE_PAIR_PACKET||'generated/source-pair-cli-world';
const load=()=>JSON.parse(fs.readFileSync(folder+'/world-metadata.json','utf8'));
test('joint source guard retains a valid individually preferred pose',()=>{
 const w=load(),before=JSON.stringify(w),p=S.populationCoordination(w);
 assert.equal(S.layout(w).boxes.length,3);assert.equal(w.AccessPlan.Sites.length,2);
 assert.equal(p.Attempts.length,2);assert.equal(p.Attempts[0].AddedNogood.Assignments.length,2);
 assert.deepEqual(w.Nodes.find(n=>n.Id.endsWith('sites-0')).Position,[10,5]);
 assert.notDeepEqual(w.Nodes.find(n=>n.Id.endsWith('sites-1')).Position,[7,5]);
 assert.equal(JSON.stringify(w),before);
 const source=w.PlacementPlan.Definition.SourceWorld,guards=source.PlacementPlan.Definition.Nogoods;
 assert.equal(guards.length,1);assert.ok(source.PlacementPlan.Placement.Request.Nogoods.some(n=>n.Id==='future-guard-0001'));
 assert.notEqual(p.Attempts[0].AddedNogood.Id,'future-guard-0001');
});
test('missing and inconsistent pair witnesses refuse display',()=>{
 for(const change of [w=>w.PopulationCoordinationPlan=null,w=>w.PopulationCoordinationPlan.Work--,
  w=>w.PopulationCoordinationPlan.Attempts[0].AddedNogood.Assignments[1].NodeId=w.PopulationCoordinationPlan.Attempts[0].AddedNogood.Assignments[0].NodeId,
  w=>w.PopulationCoordinationPlan.Attempts[0].AddedNogood.Assignments[1].Position.X=Infinity,
  w=>w.PopulationCoordinationPlan.Attempts[0].AddedNogood.Assignments.pop()]){
  const w=load();change(w);assert.throws(()=>S.layout(w),/population coordination/);
 }
});
test('published viewer matches current presentation source',()=>{
 assert.equal(fs.readFileSync(folder+'/schematic.js','utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
