const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const prefix=process.env.WORLD_NESTED_COORDINATION_PREFIX||'generated/nested-coordination-cli-parents-';
const load=n=>JSON.parse(fs.readFileSync(prefix+n+'/world-metadata.json','utf8'));
test('node clauses retain every repeated program and parent frame',()=>{
 for(const parents of [1,2]){
  const w=load(parents),before=JSON.stringify(w),p=S.populationCoordination(w),source=w.PlacementPlan.Definition.SourceWorld;
  assert.equal(p.Version,3);assert.equal(p.Attempts.length,parents+1);assert.equal(S.layout(w).boxes.length,1+parents*3);
  assert.equal(source.PlacementPlan.ProgramInstances.length,parents);assert.equal(source.PlacementPlan.Definition.Nogoods.length,parents);
  for(let i=0;i<parents;i++){
   const id=w.Id+'/containers-'+i;
   assert.deepEqual(w.Nodes.find(n=>n.Id===id).Position,[10+20*i,5]);
   assert.deepEqual(w.Nodes.find(n=>n.Id===id+'/sites-0').Position,[10+20*i,5]);
   assert.notDeepEqual(w.Nodes.find(n=>n.Id===id+'/sites-1').Position,[7+20*i,5]);
  }
  assert.equal(JSON.stringify(w),before);
 }
});
test('old, absent and incorrectly scoped coordination proofs refuse display',()=>{
 for(const change of [w=>w.PopulationCoordinationPlan.Version=1,w=>w.PopulationCoordinationPlan=null,
  w=>w.PopulationCoordinationPlan.Attempts[0].AddedNogood.Assignments[0].NodeId='unknown',
  w=>w.PopulationCoordinationPlan.Attempts[0].AddedNogood.Assignments[1].NodeId=w.PopulationCoordinationPlan.Attempts[0].AddedNogood.Assignments[0].NodeId,
  w=>w.PopulationCoordinationPlan.Attempts[0].AddedNogood.ConsumerRecipeSha256='0'.repeat(64)]){
  const w=load(2);change(w);assert.throws(()=>S.layout(w),/population coordination/);
 }
});
test('published nested viewer is the current JavaScript source',()=>{
 for(const n of [1,2])assert.equal(fs.readFileSync(prefix+n+'/schematic.js','utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
