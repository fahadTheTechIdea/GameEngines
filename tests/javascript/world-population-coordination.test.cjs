const {test}=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs');
const S=require('../../src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js');
const folder=process.env.WORLD_POPULATION_COORDINATION_PACKET||'generated/population-coordination-cli-small';
const worldPrefix=process.env.WORLD_POPULATION_COORDINATION_WORLDS||'generated/population-coordination-cli-';
const read=()=>JSON.parse(fs.readFileSync(folder+'/world-metadata.json','utf8'));
test('saved coordination preserves fixed future centroid and the selected source',()=>{
 const w=read(),before=JSON.stringify(w),view=S.layout(w),p=S.populationCoordination(w);
 assert.equal(view.boxes.length,3);assert.equal(p.Attempts.length,2);assert.equal(p.Attempts[0].Accepted,false);assert.equal(p.Attempts[1].Accepted,true);
 assert.deepEqual(w.Nodes.find(n=>n.ArchetypeId==='feature').Position,[5,3]);assert.equal(JSON.stringify(w),before);
 assert.equal(fs.readFileSync(folder+'/schematic.js','utf8'),fs.readFileSync('src/WorldCreator/WorldCreator.Core/Schematic/world-schematic.js','utf8'));
});
test('missing, tampered, unbounded and inconsistent planning witnesses refuse display',()=>{
 for(const change of [w=>w.PopulationCoordinationPlan=null,w=>w.PopulationCoordinationPlan.Work--,
  w=>w.PopulationCoordinationPlan.Attempts[0].Accepted=true,w=>w.PopulationCoordinationPlan.Attempts[1].Ordinal++,
  w=>w.PopulationCoordinationPlan.Attempts[0].AddedNogood.ConsumerRecipeSha256='0'.repeat(64),
  w=>w.PopulationCoordinationPlan.MaximumAttempts=257,w=>w.PopulationCoordinationPlan.AreaDependenciesSha256='wrong',
  w=>w.PopulationCoordinationPlan.Attempts[0].AddedNogood.Assignments[0].Position.X=Infinity]){
  const w=read();change(w);assert.throws(()=>S.layout(w),/population coordination/);
 }
});
test('both complete density worlds retain every source member and required population',()=>{
 for(const [name,count,nodes] of [['baseline',219,248],['richer-density',241,270]]){
  const w=JSON.parse(fs.readFileSync(worldPrefix+name+'/world-metadata.json','utf8')),before=JSON.stringify(w);
  assert.equal(S.layout(w).boxes.length,nodes);assert.equal(w.AccessPlan.Sites.length,nodes-1);
  assert.equal(w.TerrainDemandPlan.Results.reduce((sum,r)=>sum+r.Count,0),count);assert.equal(w.RoutePlan.Routes.length,19);
  assert.ok(S.populationCoordination(w).Attempts.length>=2);assert.equal(JSON.stringify(w),before);
 }
});
